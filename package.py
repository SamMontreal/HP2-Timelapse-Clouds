"""Package the tested Windows binaries and launchers into a portable overlay."""
import argparse
from hashlib import file_digest
from pathlib import Path
from zipfile import ZipFile, ZIP_DEFLATED

project = Path(__file__).resolve().parent
parser = argparse.ArgumentParser()
parser.add_argument("lapse", type=Path, help="Compiled four-times executable")
parser.add_argument("normal", type=Path, help="Compiled normal-speed executable")
args = parser.parse_args()
dist = project / "dist"
dist.mkdir(exist_ok=True)
archive = dist / "HP2-Timelapse-Clouds-v1.0.1-Windows-x64.zip"
with ZipFile(archive.with_suffix(".tmp"), "w", ZIP_DEFLATED, compresslevel=9) as z:
    for cmd in sorted((project / "launchers").glob("*.cmd")):
        folder = "" if cmd.name == "Play-Volumetric-Clouds.cmd" else "Timelapse-Clouds/"
        z.write(cmd, folder + cmd.name)
    z.write(args.lapse, "Timelapse-Clouds/speed-clouds-lapse-4x.exe")
    z.write(args.normal, "Timelapse-Clouds/speed-clouds-flow-032.exe")
    for guide in ("README.zh-CN.md", "README.en.md"):
        z.write(project / guide, f"Timelapse-Clouds/{guide}")
with ZipFile(archive.with_suffix(".tmp")) as z:
    if z.testzip():
        raise ValueError("Archive verification failed")
archive.with_suffix(".tmp").replace(archive)
with archive.open("rb") as f:
    digest = file_digest(f, "sha256").hexdigest()
(dist / "SHA256SUMS.txt").write_text(f"{digest}  {archive.name}\n", encoding="ascii")
