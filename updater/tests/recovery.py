"""Exercise recovery after a process exits between two file replacements."""
import os
from pathlib import Path
import subprocess
import tempfile

src = Path(__file__).resolve().parents[1]
compiler = Path(os.environ["SystemRoot"]) / "Microsoft.NET/Framework64/v4.0.30319/csc.exe"
harness = """
using System;
using System.IO;
using HP2ModUpdates;
class Check {
    static int Main(string[] a) {
        if (a[0]=="recover") { Transaction.Recover(a[1]); return 0; }
        using (var tx=new Transaction(a[1])) {
            try { tx.PutText("saves/player/save.dat","unmanaged");return 1; }
            catch (InvalidDataException) {}
            tx.PutText("Play-HP2-ShiftLight.cmd","changed");
            tx.PutText("Play-Volumetric-Clouds.cmd","created");
            tx.PutText("HP2-ShiftLight-Readme.txt","pending");
            int checks=0;
            tx.Commit(delegate { if (++checks==4) Environment.Exit(77); });
        }
        return 1;
    }
}
"""
with tempfile.TemporaryDirectory() as tmp:
    root = Path(tmp)
    program = root / "check.exe"
    (root / "Check.cs").write_text(harness, encoding="utf-8")
    subprocess.run([str(compiler), "/nologo", "/platform:x64", "/out:" + str(program),
                    "/reference:System.Web.Extensions.dll", "/resource:" + str(src / "bundled.json") + ",HP2.bundled.json",
                    str(src / "Catalog.cs"), str(src / "Transaction.cs"), str(root / "Check.cs")], check=True)
    original = root / "Play-HP2-ShiftLight.cmd"
    original.write_bytes(b"original")
    save = root / "saves/player/save.dat"
    save.parent.mkdir(parents=True)
    save.write_bytes(b"player progress")
    assert subprocess.run([str(program), "interrupt", tmp]).returncode == 77
    assert original.read_bytes() == b"changed"
    assert (root / "Play-Volumetric-Clouds.cmd").exists()
    for _ in range(2):
        subprocess.run([str(program), "recover", tmp], check=True)
    assert original.read_bytes() == b"original" and save.read_bytes() == b"player progress"
    assert not (root / "Play-Volumetric-Clouds.cmd").exists()
    assert not (root / "HP2-ModUpdates/transaction").exists()
print("Interrupted update restored successfully.")
