# FolderToISO

A desktop application for turning a folder into an `.iso` data image on
Windows, Linux and Apple Silicon Macs. Includes nested and empty folders, automatic ISO output
naming, progress, cancellation, and support for individual files larger than
4 GiB.

## Download and install

Official GitHub releases include these installers:

| Platform | Installer |
| --- | --- |
| Windows x64 | `FolderToISO-<version>-windows-x64-Setup.exe` |
| Ubuntu 24.04+ / Debian 13+, amd64 | `foldertoiso_<version>_amd64.deb` |
| Fedora 44, x86_64 | `foldertoiso-<version>-1.x86_64.rpm` |
| macOS 14+, Apple Silicon (M series) | `FolderToISO-<version>-macos-arm64.pkg` |

Windows and Linux packages include .NET. The native macOS app has no .NET,
GTK or Homebrew dependency.

On Windows, run `FolderToISO-<version>-windows-x64-Setup.exe`. The installer
includes the Windows App SDK and Microsoft Visual C++ runtime, installs a Start
menu shortcut, and optionally creates a desktop shortcut.

The Windows installer also offers **Add 'Create ISO with FolderToISO' to the
File Explorer folder context menu**, unchecked by default. When enabled,
right-click a folder and choose **Create ISO with FolderToISO** to open the app
with that folder selected and `<folder path>.iso` filled in. Click **Create ISO**
to start. On Windows 11, the command is in **Show more options**. Uninstalling
removes the command; clearing the option when reinstalling removes it as well.

On Ubuntu 24.04+ or Debian 13+, download the `.deb` and install with APT so system
dependencies, including GTK 3 and `genisoimage`, are installed automatically:

```bash
sudo apt install ./foldertoiso_<version>_amd64.deb
```

Then open FolderToISO from the applications menu or run `foldertoiso`.
A graphical desktop session is required. Release assets include `SHA256SUMS`.

On Fedora, install with DNF to resolve system dependencies:

```bash
sudo dnf install ./foldertoiso-<version>-1.x86_64.rpm
```

RPM context-menu integration is disabled by default. Enable or disable it with:

```bash
sudo foldertoiso-context-menu enable
sudo foldertoiso-context-menu disable
```

The setting survives RPM upgrades, and uninstalling removes the menu entries.

On Apple Silicon Macs, open the `.pkg` to install `/Applications/FolderToISO.app`.
The app uses SwiftUI and the built-in `hdiutil` UDF writer, with automatic output
naming, file/folder pickers, replacement confirmation, progress and cancellation.
Only ARM64 builds for M-series Macs are provided; Intel Macs are not targeted.
Current macOS builds use an ad-hoc signature and are not notarized. Downloaded
packages/apps may require approval in macOS Privacy & Security until Developer
ID signing and notarization are configured.

The Linux `.deb` asks whether to add **Create ISO with FolderToISO** to folder
context menus; the default is **No**, including noninteractive installations.
It supports GNOME Files/Nautilus (43+), Nemo, Dolphin, and Thunar under **Send To**.
The command opens the selected folder with the ISO path filled in; click
**Create ISO** to begin. The package includes the Nautilus Python binding needed
for GNOME integration. Restart your file manager or sign in again after enabling
the option if the command does not appear immediately.

To enable or disable it later, run:

```bash
sudo dpkg-reconfigure foldertoiso
```

