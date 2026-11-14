"""Package self-contained Windows and Linux publish directories for extraction."""
import argparse
from pathlib import Path
import shutil
import tarfile
from tempfile import TemporaryDirectory
import zipfile

from release_tools import portable_names, validate_version


def build_portable(platform, version, publish_dir, output_dir, redist=None):
    version = validate_version(version)
    publish_dir, output_dir = Path(publish_dir), Path(output_dir)
    required = (["FolderToISO.exe", "FolderToISO.pri", "coreclr.dll"] if platform == "windows"
                else ["FolderToISO", "libcoreclr.so"])
    required += ["FolderToISO.dll", "FolderToISO.deps.json", "FolderToISO.runtimeconfig.json"]
    for name in required:
        if not (publish_dir / name).is_file():
            raise ValueError(f"Missing self-contained publish file: {name}")
    if platform == "windows" and (redist is None or not Path(redist).is_file()):
        raise ValueError("Windows portable packaging requires the Visual C++ runtime installer.")
    output_dir.mkdir(parents=True, exist_ok=True)
    archive = output_dir / portable_names(version)[0 if platform == "windows" else 1]
    with TemporaryDirectory(prefix="foldertoiso-portable-") as temporary:
        app = Path(temporary) / "FolderToISO"
        shutil.copytree(publish_dir, app)
        if platform == "windows":
            shutil.copyfile(redist, app / "vc_redist.x64.exe")
            instructions = ("Extract the entire archive, then run FolderToISO.exe.\n"
                            "The .NET and Windows App SDK runtimes are included. If the app cannot start,\n"
                            "run the included Microsoft vc_redist.x64.exe once to install its runtime.\n")
        else:
            (app / "FolderToISO").chmod(0o755)
            instructions = ("Extract with: tar -xJf FolderToISO_<version>_x64-portable.tar.xz\n"
                            "Run ./FolderToISO/FolderToISO in a graphical desktop session.\n"
                            "The .NET runtime is included. System GTK 3, genisoimage, and .NET native\n"
                            "dependencies are required, as with the Linux installer.\n"
                            "Ubuntu 24.04+/Debian 13+: sudo apt install libgtk-3-0 genisoimage libicu-dev libssl-dev libgssapi-krb5-2\n"
                            "Fedora 44: sudo dnf install gtk3 genisoimage libicu openssl-libs krb5-libs\n")
        (app / "PORTABLE.txt").write_text(instructions + "No shortcuts or context menus are registered.\n", encoding="utf-8")
        shutil.copyfile(Path(__file__).resolve().parents[1] / "LICENSE", app / "LICENSE")
        if platform == "windows":
            with zipfile.ZipFile(archive, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=9) as stream:
                for path in sorted(app.rglob("*")):
                    if path.is_file():
                        stream.write(path, path.relative_to(app.parent).as_posix())
        else:
            def archive_mode(info):
                if info.isdir() or info.name == "FolderToISO/FolderToISO":
                    info.mode = 0o755
                return info

            with tarfile.open(archive, "w:xz", preset=9) as stream:
                stream.add(app, arcname="FolderToISO", filter=archive_mode)
    return archive


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--platform", choices=["windows", "linux"], required=True)
    parser.add_argument("--version", required=True)
    parser.add_argument("--publish-dir", type=Path, required=True)
    parser.add_argument("--output-dir", type=Path, required=True)
    parser.add_argument("--redist", type=Path)
    args = parser.parse_args()
    print(build_portable(args.platform, args.version, args.publish_dir, args.output_dir, args.redist))


if __name__ == "__main__":
    main()
