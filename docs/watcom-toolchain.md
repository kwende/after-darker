# Reproduce the Win16 test toolchain

This is the exact Open Watcom installation prepared on 2026-09-16. Use the
pinned release below, not `Current-build`, for the first compiler-built NE test
library. The compiler runs on modern Windows x64 and targets 16-bit Windows.

## Installation identity

| Item | Value |
| --- | --- |
| Project | [Open Watcom v2](https://github.com/open-watcom/open-watcom-v2) |
| Release | [2026-09-01-Build](https://github.com/open-watcom/open-watcom-v2/releases/tag/2026-09-01-Build) |
| Asset | `open-watcom-2_0-c-win-x64.exe` |
| Download size | 128,112,046 bytes |
| SHA-256 | `542a7edda4e58349d72a678704f20a013e1c46e19c915186395401df1b7faa1e` |
| Toolchain root | `C:\tools\open-watcom\2026-09-01` |
| Download directory | `C:\tools\open-watcom\downloads` |
| Local archive name | `open-watcom-2026-09-01-win-x64.exe` |
| License | Distribution's `license.txt`, Sybase Open Watcom Public License |

The archive's size and SHA-256 were verified against the GitHub release asset
metadata. The installer executable contains a ZIP archive; the actual setup
extracted that archive with Python's standard-library `zipfile` rather than
running the installer. It retained the complete distribution directory layout.
No registry or persistent system/user environment changes were made.

## Download and verify

Prerequisites: Windows x64, PowerShell, `curl.exe`, and Python 3. These were
already present on the development machine. GitHub CLI is not needed for these
reproduction commands. Use an empty/new installation directory; if the existing
installation is present, inspect its `SETUP.md` and activate it instead.

```powershell
$watcomRoot = 'C:\tools\open-watcom\2026-09-01'
$watcomDownloads = 'C:\tools\open-watcom\downloads'
$watcomArchive = Join-Path $watcomDownloads 'open-watcom-2026-09-01-win-x64.exe'
if (Test-Path -LiteralPath $watcomRoot) { throw 'Toolchain directory already exists; inspect it before reinstalling.' }
New-Item -ItemType Directory -Force -Path $watcomDownloads | Out-Null
curl.exe --fail --location --silent --show-error `
  'https://github.com/open-watcom/open-watcom-v2/releases/download/2026-09-01-Build/open-watcom-2_0-c-win-x64.exe' `
  --output $watcomArchive
if ($LASTEXITCODE -ne 0) { throw 'Watcom download failed.' }
if ((Get-Item -LiteralPath $watcomArchive).Length -ne 128112046) { throw 'Wrong archive size.' }
if ((Get-FileHash -LiteralPath $watcomArchive -Algorithm SHA256).Hash -ne `
    '542a7edda4e58349d72a678704f20a013e1c46e19c915186395401df1b7faa1e') {
    throw 'Watcom archive checksum mismatch.'
}
```

Extract only after those checks pass:

```powershell
@'
from pathlib import Path
import zipfile

archive = Path('C:/tools/open-watcom/downloads/open-watcom-2026-09-01-win-x64.exe')
root = Path('C:/tools/open-watcom/2026-09-01').resolve()
if root.exists():
    raise SystemExit('Installation directory already exists.')
with zipfile.ZipFile(archive) as package:
    for entry in package.infolist():
        if not (root / entry.filename).resolve().is_relative_to(root):
            raise SystemExit('Archive entry escapes the installation directory.')
    package.extractall(root)
'@ | python -B -
if ($LASTEXITCODE -ne 0) { throw 'Watcom extraction failed.' }
```

## Activate for a build

Create the same activation script used by the current installation:

```powershell
@'
$env:WATCOM = $PSScriptRoot
$env:PATH = "$env:WATCOM\binnt64;$env:WATCOM\binnt;$env:PATH"
$env:INCLUDE = "$env:WATCOM\h;$env:WATCOM\h\win"
$env:WINDOWS_INCLUDE = $env:INCLUDE
'@ | Set-Content -LiteralPath C:\tools\open-watcom\2026-09-01\activate.ps1

. C:\tools\open-watcom\2026-09-01\activate.ps1
```

Dot-sourcing configures only the current PowerShell process and its children.
Use a dedicated build shell because INCLUDE is selected for the Win16 target.
`binnt64` contains the modern-host executables; `h\win` contains Windows headers,
and `lib286` contains 16-bit libraries. Keep the distribution's linker
configuration files beside its tools.

- `wcc`: 16-bit C compiler.
- `wasm`: assembler.
- `wlink`: linker; select the Windows DLL/NE target, not the 32-bit WIN95 target.
- The distribution includes DLL startup libraries; inspect resulting imports
  rather than assuming that a tiny C function produces an import-free DLL.

## Verification already performed

The unmodified vendor example `samples\dll\dll16.c` and its `dll16.lnk` were
copied to `verification` under the toolchain root. To reproduce that build:

```powershell
. C:\tools\open-watcom\2026-09-01\activate.ps1
$watcomCheck = Join-Path $env:WATCOM 'verification'
New-Item -ItemType Directory -Force -Path $watcomCheck | Out-Null
Copy-Item -LiteralPath "$env:WATCOM\samples\dll\dll16.c","$env:WATCOM\samples\dll\dll16.lnk" -Destination $watcomCheck
Push-Location $watcomCheck
try {
    wcc dll16 /mc /zu /zc /bd /bt=windows /d2
    if ($LASTEXITCODE -ne 0) { throw 'Win16 C compilation failed.' }
    wlink '@dll16.lnk'
    if ($LASTEXITCODE -ne 0) { throw 'Win16 DLL linking failed.' }
} finally {
    Pop-Location
}
```

Use the explicit `.lnk` extension and quote the `@` argument in PowerShell.
The sample compiled with three NULL-to-window-handle type warnings and no
errors, linked successfully, and produced an 11,895-byte DLL. Inspection
confirmed MZ/NE signatures, Windows target OS, and the library flag. The file
has not been executed under Windows 95 or our emulator. These checks establish
build-tool availability, not execution compatibility or reproducible output
bytes across builds.

## Current branch milestone

`codex/watcom-win16-test-library` starts from merged main at `f3e903c`.
The branch's intended PR milestone is a small Win16 DLL built with this
toolchain from inspectable project-owned source and usable by automated tests.
The [Hello42 fixture](../tests/fixtures/win16/hello42/README.md) now supplies
project-owned C source, conventional DLL initialization and exit routines, a
`HELLOWORLD` export returning 42, a build script, and opt-in metadata tests.
Build it with `./tools/build-win16-fixture.ps1`; run the combined suite with
`dotnet test -p:BuildWin16Fixture=true`. Its generated DLL has been inspected,
but has not yet been loaded or executed.

Keep the fixture's code and build instructions in the repository, with generated
objects/DLLs/maps in ignored `artifacts/`. Preserve the existing parser tests
that use generated metadata bytes and require no compiler. Record any linked
runtime/import requirements when the compiler-built fixture is introduced.
The installed toolchain, downloads, and vendor verification outputs stay outside
the repository. A full NE loader or original AD execution is a separate milestone.
