"""Shared self-contained Linux application layout for DEB and RPM packages."""
from pathlib import Path
import shutil

ASSETS = Path(__file__).resolve().parent
ROOT = ASSETS.parents[1]


def stage_payload(publish_dir, stage):
    required = ["FolderToISO", "FolderToISO.dll", "FolderToISO.deps.json", "FolderToISO.runtimeconfig.json", "libcoreclr.so"]
    for filename in required:
        if not (publish_dir / filename).is_file():
            raise ValueError(f"Missing self-contained Linux publish file: {filename}")
    application = stage / "usr/lib/foldertoiso"
    shutil.copytree(publish_dir, application)
    launcher = stage / "usr/bin/foldertoiso"
    launcher.parent.mkdir(parents=True, exist_ok=True)
    launcher.write_text('#!/bin/sh\nexec /usr/lib/foldertoiso/FolderToISO "$@"\n', encoding="utf-8", newline="\n")
    desktop = stage / "usr/share/applications/foldertoiso.desktop"
    desktop.parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(ASSETS / "foldertoiso.desktop", desktop)
    icon = stage / "usr/share/icons/hicolor/scalable/apps/foldertoiso.svg"
    icon.parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(ASSETS / "foldertoiso.svg", icon)
    documentation = stage / "usr/share/doc/foldertoiso"
    documentation.mkdir(parents=True)
    shutil.copyfile(ROOT / "README.md", documentation / "README.md")
    integration = stage / "usr/share/foldertoiso/integration"
    shutil.copytree(ASSETS / "integration", integration, ignore=shutil.ignore_patterns("__pycache__"))
    helper = stage / "usr/bin/foldertoiso-context-menu"
    helper.write_text('#!/bin/sh\nexec /usr/bin/python3 /usr/share/foldertoiso/integration/manage.py "$@"\n', encoding="utf-8", newline="\n")
    (stage / "var/lib/foldertoiso").mkdir(parents=True)
    for path in stage.rglob("*"):
        path.chmod(0o755 if path.is_dir() else 0o644)
    stage.chmod(0o755)
    for path in (application / "FolderToISO", launcher, helper):
        path.chmod(0o755)
