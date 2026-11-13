"""Exercise the installed ARM64 app's native UDF writer and mounted contents."""
import hashlib
from pathlib import Path
import plistlib
import subprocess
import tempfile

APP = "/Applications/FolderToISO.app/Contents/MacOS/FolderToISO"


def run(*arguments, ok=True):
    result = subprocess.run([APP, *map(str, arguments)], text=True, capture_output=True)
    if ok:
        assert result.returncode == 0, result.stdout + result.stderr
    else:
        assert result.returncode != 0, "Invalid image request was accepted"
    return result


def digest(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").digest()


with tempfile.TemporaryDirectory(prefix="foldertoiso-macos-") as temporary:
    root = Path(temporary).resolve()
    source = root / "Source with spaces and caf\u00e9"
    (source / "Nested").mkdir(parents=True)
    (source / "Empty").mkdir()
    (source / "Nested/hello.txt").write_text("Hello from macOS")
    (source / "Nested/quotes ' and $(literal).txt").write_text("Special filename")
    image = root / "source.iso"
    run("--create", source, image, "Mac_Test")
    original = image.read_bytes()
    assert b"NSR02" in original[:1048576] or b"NSR03" in original[:1048576]
    run("--create", source, image, "Test", ok=False)
    assert image.read_bytes() == original
    run("--create", source, source / "bad.iso", "Test", ok=False)
    run("--create", source, root / "bad.txt", "Test", ok=False)
    link = source / "link"
    link.symlink_to(source / "Nested", target_is_directory=True)
    run("--create", source, image, "Test", "--replace", ok=False)
    assert image.read_bytes() == original
    link.unlink()
    # Sparse source; the UDF image itself needs a little over 4 GiB of free disk.
    with (source / "large.bin").open("wb") as stream:
        stream.truncate((1 << 32) + 1048576)
        stream.seek((1 << 32) + 123)
        stream.write(b"FolderToISO beyond 4 GiB")
    run("--create", source, image, "Large_Mac", "--replace")
    attached = subprocess.run(["hdiutil", "attach", "-readonly", "-nobrowse", "-plist", str(image)],
                              capture_output=True, check=True)
    volumes = plistlib.loads(attached.stdout)["system-entities"]
    mount = Path(next(entity["mount-point"] for entity in volumes if "mount-point" in entity))
    try:
        expected = {str(p.relative_to(source)): p.is_dir() for p in source.rglob("*")}
        actual = {str(p.relative_to(mount)): p.is_dir() for p in mount.rglob("*")}
        assert expected == actual, "Mounted UDF directory tree differs"
        for path in source.rglob("*"):
            if path.is_file():
                other = mount / path.relative_to(source)
                assert path.stat().st_size == other.stat().st_size
                assert digest(path) == digest(other), f"Content differs: {path}"
    finally:
        subprocess.run(["hdiutil", "detach", str(mount)], check=True)
    assert not list(root.glob(".foldertoiso-*.partial.iso"))
print("PASS: macOS UDF writer, replacement protection, validation, mounted contents and files over 4 GiB.")
