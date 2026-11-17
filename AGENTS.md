# FolderToISO agent instructions

## Scope and current direction

FolderToISO creates mountable data ISO images from folders on Windows, Linux,
and macOS. These are data images, not bootable installation media or complete
filesystem backups.

Keep the current native platform architecture. A MAUI migration was tried and
explicitly rejected by the user. Do not resume that migration, introduce MAUI,
or replace GTK 3 with GTK 4 without a new user request. Ignored MAUI preview
files under `src/artifacts/` are abandoned experiments, not project source.

## Repository layout

Keep application code, solution files, tests, packaging scripts, and generated
build outputs under `src/`. The repository root is for documentation and
instructions, plus necessary repository configuration such as `.github/`,
`.gitignore`, and `.gitattributes`.

```text
AGENTS.md                         Repository instructions
README.md                         User and build documentation
LICENSE
.github/workflows/build.yml        Platform builds and verification
.github/workflows/release.yml      Release packaging and publication
src/
  FolderToISO.slnx                 Windows/Linux solution
  FolderToISO/
    App.xaml, MainWindow.xaml      Windows WinUI interface
    Linux/                        GTK 3 interface and native bindings
    Services/                     Shared validation and platform ISO writers
  macos/                          SwiftUI application and Swift ISO writer
  packaging/                      Windows, Linux, macOS and release helpers
  tests/                          Integration, UI startup and packaging checks
  artifacts/                      Generated outputs; ignored by Git
```

Do not use ignored artifacts, cached packages, or nested checkouts as the
authoritative source. A protected nested checkout may remain under root
`artifacts/pr-checkout`; it is unrelated to the active solution. Existing local
tool caches can also remain at root. Do not delete or reorganize unrelated
local data as part of routine source edits.

## Platform architecture

| Platform | UI and runtime | ISO backend |
| --- | --- | --- |
| Windows x64 | C# / .NET 10, WinUI 3, Windows App SDK 1.8 | Windows IMAPI2; UDF 2.01 |
| Linux x64 | C# / .NET 10, system GTK 3 through direct native bindings | `genisoimage`; hybrid UDF, ISO9660, Rock Ridge and Joliet |
| macOS ARM64 | Swift / SwiftUI, macOS 14+ | `/usr/bin/hdiutil makehybrid`; UDF |

`src/FolderToISO/FolderToISO.csproj` selects Windows on a Windows build host
and Linux otherwise. `-p:DesktopPlatform=Linux` permits a Linux compile check
on another host; it does not make the Linux UI runnable there. macOS builds
separately from the .NET solution. The WPF setting in the Windows startup test
project belongs to the test harness, not the application UI.

## Build and run

Run the following commands from `src/` unless stated otherwise:

```sh
cd src
```

Windows requires the .NET 10 SDK and WinUI development tooling:

```powershell
dotnet build FolderToISO.slnx -c Release -p:Platform=x64
.\FolderToISO\bin\x64\Release\net10.0-windows10.0.19041.0\win-x64\FolderToISO.exe
dotnet publish FolderToISO/FolderToISO.csproj -c Release -p:Platform=x64 -o artifacts/publish
```

Distribute the entire Windows publish directory. Preserve the project target
that copies `FolderToISO.pri` into the publish output; the installer validates
that this resource index is present.

Linux requires GTK 3 and `genisoimage`, plus the .NET 10 SDK for builds:

```sh
sudo apt install libgtk-3-0t64 genisoimage
dotnet build FolderToISO.slnx -c Release
dotnet run --project FolderToISO/FolderToISO.csproj -c Release
dotnet publish FolderToISO/FolderToISO.csproj -c Release -r linux-x64 --self-contained true -o artifacts/linux-x64
```

Older Debian/Ubuntu systems may call the GTK package `libgtk-3-0`; Fedora uses
`gtk3`. Self-contained publishing includes .NET, not the system GTK or ISO tools.

Build macOS packages on an Apple Silicon Mac with Xcode command-line tools:

```sh
python3 packaging/macos/build_pkg.py --version 1.2.3 --output-dir artifacts/release
```

The macOS builder supports application and installer signing identities.
Existing CI uses ad-hoc app signing; do not claim notarization or Developer ID
signing unless it has actually been configured and verified.

## Verification

Run checks appropriate to the files changed. Report which platforms were
actually tested; compiling or checking a backend does not verify the UI.

Packaging checks (Python standard library only):

```sh
python -m unittest discover -s tests -p test_packaging.py -v
```

Windows/Linux ISO integration checks:

```sh
dotnet run --project tests/FolderToISO.Integration.csproj -c Release
dotnet run --project tests/FolderToISO.Integration.csproj -c Release -- --large
```

The large check creates an image exceeding 4 GiB and needs sufficient disk
space. Generated images stay under `src/artifacts/`; the latest image path is
written to `artifacts/last-integration-image.txt` relative to `src/`.

Windows UI startup regression check, after building the app:

```powershell
dotnet run --project tests/FolderToISO.WindowsStartup.csproj -c Release -- FolderToISO/bin/x64/Release/net10.0-windows10.0.19041.0/win-x64/FolderToISO.exe
```

Native macOS core checks:

```sh
mkdir -p artifacts
xcrun swiftc -swift-version 5 -parse-as-library macos/IsoWriter.swift tests/macos_core_tests.swift -o artifacts/macos-core-tests
./artifacts/macos-core-tests
```

`python3 tests/check_macos.py` checks the installed macOS app and mounts real
images, including a large image. Linux CI opens the GTK UI under Xvfb and
mounts images as UDF before checking file trees and hashes with
`tests/verify_image.py`. See the workflows for full platform verification.

After the `src/` relocation, the Windows Release build, 19 packaging tests,
and standard Windows ISO integration checks passed. Linux and macOS runtime
checks were not performed locally during that relocation.

## Behavior to preserve

- `--source <absolute folder path>` supports file-manager launches, including
  paths with spaces and Unicode. Invalid arguments produce a useful error.
- Selecting a source suggests an adjacent `<folder path>.iso` output and a
  volume label derived from the folder name.
- Preserve nested folders, empty folders, and files larger than 4 GiB.
- Validate source and destination paths and volume labels before writing.
  Reject output inside the source and linked directory aliases that could
  bypass that check. Preserve existing symbolic-link/junction rejection.
- Labels are 1–32 characters with no path punctuation or control characters.
- Linux rejects entry names longer than 103 characters to prevent truncation
  by the current writer. Large Linux images must be read as UDF.
- Confirm replacements in the UI. Write to a temporary file beside the
  destination and replace the destination only after successful completion.
  Failures and cancellation must preserve an existing output and clean up
  incomplete temporary images.
- Keep progress, cancellation, output-folder opening, and startup button-state
  behavior working. Backend changes must retain cancellation of child tools.

## Packaging, CI and path conventions

Windows uses Inno Setup; Linux has DEB, RPM and optional file-manager
integration; macOS has an ARM64 app, PKG and portable archive. Release helpers
validate versions, assemble assets and checksums, and publish release content.
Do not publish a release merely to validate a local change.

Both workflows set shell-step `working-directory: src`. GitHub Action inputs
such as upload/download artifact paths remain relative to the repository
root and therefore use `src/artifacts/...`.

Packaging paths resolved relative to their scripts must distinguish the source
root (`src`) from the repository root. App assets and Swift sources live under
`src`; `README.md` and `LICENSE` remain at repository root. Preserve that
distinction when editing packaging helpers.

Use the established C# nullable/implicit-using conventions and existing native
UI patterns. Keep changes focused, update documentation when commands or
behavior change, and keep build outputs and local experiments out of Git.
