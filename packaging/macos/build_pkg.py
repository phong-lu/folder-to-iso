"""Build a native Apple Silicon app and a macOS installer package."""
import argparse
import os
from pathlib import Path
import plistlib
import subprocess
import sys
import xml.etree.ElementTree as ET
from tempfile import TemporaryDirectory

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from release_tools import validate_version

ROOT = Path(__file__).resolve().parents[2]


def app_info(version):
    version = validate_version(version)
    return {
        "CFBundleName": "FolderToISO", "CFBundleDisplayName": "FolderToISO",
        "CFBundleIdentifier": "io.github.foldertoiso.app", "CFBundleExecutable": "FolderToISO",
        "CFBundlePackageType": "APPL", "CFBundleShortVersionString": version,
        "CFBundleVersion": version, "LSMinimumSystemVersion": "14.0",
        "NSHighResolutionCapable": True,
        "NSHumanReadableCopyright": "FolderToISO contributors",
    }


def distribution_xml(version):
    version = validate_version(version)
    root = ET.Element("installer-gui-script", {"minSpecVersion": "2"})
    ET.SubElement(root, "title").text = "FolderToISO"
    ET.SubElement(root, "options", {"customize": "never", "rootVolumeOnly": "true", "hostArchitectures": "arm64"})
    volume = ET.SubElement(root, "volume-check")
    ET.SubElement(ET.SubElement(volume, "allowed-os-versions"), "os-version", {"min": "14.0"})
    outline = ET.SubElement(root, "choices-outline")
    ET.SubElement(outline, "line", {"choice": "app"})
    choice = ET.SubElement(root, "choice", {"id": "app", "title": "FolderToISO", "visible": "false"})
    ET.SubElement(choice, "pkg-ref", {"id": "io.github.foldertoiso.app"})
    ET.SubElement(root, "pkg-ref", {"id": "io.github.foldertoiso.app", "version": version}).text = "#foldertoiso-component.pkg"
    return ET.tostring(root, encoding="unicode")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--version", required=True)
    parser.add_argument("--output-dir", type=Path, required=True)
    parser.add_argument("--app-identity", default=os.environ.get("MACOS_APPLICATION_IDENTITY", ""))
    parser.add_argument("--installer-identity", default=os.environ.get("MACOS_INSTALLER_IDENTITY", ""))
    args = parser.parse_args()
    version = validate_version(args.version)
    output = args.output_dir.resolve()
    output.mkdir(parents=True, exist_ok=True)
    with TemporaryDirectory(prefix="foldertoiso-macos-") as temporary:
        stage = Path(temporary) / "root"
        app = stage / "Applications/FolderToISO.app"
        executable = app / "Contents/MacOS/FolderToISO"
        executable.parent.mkdir(parents=True)
        with (app / "Contents/Info.plist").open("wb") as stream:
            plistlib.dump(app_info(version), stream)
        resources = app / "Contents/Resources"
        resources.mkdir()
        (resources / "README.md").write_bytes((ROOT / "README.md").read_bytes())
        subprocess.run(["xcrun", "swiftc", "-swift-version", "5", "-parse-as-library", "-O",
                        "-target", "arm64-apple-macos14.0", "-framework", "SwiftUI", "-framework", "AppKit",
                        str(ROOT / "macos/IsoWriter.swift"), str(ROOT / "macos/FolderToISO.swift"),
                        "-o", str(executable)], check=True)
        identity = args.app_identity or "-"
        signing = ["codesign", "--force", "--sign", identity]
        if args.app_identity:
            signing += ["--options", "runtime", "--timestamp"]
        subprocess.run([*signing, str(app)], check=True)
        subprocess.run(["codesign", "--verify", "--strict", str(app)], check=True)
        components = Path(temporary) / "components.plist"
        subprocess.run(["pkgbuild", "--analyze", "--root", str(stage), str(components)], check=True)
        with components.open("rb") as stream:
            info = plistlib.load(stream)
        for component in info:
            component["BundleIsRelocatable"] = False
            component["BundleHasStrictIdentifier"] = True
        with components.open("wb") as stream:
            plistlib.dump(info, stream)
        component = Path(temporary) / "foldertoiso-component.pkg"
        command = ["pkgbuild", "--root", str(stage), "--component-plist", str(components),
                   "--identifier", "io.github.foldertoiso.app", "--version", version,
                   "--install-location", "/"]
        subprocess.run([*command, str(component)], check=True)
        distribution = Path(temporary) / "Distribution.xml"
        distribution.write_text(distribution_xml(version), encoding="utf-8")
        package = output / f"foldertoiso-{version}-macos-arm64.pkg"
        command = ["productbuild", "--distribution", str(distribution), "--package-path", temporary]
        if args.installer_identity:
            command += ["--sign", args.installer_identity]
        subprocess.run([*command, str(package)], check=True)
        print(package)


if __name__ == "__main__":
    main()
