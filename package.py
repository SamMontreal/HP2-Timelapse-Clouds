"""Package the tested Windows binaries and launchers into a portable overlay."""
import argparse
import json
from hashlib import file_digest
from pathlib import Path
from zipfile import ZipFile, ZIP_DEFLATED

project = Path(__file__).resolve().parent
parser = argparse.ArgumentParser()
parser.add_argument("runtime", type=Path, help="Compiled 0.14 cloud runtime")
parser.add_argument("--build-info", type=Path, help="JSON build manifest for this binary")
args = parser.parse_args()
dist = project / "dist"
dist.mkdir(exist_ok=True)
archive = dist / "HP2-Timelapse-Clouds-v1.4.4-HP2-0.14-Windows-x64.zip"
with ZipFile(archive.with_suffix(".tmp"), "w", ZIP_DEFLATED, compresslevel=9) as z:
    for cmd in sorted((project / "launchers").glob("*.cmd")):
        folder = "" if cmd.name == "Play-Volumetric-Clouds.cmd" else "Timelapse-Clouds/"
        z.write(cmd, folder + cmd.name)
    z.write(args.runtime, "Timelapse-Clouds/speed-clouds.exe")
    z.write(project / "launchers/使用说明.txt", "Timelapse-Clouds/使用说明.txt")
    if args.build_info:
        info = json.loads(args.build_info.read_text(encoding="utf-8-sig"))
        with args.runtime.open("rb") as runtime:
            if info["cloud_exe_sha256"] != file_digest(runtime, "sha256").hexdigest():
                raise ValueError("Build manifest and runtime have different SHA-256 digests")
        z.write(args.build_info, "Timelapse-Clouds/build-info.json")
    for guide in ("README.md", "README.zh-CN.md", "README.en.md"):
        z.write(project / guide, f"Timelapse-Clouds/{guide}")
with ZipFile(archive.with_suffix(".tmp")) as z:
    if z.testzip():
        raise ValueError("Archive verification failed")
archive.with_suffix(".tmp").replace(archive)
with archive.open("rb") as f:
    digest = file_digest(f, "sha256").hexdigest()
(dist / "SHA256SUMS.txt").write_text(f"{digest}  {archive.name}\n", encoding="ascii")
