"""Optional folder context-menu provider for Nautilus 43+."""
from pathlib import Path
import subprocess

import gi
gi.require_version("Nautilus", "4.0")
from gi.repository import GObject, Nautilus


class FolderToISOMenuProvider(GObject.GObject, Nautilus.MenuProvider):
    def get_file_items(self, files):
        # A loaded extension stops offering actions immediately after it is disabled.
        if not Path(__file__).is_file() or len(files) != 1 or not files[0].is_directory():
            return []
        folder = files[0].get_location().get_path()
        if folder is None:  # Remote URLs are not supported by the ISO writer.
            return []
        item = Nautilus.MenuItem(name="FolderToISO::CreateISO", label="Create ISO with FolderToISO")
        item.connect("activate", self._activate, folder)
        return [item]

    def _activate(self, item, folder):
        subprocess.Popen(["/usr/bin/foldertoiso", "--source", folder], start_new_session=True)
