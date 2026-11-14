"""Release metadata, asset checksums, and idempotent GitHub release updates."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
from tempfile import TemporaryDirectory
from urllib.parse import quote

START = "<!-- foldertoiso-installers:start -->"
END = "<!-- foldertoiso-installers:end -->"


def validate_version(tag):
    match = re.fullmatch(r"v?((?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*))", tag)
    if not match or any(int(part) > 65534 for part in match[1].split(".")):
        raise ValueError("Use an official release tag such as v1.2.3 (three numeric components, each 0-65534).")
    return match[1]


def validate_repository(repository):
    if not re.fullmatch(r"[A-Za-z0-9-]+/[A-Za-z0-9_.-]+", repository):
        raise ValueError("Expected a GitHub repository in owner/name format.")
    return repository


def installer_names(version):
    version = validate_version(version)
    return [f"foldertoiso_{version}_x64.exe", f"foldertoiso_{version}_amd64.deb",
            f"foldertoiso_{version}_x86_64.rpm", f"foldertoiso_{version}_arm64.pkg"]


def release_metadata(event):
    release = event["release"]
    if release.get("draft") or release.get("prerelease"):
        raise ValueError("Only published official releases receive installers.")
    version = validate_version(release["tag_name"])
    repository = validate_repository(event["repository"]["full_name"])
    return version, repository.split("/")[0]


def write_checksums(directory, version):
    directory = Path(directory)
    lines = []
    for filename in installer_names(version):
        path = directory / filename
        if not path.is_file() or path.stat().st_size == 0:
            raise ValueError(f"Missing or empty installer: {filename}")
        with path.open("rb") as stream:
            digest = hashlib.file_digest(stream, "sha256").hexdigest()
        lines.append(f"{digest}  {filename}\n")
    result = directory / "SHA256SUMS"
    result.write_text("".join(lines), encoding="utf-8", newline="\n")
    return result


def installation_notes(version, release_url, tag):
    windows, deb, rpm, macos = installer_names(version)
    base = f"{release_url.split('/releases/')[0]}/releases/download/{quote(tag, safe='')}"
    return f"""{START}
## Install FolderToISO {version}

| Platform | Installer |
| --- | --- |
| Windows x64 (Windows 10 2004+ / Windows 11) | [{windows}]({base}/{windows}) |
| Linux amd64 (Ubuntu 24.04+ / Debian 13+) | [{deb}]({base}/{deb}) |
| Linux x86_64 (Fedora 44) | [{rpm}]({base}/{rpm}) |
| macOS 14+ (Apple Silicon / M series) | [{macos}]({base}/{macos}) |

**Windows:** Run the setup executable. It includes the .NET and Windows App SDK
runtimes, installs the Microsoft Visual C++ runtime, and adds a Start menu shortcut.
An unchecked installer option adds **Create ISO with FolderToISO** to folder
context menus (under **Show more options** on Windows 11).

**Linux:** Download the `.deb`, then install it with:

```bash
sudo apt install ./{deb}
```

**Fedora:** Install the RPM with:

```bash
sudo dnf install ./{rpm}
```

RPM folder context menus are disabled by default. Enable or disable them with
`sudo foldertoiso-context-menu enable` or `sudo foldertoiso-context-menu disable`.

**macOS (Apple Silicon):** Open the `.pkg` to install FolderToISO in Applications.
The native SwiftUI app uses macOS's built-in image writer; no .NET or Homebrew
installation is required. This build is ad-hoc signed unless Developer ID signing
is configured, and is not notarized.

Open FolderToISO from the applications menu or run `foldertoiso`. The package
includes .NET; APT installs GTK 3, genisoimage, and other system dependencies.
A graphical desktop session is required.
The Debian installer offers optional folder context-menu integration, disabled
by default, for Nautilus, Nemo, Dolphin, and Thunar (Send To). Change it later with
`sudo dpkg-reconfigure foldertoiso`. Restart the file manager if needed.

For files larger than 4 GiB in Linux-created images, mount or read the image as
UDF. Linux filenames are limited to 103 characters per path component.

[SHA-256 checksums]({base}/SHA256SUMS) are included with these installers.
{END}"""


def merge_notes(body, section):
    body = body or ""
    if START in body or END in body:
        if body.count(START) != 1 or body.count(END) != 1 or body.index(END) < body.index(START):
            raise ValueError("Malformed managed installer notes; refusing to overwrite release text.")
        start, end = body.index(START), body.index(END) + len(END)
        return body[:start] + section + body[end:]
    return body.rstrip() + ("\n\n" if body.strip() else "") + section + "\n"


def gh(*arguments, payload=None):
    try:
        result = subprocess.run(["gh", *arguments], input=json.dumps(payload) if payload is not None else None,
                                text=True, capture_output=True, check=True)
    except subprocess.CalledProcessError as error:
        raise RuntimeError(error.stderr.strip() or "GitHub CLI request failed.") from error
    return result.stdout


def publish(event, assets_dir):
    version, _ = release_metadata(event)
    repository = validate_repository(event["repository"]["full_name"])
    tag = event["release"]["tag_name"]
    # Read fresh notes so edits made while the installers build are preserved.
    release_id = int(event["release"]["id"])
    current = json.loads(gh("api", f"repos/{repository}/releases/{release_id}"))
    if current.get("draft") or current.get("prerelease") or current["tag_name"] != tag:
        raise ValueError("The release is no longer an official release with the expected tag.")
    sums = write_checksums(assets_dir, version)
    body = current.get("body") or ""
    if not body.strip():
        generated = json.loads(gh("api", "--method", "POST", f"repos/{repository}/releases/generate-notes",
                                  "--input", "-", payload={"tag_name": tag}))
        body = generated.get("body") or ""
    notes = merge_notes(body, installation_notes(version, current["html_url"], tag))
    assets = [str(Path(assets_dir) / name) for name in installer_names(version)] + [str(sums)]
    # Upload only after all build jobs succeed. Clobber supports re-running failed jobs.
    gh("release", "upload", tag, *assets, "--repo", repository, "--clobber")
    with TemporaryDirectory() as temporary:
        notes_file = Path(temporary) / "release-notes.md"
        notes_file.write_text(notes, encoding="utf-8")
        gh("release", "edit", tag, "--repo", repository, "--notes-file", str(notes_file))
    if os.environ.get("GITHUB_STEP_SUMMARY"):
        with Path(os.environ["GITHUB_STEP_SUMMARY"]).open("a", encoding="utf-8") as stream:
            stream.write(f"Uploaded all four installers and SHA256SUMS to {current['html_url']}\n")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("command", choices=["metadata", "publish"])
    parser.add_argument("--assets-dir", type=Path, default=Path("artifacts/release"))
    args = parser.parse_args()
    event = json.loads(Path(os.environ["GITHUB_EVENT_PATH"]).read_text(encoding="utf-8"))
    if args.command == "metadata":
        version, publisher = release_metadata(event)
        with Path(os.environ["GITHUB_OUTPUT"]).open("a", encoding="utf-8") as stream:
            stream.write(f"version={version}\npublisher={publisher}\n")
    else:
        publish(event, args.assets_dir)


if __name__ == "__main__":
    main()