Uninstalling removes the Linux menu entries. File-manager integrations use the
respective [Nautilus](https://gnome.pages.gitlab.gnome.org/nautilus-python/nautilus-python-overview.html),
[Nemo](https://github.com/linuxmint/nemo/blob/master/files/usr/share/nemo/actions/sample.nemo_action),
[Dolphin](https://develop.kde.org/docs/apps/dolphin/service-menus/), and
[Thunar](https://docs.xfce.org/xfce/thunar/send-to) extension formats.

## Automatic GitHub releases

Commit the `.github/workflows/release.yml` workflow and `packaging/` files before
creating a release. Publish an official GitHub release with a version tag such
as **`v1.2.3`** (or `1.2.3`) and enter your release description as usual.
The workflow runs for a stable release, including a prerelease promoted to
stable; drafts and prereleases are excluded.

GitHub Actions then:

1. Builds and tests the tagged source on Windows and Linux.
2. Creates Windows `.exe`, Debian `.deb`, Fedora `.rpm`, and Apple Silicon macOS
   `.pkg` installers, using the tag as the application and installer version.
3. Checks Windows installation/uninstallation, Debian and Fedora GTK startup,
   menu opt-in/opt-out and cleanup, and native macOS UDF image creation.
4. After all jobs succeed, uploads all four installers and `SHA256SUMS` to that
   release, preserving its existing description and adding download links and
   installation instructions. If the description is empty, GitHub-generated
   change notes are included.

No custom secret is needed: only the upload job requests `contents: write` for
the built-in `GITHUB_TOKEN`. Re-running a failed workflow replaces the matching
assets and updates a single managed section of the description. Release tags
must have three numeric components, each from 0 to 65534, so the same version
works across the installer formats. Installers appear after the workflow
finishes; publishing a draft or pushing a tag alone does not build them.

The release must be created by a person or a token that can trigger workflows.
GitHub suppresses new workflow runs caused by another workflow's `GITHUB_TOKEN`.
See [GitHub's release events](https://docs.github.com/en/actions/reference/workflows-and-actions/events-that-trigger-workflows#release)
and [workflow trigger rules](https://docs.github.com/en/actions/how-tos/writing-workflows/choosing-when-your-workflow-runs/triggering-a-workflow).

Packaging helper checks can be run locally without creating a release:

```bash
python -m unittest discover -s tests -p test_packaging.py -v
```

Installer compilation and installation checks run on their respective GitHub
runners; the full release workflow has not been executed from this workspace.

## macOS development

Build on an Apple Silicon Mac with macOS 14+ and Xcode command-line tools:

```bash
python3 packaging/macos/build_pkg.py --version 1.2.3 --output-dir artifacts/release
```

The macOS implementation is in `macos/` and builds separately from the .NET
solution. The package restricts installation to ARM64 and macOS 14 or later.
The builder accepts `--app-identity` and `--installer-identity` for Developer ID
certificates already installed in the build machine's keychain. CI currently
uses ad-hoc app signing; no private signing certificates or notarization
credentials are configured.

The macOS CI checks native ARM64 compilation, package installation, cancellation,
existing-output preservation, UDF mounting, nested/empty folders, and hashes of
files larger than 4 GiB. Native macOS and Fedora checks require the CI runners;
they cannot run in this Windows development environment.

## Linux

Requires a graphical Linux desktop, GTK 3, `genisoimage`, and the .NET 10 SDK
when building from source. On Ubuntu 24.04 / Debian with the .NET SDK installed:

```bash
sudo apt install libgtk-3-0t64 genisoimage
dotnet build FolderToISO.sln -c Release
dotnet run --project FolderToISO/FolderToISO.csproj -c Release
```

Older Ubuntu/Debian releases use `libgtk-3-0` instead of `libgtk-3-0t64`.
Fedora uses the `gtk3` and `genisoimage` packages. GTK supports both X11 and
Wayland sessions. No WinUI or Windows SDK packages are used by the Linux build.
The UI calls the system GTK 3 library directly, so no managed GTK package is
required.

To publish a self-contained Linux x64 application (the destination machine
still needs GTK 3 and `genisoimage`, but does not need .NET):

```bash
dotnet publish FolderToISO/FolderToISO.csproj -c Release -r linux-x64 --self-contained true -o artifacts/linux-x64
./artifacts/linux-x64/FolderToISO
```

The Linux writer invokes `genisoimage` with UDF, Rock Ridge and Joliet support.
For large files, read or mount the resulting image as **UDF**: its ISO9660 view
can report a smaller file size. For example:

```bash
sudo mount -t udf -o loop,ro /path/to/folder.iso /mnt/iso
```

`genisoimage` has filename and filesystem limits of its own; its UDF names are
coupled to Joliet. The app rejects names longer than 103 characters on Linux
to prevent silent truncation. The Linux images
are hybrid images rather than the UDF-only 2.01 images produced on Windows.
See the [genisoimage manual](https://manpages.debian.org/bookworm/genisoimage/genisoimage.1.en.html)
for backend options and limitations.

## Windows

Requires Windows 10 (2004+) or Windows 11, x64. Building requires the .NET 10
SDK and Visual Studio with the WinUI application development workload.

```powershell
dotnet build FolderToISO.sln -c Release -p:Platform=x64
.\FolderToISO\bin\x64\Release\net10.0-windows10.0.19041.0\win-x64\FolderToISO.exe
```

Or open `FolderToISO.sln` in Visual Studio, select x64 and launch FolderToISO.
Windows keeps the native WinUI 3 interface and built-in IMAPI2 UDF 2.01 writer;
no external ISO utility or optical drive is required.

```powershell
dotnet publish FolderToISO/FolderToISO.csproj -c Release -p:Platform=x64 -o artifacts/publish
```

Distribute the entire publish folder, not just the executable.

The project chooses its UI from the build host: Windows uses WinUI and Linux
uses GTK. `-p:DesktopPlatform=Linux` explicitly selects the Linux UI for a
compile check on another host. Running that build still requires Linux.

## Use

1. Browse to or type the source folder.
2. The ISO path updates automatically, for example `/home/me/Photos` becomes
   `/home/me/Photos.iso`. You can edit it or choose another location with Save as.
3. Set the volume label and select **Create ISO**.

The folder's contents appear at the image root. Save the ISO outside the source
folder. Existing outputs require confirmation and are replaced only after the
new image completes. Temporary images are removed on ordinary failure or
cancellation. On Linux cancellation terminates the writer process; on Windows,
cancellation during IMAPI preparation takes effect after the COM call returns.

Use a filesystem supporting large output files, such as ext4, Btrfs, XFS, NTFS
or exFAT. FAT32 cannot store files larger than 4 GiB. Keep the source unchanged
until creation finishes. Symlinks and junctions in the source and in either
path's parent directories are rejected. These are data images, not bootable
installation media, and are not intended to preserve filesystem metadata as a
backup tool. Image size is subject to backend limits and available disk space.

## Checks

Run on either platform (Linux requires `genisoimage`):

```bash
dotnet run --project tests/FolderToISO.Integration.csproj -c Release
dotnet run --project tests/FolderToISO.Integration.csproj -c Release -- --large
```

The large check creates a sparse source of 4 GiB + 1 MiB and a real ISO requiring
a little over 4 GiB of free disk space. Checks cover UDF descriptors, data beyond
the 4 GiB boundary, path validation, automatic naming, cancellation preserving
existing output, and temporary-file cleanup. Linux also checks missing tools,
writer failure, cancellation of a running writer, and output path aliases.
Artifacts remain under `artifacts/`; the latest image path is recorded in
`artifacts/last-integration-image.txt`.

The GitHub Actions workflow builds on Windows and Linux. Its Linux job opens
the GTK window under Xvfb, creates small and large images, mounts them as UDF,
and compares the entire directory tree and file hashes with the source.

In the Windows development environment, both UI variants compile and the
Windows integration checks pass. Linux runtime, GTK visual behavior, and
Linux-mounted image checks require a Linux machine or the CI workflow; they
have not been run in this Windows-only environment.
