<h1 align="center">&#128191; FolderToISO</h1>

<p align="center">
  <strong>Turn folders into ISO images on Windows, Linux, and macOS.</strong>
</p>

<hr>

<p align="center">
  <a href="https://github.com/phong-lu/folder-to-iso/releases/latest"><img src="https://img.shields.io/badge/Download-Installers-0078D4?style=for-the-badge&amp;logo=github&amp;logoColor=white" alt="Download installers"></a>
  <a href="https://github.com/phong-lu/folder-to-iso/actions/workflows/build.yml"><img src="https://img.shields.io/github/actions/workflow/status/phong-lu/folder-to-iso/build.yml?style=for-the-badge&amp;logo=githubactions&amp;logoColor=white&amp;label=Build" alt="Build and verify status"></a>
  <a href="https://github.com/phong-lu/folder-to-iso/releases"><img src="https://img.shields.io/github/v/release/phong-lu/folder-to-iso?style=for-the-badge&amp;logo=github&amp;logoColor=white&amp;label=Release&amp;color=8250DF" alt="Latest release"></a>
  <a href="#download-and-install"><img src="https://img.shields.io/badge/Setup-Guide-2DA44E?style=for-the-badge&amp;logo=readthedocs&amp;logoColor=white" alt="Installation guide"></a>
  <a href="https://github.com/phong-lu/folder-to-iso/issues"><img src="https://img.shields.io/badge/Feedback-Issues-D95040?style=for-the-badge&amp;logo=github&amp;logoColor=white" alt="Bug reports and feature requests"></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/License-MIT-D4A017?style=for-the-badge" alt="MIT license"></a>
</p>

<p align="center">
  <img src="https://img.shields.io/badge/Windows-WinUI_3-0078D4?style=flat-square" alt="Windows with WinUI 3">
  <img src="https://img.shields.io/badge/Linux-GTK_3-E95420?style=flat-square&amp;logo=linux&amp;logoColor=white" alt="Linux with GTK 3">
  <img src="https://img.shields.io/badge/macOS-SwiftUI-F05138?style=flat-square&amp;logo=swift&amp;logoColor=white" alt="macOS with SwiftUI">
  <img src="https://img.shields.io/badge/.NET-10-512BD4?style=flat-square&amp;logo=dotnet&amp;logoColor=white" alt=".NET 10">
  <img src="https://img.shields.io/badge/Image_format-UDF-555555?style=flat-square" alt="UDF image format">
</p>

FolderToISO packages a directory's contents into a mountable `.iso` file using
an interface built for each platform: WinUI 3 on Windows, GTK 3 on Linux, and
SwiftUI on macOS. Use it to distribute a collection of files, organize an offline
archive, or prepare a data image without an optical drive.

It preserves nested and empty folders, supports individual files larger than
4 GiB through UDF, and provides automatic output naming, progress, cancellation,
and confirmation before replacing an existing image.

## Contents

