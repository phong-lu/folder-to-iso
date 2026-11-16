"""Build a MAUI Mac Catalyst Apple Silicon app and a macOS installer package."""
import argparse
import os
from pathlib import Path
import plistlib
import shutil
import subprocess
import sys
import xml.etree.ElementTree as ET
from tempfile import TemporaryDirectory

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from release_tools import validate_version

ROOT = Path(__file__).resolve().parents[2]


def validate_bundle(app, version):
    """Validate the MAUI-produced bundle instead of replacing its generated plist."""
    with (app / "Contents/Info.plist").open("rb") as stream:
        info = plistlib.load(stream)
    if info.get("CFBundleIdentifier") != "io.github.foldertoiso.app":
        raise ValueError("Unexpected application bundle identifier.")
    if info.get("CFBundleShortVersionString") != validate_version(version):
        raise ValueError("Application and installer versions differ.")
    executable = app / "Contents/MacOS" / info.get("CFBundleExecutable", "")
    if not executable.is_file():
        raise ValueError(f"Missing published MAUI executable: {executable}. "
                         f"Bundle files: {sorted(str(path.relative_to(app)) for path in app.rglob('*'))}")
    return info


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
        build = Path(temporary) / "build"
        built_app = build / "Release/net10.0-maccatalyst/maccatalyst-arm64/FolderToISO.app"
        command = ["dotnet", "publish", str(ROOT / "FolderToISO/FolderToISO.csproj"),
                   "-c", "Release", "-p:DesktopPlatform=MacCatalyst", "-r", "maccatalyst-arm64",
                   "--self-contained", "true", "-p:CreatePackage=false",
                   "-p:EnableCodeSigning=true", "-p:CodesignRequireProvisioningProfile=false",
                   f"-p:CodesignKey={args.app_identity or '-'}",
                   f"-p:Version={version}", f"-p:ApplicationDisplayVersion={version}",
                   f"-p:BaseOutputPath={build}{os.sep}"]
        if args.app_identity:
            command.append("-p:UseHardenedRuntime=true")
        subprocess.run(command, check=True)
        # Keep the SDK's bundle and publish paths aligned under an isolated build root.
        validate_bundle(built_app, version)
        app.parent.mkdir(parents=True)
        # Preserve framework symlinks and the SDK's signatures on nested native code.
        shutil.copytree(built_app, app, symlinks=True)
        subprocess.run(["codesign", "--verify", "--deep", "--strict", str(app)], check=True)
        portable = output / f"FolderToISO_{version}_arm64-portable.zip"
        subprocess.run(["ditto", "-c", "-k", "--sequesterRsrc", "--keepParent", str(app), str(portable)], check=True)
        print(portable)
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
        package = output / f"FolderToISO_{version}_arm64-setup.pkg"
        command = ["productbuild", "--distribution", str(distribution), "--package-path", temporary]
        if args.installer_identity:
            command += ["--sign", args.installer_identity]
        subprocess.run([*command, str(package)], check=True)
        print(package)


if __name__ == "__main__":
    main()
