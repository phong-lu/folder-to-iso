"""Build an amd64 Debian package from a self-contained Linux publish folder."""
import argparse
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
from tempfile import TemporaryDirectory

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from release_tools import validate_repository, validate_version
sys.path.insert(0, str(Path(__file__).resolve().parent))
from payload import stage_payload

ASSETS = Path(__file__).resolve().parent
ROOT = ASSETS.parents[1]


def stage_package(publish_dir, stage, version, publisher, repository=""):
    version = validate_version(version)
    if not re.fullmatch(r"[A-Za-z0-9-]+", publisher):
        raise ValueError("Publisher must be a GitHub owner name.")
    if repository:
        validate_repository(repository)
    stage_payload(publish_dir, stage)
    installed_size = (sum(path.stat().st_size for path in stage.rglob("*") if path.is_file()) + 1023) // 1024
    control = stage / "DEBIAN/control"
    control.parent.mkdir()
    homepage = f"Homepage: https://github.com/{repository}\n" if repository else ""
    control.write_text(
        f"Package: foldertoiso\nVersion: {version}\nSection: utils\nPriority: optional\n"
        f"Architecture: amd64\nMaintainer: {publisher} <{publisher}@users.noreply.github.com>\n"
        f"Installed-Size: {installed_size}\n"
        "Depends: libc6 (>= 2.38), libgcc-s1, libstdc++6, zlib1g, libgssapi-krb5-2, "
        "libssl3t64 | libssl3, libicu74 | libicu76 | libicu78 | libicu72, "
        "libgtk-3-0t64 | libgtk-3-0, genisoimage, debconf (>= 0.5) | debconf-2.0, python3, python3-nautilus\n"
        + homepage + "Description: Create ISO disc images from folders\n"
        " Native GTK desktop application with automatic output naming, progress,\n"
        " cancellation, and support for files larger than 4 GiB through UDF.\n"
        " Includes the .NET runtime. A graphical desktop session is required.\n",
        encoding="utf-8", newline="\n")
    for name in ("templates", "config", "postinst", "postrm"):
        shutil.copyfile(ASSETS / "debian" / name, control.parent / name)
    for path in stage.rglob("*"):
        path.chmod(0o755 if path.is_dir() else 0o644)
    stage.chmod(0o755)
    (stage / "usr/lib/foldertoiso/FolderToISO").chmod(0o755)
    (stage / "usr/bin/foldertoiso").chmod(0o755)
    (stage / "usr/bin/foldertoiso-context-menu").chmod(0o755)
    for name in ("config", "postinst", "postrm"):
        (control.parent / name).chmod(0o755)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--version", required=True)
    parser.add_argument("--publisher", required=True)
    parser.add_argument("--repository", default=os.environ.get("GITHUB_REPOSITORY", ""))
    parser.add_argument("--publish-dir", required=True, type=Path)
    parser.add_argument("--output-dir", required=True, type=Path)
    args = parser.parse_args()
    args.output_dir.mkdir(parents=True, exist_ok=True)
    package = args.output_dir.resolve() / f"foldertoiso_{validate_version(args.version)}_amd64.deb"
    with TemporaryDirectory(prefix="foldertoiso-deb-") as temporary:
        stage = Path(temporary) / "package"
        stage_package(args.publish_dir, stage, args.version, args.publisher, args.repository)
        subprocess.run(["dpkg-deb", "--root-owner-group", "--build", str(stage), str(package)], check=True)
    subprocess.run(["dpkg-deb", "--info", str(package)], check=True)
    print(package)


if __name__ == "__main__":
    main()
