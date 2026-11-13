"""Compare a mounted UDF image with its source, including empty folders and large files."""
import hashlib
from pathlib import Path
import sys

source, mounted = map(Path, sys.argv[1:])


def tree(root):
    return {str(path.relative_to(root)): path.is_dir() for path in root.rglob("*")}


def digest(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").digest()


assert tree(source) == tree(mounted), "Mounted paths differ from source"
for path in source.rglob("*"):
    if path.is_file():
        other = mounted / path.relative_to(source)
        assert path.stat().st_size == other.stat().st_size, f"Size differs: {path}"
        assert digest(path) == digest(other), f"Content differs: {path}"
print("PASS: Mounted UDF tree, empty folders, Unicode names and all file contents match.")
