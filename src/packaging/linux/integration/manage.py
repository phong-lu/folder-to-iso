"""Install or remove only FolderToISO's optional system file-manager entries."""
import argparse
from pathlib import Path
import shutil

ENTRIES = {
    "foldertoiso-nautilus.py": "usr/share/nautilus-python/extensions/foldertoiso.py",
    "foldertoiso.nemo_action": "usr/share/nemo/actions/foldertoiso.nemo_action",
    "foldertoiso-dolphin.desktop": "usr/share/kio/servicemenus/foldertoiso.desktop",
    "foldertoiso-thunar.desktop": "usr/share/Thunar/sendto/foldertoiso.desktop",
}


def configure(enabled, root=Path("/"), assets=Path(__file__).resolve().parent):
    for source, relative in ENTRIES.items():
        target = root / relative
        if enabled:
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(assets / source, target)
            target.chmod(0o644)
        else:
            target.unlink(missing_ok=True)


def set_enabled(enabled, root=Path("/"), assets=Path(__file__).resolve().parent):
    configure(enabled, root, assets)
    marker = root / "var/lib/foldertoiso/context-menu-enabled"
    if enabled:
        marker.parent.mkdir(parents=True, exist_ok=True)
        marker.write_text("enabled\n", encoding="utf-8")
    else:
        marker.unlink(missing_ok=True)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("mode", choices=["enable", "disable", "apply"])
    mode = parser.parse_args().mode
    if mode == "apply":
        configure(Path("/var/lib/foldertoiso/context-menu-enabled").is_file())
    else:
        set_enabled(mode == "enable")
