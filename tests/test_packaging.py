import hashlib
import importlib.util
import json
import os
import plistlib
from types import ModuleType, SimpleNamespace
from pathlib import Path
import sys
from tempfile import TemporaryDirectory
import unittest
import xml.etree.ElementTree as ET
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "packaging"))
import release_tools as release

spec = importlib.util.spec_from_file_location("build_deb", ROOT / "packaging/linux/build_deb.py")
deb = importlib.util.module_from_spec(spec)
spec.loader.exec_module(deb)
menu_spec = importlib.util.spec_from_file_location("linux_menu", ROOT / "packaging/linux/integration/manage.py")
linux_menu = importlib.util.module_from_spec(menu_spec)
menu_spec.loader.exec_module(linux_menu)
rpm_spec = importlib.util.spec_from_file_location("build_rpm", ROOT / "packaging/linux/build_rpm.py")
rpm = importlib.util.module_from_spec(rpm_spec)
rpm_spec.loader.exec_module(rpm)
mac_spec = importlib.util.spec_from_file_location("build_pkg", ROOT / "packaging/macos/build_pkg.py")
mac = importlib.util.module_from_spec(mac_spec)
mac_spec.loader.exec_module(mac)


def event():
    return {"release": {"id": 42, "tag_name": "v1.2.3", "draft": False, "prerelease": False},
            "repository": {"full_name": "example/FolderToISO"}}


class ReleaseTests(unittest.TestCase):
    def test_official_release_version(self):
        self.assertEqual(release.release_metadata(event()), ("1.2.3", "example"))
        self.assertEqual(release.validate_version("1.0.0"), "1.0.0")
        for tag in ("v1.2", "v01.2.3", "v1.2.3-beta", "v1.2.3+build", "v65535.0.0", "v1.2.3\nattack", "$(echo bad)"):
            with self.subTest(tag=tag), self.assertRaises(ValueError):
                release.validate_version(tag)

    def test_draft_and_prerelease_rejected(self):
        for field in ("draft", "prerelease"):
            payload = event()
            payload["release"][field] = True
            with self.assertRaises(ValueError):
                release.release_metadata(payload)

    def test_preserves_author_notes_and_is_idempotent(self):
        section = release.installation_notes("1.2.3", "https://github.com/example/FolderToISO/releases/tag/v1.2.3", "v1.2.3")
        author_notes = "## What's changed\n\nFixed the ISO output name.\n"
        first = release.merge_notes(author_notes, section)
        with_footer = first + "\nMaintainer footer.\n"
        second = release.merge_notes(with_footer, section)
        self.assertEqual(with_footer, second)
        self.assertTrue(second.startswith(author_notes))
        self.assertTrue(second.endswith("Maintainer footer.\n"))
        self.assertEqual(second.count(release.START), 1)

    def test_malformed_notes_cannot_overwrite_text(self):
        for body in (release.START, release.END, release.END + release.START, release.START * 2 + release.END):
            with self.assertRaises(ValueError):
                release.merge_notes(body, "replacement")

    def test_checksums_require_all_four_nonempty_installers(self):
        with TemporaryDirectory() as temporary:
            directory = Path(temporary)
            names = release.installer_names("1.2.3")
            self.assertEqual(len(names), 4)
            for name in names:
                (directory / name).write_bytes(name.encode())
            for name in names:
                (directory / name).write_bytes(b"")
                with self.subTest(name=name), self.assertRaises(ValueError):
                    release.write_checksums(directory, "1.2.3")
                (directory / name).write_bytes(name.encode())
            lines = release.write_checksums(directory, "1.2.3").read_text().splitlines()
            self.assertEqual(lines, [f"{hashlib.sha256(name.encode()).hexdigest()}  {name}" for name in names])

    def test_publish_uses_fresh_notes_and_uploads_exact_assets(self):
        with TemporaryDirectory() as temporary:
            directory = Path(temporary)
            for filename in release.installer_names("1.2.3"):
                (directory / filename).write_bytes(b"installer")
            calls = []
            saved_notes = []

            def fake_gh(*arguments, payload=None):
                calls.append(arguments)
                if arguments[0] == "api":
                    return json.dumps({**event()["release"], "body": "Latest maintainer edits.",
                                       "html_url": "https://github.com/example/FolderToISO/releases/tag/v1.2.3"})
                if arguments[:2] == ("release", "edit"):
                    saved_notes.append(Path(arguments[arguments.index("--notes-file") + 1]).read_text())
                return ""

            with patch.object(release, "gh", side_effect=fake_gh):
                release.publish(event(), directory)
            self.assertTrue(saved_notes[0].startswith("Latest maintainer edits."))
            upload = next(call for call in calls if call[:2] == ("release", "upload"))
            self.assertIn(str(directory / "SHA256SUMS"), upload)
            self.assertIn("--clobber", upload)
            self.assertLess(calls.index(upload), next(i for i, call in enumerate(calls) if call[:2] == ("release", "edit")))

    def test_empty_description_gets_generated_release_notes(self):
        with TemporaryDirectory() as temporary:
            directory = Path(temporary)
            for filename in release.installer_names("1.2.3"):
                (directory / filename).write_bytes(b"installer")
            saved_notes = []

            def fake_gh(*arguments, payload=None):
                if any("generate-notes" in argument for argument in arguments):
                    self.assertEqual(payload, {"tag_name": "v1.2.3"})
                    return json.dumps({"body": "Generated changes."})
                if arguments[0] == "api":
                    return json.dumps({**event()["release"], "body": None,
                                       "html_url": "https://github.com/example/FolderToISO/releases/tag/v1.2.3"})
                if arguments[:2] == ("release", "edit"):
                    saved_notes.append(Path(arguments[arguments.index("--notes-file") + 1]).read_text())
                return ""

            with patch.object(release, "gh", side_effect=fake_gh):
                release.publish(event(), directory)
            self.assertTrue(saved_notes[0].startswith("Generated changes."))

    def test_release_changed_to_prerelease_prevents_upload(self):
        current = {**event()["release"], "prerelease": True}
        with patch.object(release, "gh", return_value=json.dumps(current)) as github:
            with self.assertRaisesRegex(ValueError, "no longer an official release"):
                release.publish(event(), Path("unused"))
        self.assertEqual(github.call_count, 1)


