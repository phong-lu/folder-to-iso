"""Build a Fedora x86_64 RPM from the shared self-contained Linux payload."""
import argparse
import os
from pathlib import Path
import re
import subprocess
import sys
from tempfile import TemporaryDirectory

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
sys.path.insert(0, str(Path(__file__).resolve().parent))
from release_tools import validate_repository, validate_version
from payload import stage_payload


def render_spec(version, publisher, repository=""):
    version = validate_version(version)
    if not re.fullmatch(r"[A-Za-z0-9-]+", publisher):
        raise ValueError("Publisher must be a GitHub owner name.")
    if repository:
        validate_repository(repository)
    url = "https://github.com/" + (repository or publisher)
    template = Path(__file__).with_name("rpm").joinpath("foldertoiso.spec.in").read_text(encoding="utf-8")
    return template.replace("@VERSION@", version).replace("@PUBLISHER@", publisher).replace("@URL@", url)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--version", required=True)
    parser.add_argument("--publisher", required=True)
    parser.add_argument("--repository", default=os.environ.get("GITHUB_REPOSITORY", ""))
    parser.add_argument("--publish-dir", required=True, type=Path)
    parser.add_argument("--output-dir", required=True, type=Path)
    args = parser.parse_args()
    spec = render_spec(args.version, args.publisher, args.repository)
    args.output_dir.mkdir(parents=True, exist_ok=True)
    with TemporaryDirectory(prefix="foldertoiso-rpm-") as temporary:
        top = Path(temporary)
        payload = top / "payload"
        stage_payload(args.publish_dir, payload)
        for name in ("BUILD", "BUILDROOT", "RPMS", "SOURCES", "SPECS", "SRPMS"):
            (top / name).mkdir()
        spec_path = top / "SPECS/foldertoiso.spec"
        spec_path.write_text(spec, encoding="utf-8", newline="\n")
        subprocess.run(["rpmbuild", "-bb", "--target", "x86_64", "--define", f"_topdir {top}",
                        "--define", f"payload_dir {payload}", "--define", f"_rpmdir {args.output_dir.resolve()}",
                        "--define", "_rpmfilename %{NAME}-%{VERSION}-%{RELEASE}.%{ARCH}.rpm", str(spec_path)], check=True)
    package = args.output_dir / f"foldertoiso-{validate_version(args.version)}-1.x86_64.rpm"
    subprocess.run(["rpm", "-qpi", str(package)], check=True)
    print(package)


if __name__ == "__main__":
    main()
