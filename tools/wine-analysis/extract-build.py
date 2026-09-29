"""Build configured Wine C objects under CodeQL, without linking a Wine runtime.

The module allowlist is an explicit extraction boundary. Calls beyond it remain
declaration-only graph nodes. Run in a fresh out-of-tree Wine build directory.
"""
import argparse
import json
import os
from pathlib import Path
import re
import subprocess

MODULES = {
    "krnl386.exe16", "user.exe16", "gdi.exe16", "win87em.dll16",
    "mmsystem.dll16", "shell.dll16", "commdlg.dll16", "keyboard.drv16",
    "system.drv16", "sound.drv16", "kernel32", "kernelbase", "ntdll",
    "user32", "gdi32", "win32u", "winmm", "msvcrt", "advapi32",
    "shell32", "comdlg32", "shlwapi", "version", "comctl32",
}

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--build-dir", type=Path, required=True)
args = parser.parse_args()
os.chdir(args.build_dir)
targets = {}
for match in re.finditer(r"^(dlls/([^/]+)/[^\s:]+\.o): (\S+\.(?:c|cc|cpp))$",
                         Path("Makefile").read_text(), re.MULTILINE):
    target, module, source = match.groups()
    if module in MODULES:
        targets[target] = source
Path("analysis-build-targets.json").write_text(json.dumps({
    "modules": sorted(MODULES), "targets": targets}, indent=2) + "\n")
print(f"Compiling {len(targets)} C/C++ objects in {len(MODULES)} selected modules", flush=True)
if not targets:
    raise SystemExit("No configured targets found; refusing an empty extraction")
subprocess.run(["make", "-j4", *sorted(targets)], check=True)