class DebianPackageTests(unittest.TestCase):
    def test_staged_package_contains_runtime_launcher_and_desktop_entry(self):
        with TemporaryDirectory() as temporary:
            directory = Path(temporary)
            publish = directory / "publish"
            publish.mkdir()
            for filename in ("FolderToISO", "FolderToISO.dll", "FolderToISO.deps.json", "FolderToISO.runtimeconfig.json", "libcoreclr.so"):
                (publish / filename).write_bytes(b"published payload")
            stage = directory / "package"
            deb.stage_package(publish, stage, "1.2.3", "example", "example/FolderToISO")
            control = (stage / "DEBIAN/control").read_text()
            for field in ("Version: 1.2.3\n", "Architecture: amd64\n", "genisoimage", "libgtk-3-0t64 | libgtk-3-0"):
                self.assertIn(field, control)
            self.assertEqual((stage / "usr/lib/foldertoiso/libcoreclr.so").read_bytes(), b"published payload")
            self.assertIn('"$@"', (stage / "usr/bin/foldertoiso").read_text())
            self.assertIn("Exec=foldertoiso\n", (stage / "usr/share/applications/foldertoiso.desktop").read_text())
            self.assertIn("Default: false\n", (stage / "DEBIAN/templates").read_text())
            self.assertTrue((stage / "DEBIAN/postinst").is_file())
            for relative in linux_menu.ENTRIES.values():
                self.assertFalse((stage / relative).exists(), "Menu must not be enabled in the package payload")
            if os.name != "nt":
                self.assertEqual((stage / "usr/lib/foldertoiso/FolderToISO").stat().st_mode & 0o777, 0o755)

    def test_framework_dependent_payload_cannot_be_packaged(self):
        with TemporaryDirectory() as temporary:
            with self.assertRaisesRegex(ValueError, "self-contained"):
                deb.stage_package(Path(temporary), Path(temporary) / "stage", "1.2.3", "example")


