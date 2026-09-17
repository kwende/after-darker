# Hello42: a compiler-built Win16 DLL

This is project-owned C source for a tiny Windows 3.x NE library. It gives the
parser and future loader a fixture produced by an independent, conventional
toolchain. No original After Dark files are needed.

| Entry | Contract | How it is reached |
| --- | --- | --- |
| `LibMain` | `BOOL FAR PASCAL (HINSTANCE, WORD, WORD, LPSTR)`; returns TRUE | Watcom startup stub, addressed by the NE header |
| `HelloWorld` | `WORD FAR PASCAL (void)`; returns 42 in AX | Public `HELLOWORLD`, ordinal 1 |
| `WEP` | `int FAR PASCAL (int)`; returns 1 | Resident public `WEP`, ordinal 2 |

Watcom uppercases Pascal symbol names. `LibMain` is deliberately not a named
export: Windows starts at the header's entry address, and Watcom's stub performs
runtime initialization before calling it. `WEP` is the standard Windows Exit
Procedure. This fixture has no After Dark `MODULE` dispatcher.

## Build and inspect

From the repository root, with the [pinned toolchain](../../../../docs/watcom-toolchain.md):

```powershell
./tools/build-win16-fixture.ps1
dotnet run --project src/AfterDarker.Tutorials --no-launch-profile -- 05 artifacts/win16/hello42/hello42.dll
dotnet test -p:BuildWin16Fixture=true
```

The script writes `hello42.dll`, `hello42.obj`, and `hello42.map` under
`artifacts/win16/hello42/`. The directory is ignored; commit source and build
instructions rather than generated binaries. Build environment changes are
restored when the script returns. For the same installation at another path:

```powershell
./tools/build-win16-fixture.ps1 -WatcomRoot D:\tools\open-watcom\2026-09-01
dotnet test -p:BuildWin16Fixture=true -p:WatcomRoot=D:\tools\open-watcom\2026-09-01
```

Opting in builds a fresh DLL, copies it into the test output's `Fixtures`
directory, and includes 12 `Toolchain` tests (metadata and tutorial 06 execution).
A missing compiler or failed build fails that run. Ordinary `dotnet test` has
89 tests without requiring Watcom; the opt-in suite has 101. Do not use
`--no-build` when enabling or disabling the fixture tests.

## What the compiler adds

`/bt=windows /bd` selects Windows DLL code generation; `/mc` uses compact memory
model (near code, far data), while the public functions explicitly use FAR.
`/zu /zc` follows Watcom's Win16 DLL sample's stack/data and code-segment settings.
`/w4 /we` makes compiler warnings fail the build. The link selects Windows NE,
one automatic data segment, and Watcom's normal `libentry` startup. No resources
are included and no new managed package is required. The linked startup/runtime
comes from the pinned Open Watcom distribution under its Sybase Open Watcom
Public License (`license.txt` in the installation).

The inspected build has two segments, a 1,024-byte initial local heap request,
and three imported APIs:

- `KERNEL!3` (`GetVersion`): startup version initialization.
- `KERNEL!4` (`LocalInit`): local heap initialization.
- `USER!1` (`MessageBox`): linked runtime error reporting. The C fixture itself
  has no UI calls, but the compiler retains stack checking and its error path.

The link map identifies `__DLLstart_`, `LIBMAIN`, `HELLOWORLD`, `WEP`, stack
checking, and runtime error-reporting routines. In the inspected build, startup
is `S1:0000`, `HELLOWORLD` is `S1:00A0`, and `WEP` is `S1:00B7`. These are file
segment numbers, not selectors. Tests resolve entries through the parser and
check code-segment bounds instead of fixing these offsets in assertions.

## Proof boundary

Verified: warning-free compilation/linking, a 1,034-byte Windows NE DLL, typed
parsing, the expected exports/startup, and the exact import set. Inspection of
the linked `HELLOWORLD` bytes finds `B8 2A 00` (`MOV AX,42`) and a final `CB`
(`RETF`), alongside the compiler's prologue, stack check, and epilogue.

[Tutorial 06](../../../../docs/tutorial-06-load-library.md) now initializes and
executes this DLL in Unicorn: startup returns 1, HELLOWORLD returns/stores 42,
and WEP returns 1. It assigns code/data/stack descriptors, patches imports and
export prologues, and uses real guest far calls. Its LocalInit response is a
checked test double and GetVersion is fixed; it does not implement a Windows
heap. Execution under Windows 95 remains untested. Read the tutorial guide for
the exact register setup, trace, tests, and runtime limitations.