- [Features](#features)
- [How it works](#how-it-works)
- [Download and install](#download-and-install)
- [Use](#use)
- [Build from source](#build-from-source)
- [Checks](#checks)
- [Automatic GitHub releases](#automatic-github-releases)
- [Project structure](#project-structure)
- [Troubleshooting](#troubleshooting)
- [Contributing](#contributing)
- [License](#license)
- [Topics](#topics)

## Features

| Feature | Details |
| --- | --- |
| Native desktop interfaces | WinUI 3, GTK 3, and SwiftUI interfaces with folder and output pickers. |
| UDF image creation | Supports files larger than 4 GiB; Linux images also include ISO9660, Rock Ridge, and Joliet views. |
| Folder structure | Includes nested directories and empty folders, with source contents at the image root. |
| Automatic naming | Selecting a folder suggests an adjacent `<folder path>.iso` output. |
| Custom volume labels | Choose the name shown when the image is mounted. |
| Progress and cancellation | Follow creation and cancel an active operation. |
| Output protection | Confirm replacements; existing output is replaced after the new image completes. |
| Optional folder context menus | Open a selected folder from Windows Explorer or supported Linux file managers. |
| Platform installers | Windows EXE, Debian DEB, Fedora RPM, and Apple Silicon macOS PKG packages. |
| Release verification | Automated build and integration checks, plus SHA-256 checksums for release installers. |

These are **data images**, not bootable installation media. FolderToISO is not a
filesystem backup tool and does not aim to preserve all source metadata.

## How it works

Each platform uses its own image-writing backend:

| Platform | Interface | Image writer | Image format |
| --- | --- | --- | --- |
| Windows x64 | WinUI 3 / .NET 10 | Built-in Windows IMAPI2 | UDF 2.01 |
| Linux x64 | GTK 3 / .NET 10 | System `genisoimage` | Hybrid UDF, ISO9660, Rock Ridge, and Joliet |
| macOS ARM64 | SwiftUI / Swift | Built-in `hdiutil makehybrid` | UDF |

The app validates the request, checks source entries, writes a temporary image
beside the destination, and moves the completed image into place. Ordinary
failures and cancellation remove the temporary image while preserving an
existing destination. Keep the source folder unchanged during creation.

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
until creation finishes. Symbolic links in the source are rejected. The Windows
and Linux implementations also reject junctions and linked parent directories. These are data images, not bootable
installation media, and are not intended to preserve filesystem metadata as a
backup tool. Image size is subject to backend limits and available disk space.

## Build from source

Clone the repository, then follow the instructions for your build platform:

```bash
git clone https://github.com/phong-lu/folder-to-iso.git
cd folder-to-iso
```

### macOS

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
files larger than 4 GiB. Run platform-specific checks on the corresponding operating system or CI runner.

### Linux

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

### Windows

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

## Checks

Run the .NET integration checks on Windows or Linux (Linux requires `genisoimage`):

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

The [build workflow](.github/workflows/build.yml) builds and checks Windows,
Linux, and macOS. Linux checks open the GTK window under Xvfb, create small and
large images, mount them as UDF, and compare the directory tree and file hashes
with the source. macOS checks compile the native app, test cancellation, install
the package, and verify mounted image contents, including files over 4 GiB.

To run the native macOS core checks on a Mac:

```bash
mkdir -p artifacts
xcrun swiftc -swift-version 5 -parse-as-library macos/IsoWriter.swift tests/macos_core_tests.swift -o artifacts/macos-core-tests
./artifacts/macos-core-tests
```

After installing the macOS package, run the installed-app integration check:

```bash
python3 tests/check_macos.py
```

The installed-app check also creates a real image larger than 4 GiB and mounts
it to compare its contents. Allow sufficient free disk space.

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

Installer compilation and installation checks run on their respective platform runners.

## Project structure

```text
folder-to-iso/
|-- FolderToISO/          # Windows and Linux application, validation, and writers
|-- macos/                # Native SwiftUI application and macOS image writer
|-- packaging/
|   |-- windows/          # Windows installer and installation checks
|   |-- linux/            # DEB/RPM builders and file-manager integration
|   |-- macos/            # ARM64 app and PKG builder
|   `-- release_tools.py  # Release metadata, checksums, and asset publishing
|-- tests/                # Image, cancellation, packaging, and installation checks
|-- .github/workflows/    # Build verification and release automation
|-- FolderToISO.sln       # Windows/Linux .NET solution
`-- LICENSE              # MIT license
```

## Troubleshooting

| Problem | What to check |
| --- | --- |
| A large file appears smaller inside a Linux-created ISO | Mount or read the image as **UDF**. The ISO9660 view can report a smaller size. |
| The output cannot hold a large image | Use a filesystem such as NTFS, exFAT, ext4, Btrfs, or XFS and check free space. FAT32 cannot hold files larger than 4 GiB. |
| Linux reports that `genisoimage` is missing | Install it through your distribution's package manager. DEB/RPM installation resolves this dependency. |
| Linux rejects a filename | Shorten each filename or directory name to at most 103 characters to avoid backend truncation. |
| A source entry is rejected | Remove symbolic links, junctions, or unsupported special entries from the source. |
| A folder context-menu command is missing | Enable the optional integration during installation or with the commands above; restart the Linux file manager if needed. On Windows 11, check **Show more options**. |
| macOS asks for approval before opening | Current packages use ad-hoc signing and are not notarized. Review the downloaded app in macOS **Privacy & Security**. |
| An existing ISO cannot be replaced | Confirm replacement in the app and ensure the destination is writable. |

## Contributing

Bug reports and pull requests are welcome. For a bug report, include your OS and
architecture, app version, steps to reproduce, and the complete error message.
For image-content problems, include the writing platform, how you mounted the
image, and whether the source contains files larger than 4 GiB.

For code changes, describe the behavior being changed and run the relevant
platform checks. Changes to installer or release helpers should also pass:

```bash
python -m unittest discover -s tests -p test_packaging.py -v
```

Avoid committing generated installers, build output, or test images. Use the
[issue tracker](https://github.com/phong-lu/folder-to-iso/issues) to discuss
feature requests and platform support.

## License

Licensed under the [MIT License](LICENSE). Copyright (c) 2026 Phong Lu.

## Topics

`folder-to-iso` ? `iso` ? `iso-image` ? `udf` ? `desktop-app` ? `cross-platform` ?
`windows` ? `linux` ? `macos` ? `winui3` ? `gtk3` ? `swiftui` ? `dotnet` ? `csharp` ?
`swift`