class LinuxMenuTests(unittest.TestCase):
    def test_rpm_menu_setting_persists_and_defaults_to_off(self):
        with TemporaryDirectory() as temporary:
            root = Path(temporary)
            assets = ROOT / "packaging/linux/integration"
            marker = root / "var/lib/foldertoiso/context-menu-enabled"
            self.assertFalse(marker.exists())
            linux_menu.set_enabled(True, root, assets)
            self.assertTrue(marker.exists())
            linux_menu.configure(marker.exists(), root, assets)
            self.assertTrue(all((root / relative).exists() for relative in linux_menu.ENTRIES.values()))
            linux_menu.set_enabled(False, root, assets)
            self.assertFalse(marker.exists())

    def test_enable_disable_is_idempotent_and_preserves_other_extensions(self):
        with TemporaryDirectory() as temporary:
            root = Path(temporary)
            assets = ROOT / "packaging/linux/integration"
            unrelated = root / "usr/share/nautilus-python/extensions/another-extension.py"
            unrelated.parent.mkdir(parents=True)
            unrelated.write_text("Another application owns this file.")
            linux_menu.configure(False, root, assets)
            for _ in range(2):
                linux_menu.configure(True, root, assets)
                for source, relative in linux_menu.ENTRIES.items():
                    self.assertEqual((root / relative).read_bytes(), (assets / source).read_bytes())
            for _ in range(2):
                linux_menu.configure(False, root, assets)
                for relative in linux_menu.ENTRIES.values():
                    self.assertFalse((root / relative).exists())
            self.assertEqual(unrelated.read_text(), "Another application owns this file.")

    def test_uninstall_cleanup_covers_all_supported_managers(self):
        cleanup = (ROOT / "packaging/linux/debian/postrm").read_text()
        for relative in linux_menu.ENTRIES.values():
            self.assertIn("/" + relative, cleanup)


class AdditionalPlatformTests(unittest.TestCase):
    def test_nautilus_binding_versions(self):
        for available in ({"4.1"}, {"4.0"}, {"4.0", "4.1"}, set()):
            with self.subTest(available=available):
                gi = ModuleType("gi")
                selected = []

                def require_version(namespace, version):
                    self.assertEqual(namespace, "Nautilus")
                    if version not in available:
                        raise ValueError("Unavailable version")
                    selected.append(version)

                gi.require_version = require_version
                repository = ModuleType("gi.repository")
                repository.GObject = SimpleNamespace(GObject=type("GObject", (), {}))
                repository.Nautilus = SimpleNamespace(MenuProvider=type("MenuProvider", (), {}))
                provider_spec = importlib.util.spec_from_file_location(
                    "nautilus_provider", ROOT / "packaging/linux/integration/foldertoiso-nautilus.py")
                provider = importlib.util.module_from_spec(provider_spec)
                with patch.dict(sys.modules, {"gi": gi, "gi.repository": repository}):
                    if available:
                        provider_spec.loader.exec_module(provider)
                        self.assertEqual(selected, ["4.1" if "4.1" in available else "4.0"])
                    else:
                        with self.assertRaises(ValueError):
                            provider_spec.loader.exec_module(provider)

    def test_rpm_metadata_and_uninstall_script(self):
        spec = rpm.render_spec("v1.2.3", "example", "example/FolderToISO")
        self.assertIn("Version: 1.2.3\n", spec)
        self.assertIn("BuildArch: x86_64\n", spec)
        self.assertIn("nautilus-python >= 4", spec)
        self.assertIn('if [ "$1" -eq 0 ]; then', spec)
        self.assertIn("manage.py apply", spec)
        self.assertNotIn("@VERSION@", spec)
        with self.assertRaises(ValueError):
            rpm.render_spec("1.2.3", "bad\nInjected: field")

    def test_mac_bundle_and_installer_target_apple_silicon(self):
        info = plistlib.loads(plistlib.dumps(mac.app_info("v1.2.3")))
        self.assertEqual(info["CFBundleShortVersionString"], "1.2.3")
        self.assertEqual(info["LSMinimumSystemVersion"], "14.0")
        distribution = ET.fromstring(mac.distribution_xml("1.2.3"))
        self.assertEqual(distribution.find("options").get("hostArchitectures"), "arm64")
        self.assertEqual(distribution.find("volume-check/allowed-os-versions/os-version").get("min"), "14.0")


if __name__ == "__main__":
    unittest.main()
