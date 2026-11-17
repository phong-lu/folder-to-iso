#!/bin/bash
# Run in the Fedora release-test container as root.
set -euo pipefail
dnf install -y artifacts/release/FolderToISO_*_x86_64-setup.rpm xorg-x11-server-Xvfb xdotool
menus=(
  /usr/share/nautilus-python/extensions/foldertoiso.py
  /usr/share/nemo/actions/foldertoiso.nemo_action
  /usr/share/kio/servicemenus/foldertoiso.desktop
  /usr/share/Thunar/sendto/foldertoiso.desktop
)
for entry in "${menus[@]}"; do test ! -e "$entry"; done
test ! -e /var/lib/foldertoiso/context-menu-enabled
Xvfb :99 -screen 0 1024x768x24 &
server=$!
trap 'kill "$server" 2>/dev/null || true' EXIT
export DISPLAY=:99
for attempt in {1..50}; do
  if xdotool getdisplaygeometry >/dev/null 2>&1; then break; fi
  sleep 0.1
done
foldertoiso > artifacts/rpm-ui.log 2>&1 &
app=$!
trap 'kill "$app" "$server" 2>/dev/null || true' EXIT
timeout 15s xdotool search --sync --onlyvisible --name '^FolderToISO$'
kill -0 "$app"
kill "$app"
wait "$app" || true
foldertoiso-context-menu enable
for entry in "${menus[@]}"; do test -f "$entry"; done
python3 tests/check_linux_context_menu.py
rpm -Uvh --replacepkgs artifacts/release/FolderToISO_*_x86_64-setup.rpm
for entry in "${menus[@]}"; do test -f "$entry"; done
foldertoiso-context-menu disable
for entry in "${menus[@]}"; do test ! -e "$entry"; done
foldertoiso-context-menu enable
dnf remove -y foldertoiso
for entry in "${menus[@]}"; do test ! -e "$entry"; done
test ! -e /var/lib/foldertoiso/context-menu-enabled
echo 'PASS: RPM install, desktop startup, menu opt-in, upgrade persistence, opt-out and uninstall.'
