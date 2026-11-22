"""Build the Windows download and its four runtime assets plus bundled packages."""
import argparse
import hashlib
import json
import os
import shutil
import subprocess
import sys
import tempfile
import urllib.request
from pathlib import Path
from zipfile import ZIP_DEFLATED, ZipFile

here = Path(__file__).resolve().parent
project = here.parent
out = project / "dist" / "updater"
bundle = out / "bundle"
prefix = "HP2-Timelapse-Clouds-auto-update/"
seeds = json.loads((here / "bundled.json").read_text(encoding="utf-8"))
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--hud", type=Path)
parser.add_argument("--clouds", type=Path)
args = parser.parse_args()

def digest(file):
    with file.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()

exe = out / "HP2-UpdateAndPlay.exe"
if not exe.is_file():
    sys.exit("Run updater/build.cmd first.")
env = dict(os.environ, HP2_PACK_EXE=str(exe))
version = subprocess.check_output(
    ["powershell", "-NoProfile", "-Command", "([version](Get-Item -LiteralPath $env:HP2_PACK_EXE).VersionInfo.FileVersion).ToString(3)"],
    env=env, text=True).strip()
packages = bundle / "Packages"
packages.mkdir(parents=True, exist_ok=True)
files = ["HP2-UpdateAndPlay.exe", "launcher.json", "使用说明.txt", "仅更新.cmd"]
shutil.copy2(exe, bundle / exe.name)
for name in files[2:]:
    shutil.copy2(here / name, bundle / name)
(bundle / "launcher.json").write_text(json.dumps({"Version": version, "Hash": digest(exe)}, indent=2) + "\n", encoding="ascii")
for key, repo, suffix in (("hud", "HP2-ShiftLight", ""), ("clouds", "HP2-Timelapse-Clouds", "-HP2-0.14")):
    release = seeds[key]
    name = f"{repo}-v{release['Version']}{suffix}-Windows-x64.zip"
    supplied = getattr(args, key)
    local = supplied or (project.parent / repo / "dist" / name)
    dest = packages / name
    if local.is_file():
        if digest(local) != release["Hash"]:
            sys.exit(f"Package digest mismatch: {local}")
        shutil.copy2(local, dest)
    elif supplied:
        sys.exit(f"Package missing: {local}")
    else:
        url = f"https://raw.githubusercontent.com/SamMontreal/{repo}/{release['Commit']}/{name}"
        with urllib.request.urlopen(url, timeout=45) as response, tempfile.TemporaryFile() as scratch:
            shutil.copyfileobj(response, scratch)
            scratch.seek(0)
            if hashlib.file_digest(scratch, "sha256").hexdigest() != release["Hash"]:
                sys.exit(f"Package digest mismatch: {name}")
            scratch.seek(0)
            with dest.open("wb") as output:
                shutil.copyfileobj(scratch, output)
    files.append(f"Packages/{name}")

archive = out / "HP2-Mods-AutoUpdate-Windows-x64.zip"
with ZipFile(archive.with_suffix(".tmp"), "w", ZIP_DEFLATED, compresslevel=6) as result:
    for name in files:
        result.write(bundle / name, prefix + name)
with ZipFile(archive.with_suffix(".tmp")) as check:
    assert check.testzip() is None and len(check.namelist()) == len(files)
archive.with_suffix(".tmp").replace(archive)
(out / "SHA256SUMS.txt").write_text(f"{digest(archive)}  {archive.name}\n", encoding="ascii")
print(archive)
