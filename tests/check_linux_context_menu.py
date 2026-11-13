"""Exercise the installed Nautilus provider using the real system GI bindings."""
import importlib.util
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import patch

spec = importlib.util.spec_from_file_location("foldertoiso_nautilus", "/usr/share/nautilus-python/extensions/foldertoiso.py")
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)
from gi.repository import Gio

provider = module.FolderToISOMenuProvider()
folder = "/tmp/FolderToISO spaces 'quotes' $(literal) caf\u00e9"
selected = SimpleNamespace(is_directory=lambda: True, get_location=lambda: Gio.File.new_for_path(folder))
items = provider.get_file_items([selected])
assert len(items) == 1
with patch.object(module.subprocess, "Popen") as launch:
    items[0].emit("activate")
    launch.assert_called_once_with(["/usr/bin/foldertoiso", "--source", folder], start_new_session=True)
assert provider.get_file_items([]) == []
assert provider.get_file_items([selected, selected]) == []
assert provider.get_file_items([SimpleNamespace(is_directory=lambda: False)]) == []
remote = SimpleNamespace(is_directory=lambda: True, get_location=lambda: Gio.File.new_for_uri("sftp://example/folder"))
assert provider.get_file_items([remote]) == []
print("PASS: Real Nautilus provider, single-folder selection, remote exclusion, and safe argument passing.")
