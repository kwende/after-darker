# After Darker tutorials

One C# console application hosts small lessons through `ITutorial`. Each lesson
is an inspectable experiment: prediction, execution, observation, and a stated
proof boundary. Work through one together before implementing the next.

## Run tutorial 01

Prerequisites: Windows x64, .NET 10 SDK, and Visual Studio with .NET 10 support
and the **Desktop development with C++** tools (for Microsoft's `editbin`).
These were already installed on the development machine. Windows PowerShell
and Windows `tar.exe` handle the native dependency restore.

1. Open `AfterDarker.sln` in Visual Studio.
2. The only executable project is `AfterDarker.Tutorials`; select it as the
   startup project if Visual Studio asks.
3. Press **F5** using the `Tutorial 01 - 16-bit addition` launch profile.

The program exits without waiting for keyboard input. Visual Studio may keep
its debug console open afterward according to the IDE's settings. Put a
breakpoint on `RegRead` in `Tutorial01Addition.Run` to inspect the host code.
Stepping in C# steps the host, not individual guest instructions.

From the repository root, the equivalent command is:

```powershell
dotnet run --project src/AfterDarker.Tutorials
```

Expected output:

```text
Tutorial 01: Add two constants in 16-bit x86
Guest: MOV AX, 7; ADD AX, 5
AX = 12 (0x000C)
PASS: the guest computed 7 + 5 and reached the end of its code.
```

Success exits with code 0. An unexpected result, incomplete execution, or a
managed exception exits with code 1.

## Read tutorial 01

Start at
[`Tutorial01Addition.cs`](../src/AfterDarker.Tutorials/Lessons/Tutorial01Addition.cs).
The complete guest program is six bytes:

| Guest address | Bytes | Assembly | Effect |
| --- | --- | --- | --- |
| `0x1000` | `B8 07 00` | `MOV AX, 7` | Load 7 into the 16-bit accumulator |
| `0x1003` | `05 05 00` | `ADD AX, 5` | Add 5 to that accumulator |
| `0x1006` | No instruction | End address | Return control to C# before executing here |

The constants use little-endian encoding: the low byte precedes the high byte.
The byte array is hand-encoded, with its assembly beside it. No assembler or
executable-file parser is required for this two-instruction lesson.

```text
C# maps guest memory and copies the six bytes
    -> Unicorn executes MOV and ADD synchronously
    -> execution reaches the configured end address
    -> C# reads AX and IP
    -> C# verifies AX=12 and IP=0x1006, prints, and closes the engine
```

`UC_MODE_16` starts this probe in real mode. `CS=0` keeps the instruction offset
equal to the guest address. We have not configured protected-mode descriptors.
The one-second timeout and 16-instruction budget bound accidental runaway code;
the final `IP` check distinguishes completion from an early bounded stop.

This proves that our Windows C# host can load the native engine, execute this
16-bit addition, and read guest registers. It does not prove protected-mode
selectors, far calls, gateways, NE loading, or original After Dark execution.
There are no hooks, callbacks, suspension handlers, or Win16 services here.

## Tutorial 02: guest stack and near call

Select the **Tutorial 02 - stack and near call** Visual Studio launch profile
and press F5, or run:

```powershell
dotnet run --project src/AfterDarker.Tutorials --launch-profile "Tutorial 02 - stack and near call"
```

Read
[`Tutorial02StackCall.cs`](../src/AfterDarker.Tutorials/Lessons/Tutorial02StackCall.cs).
The stack allocation and register initialization are directly in `Run()`.
The lesson maps a separate read/write page at `0x8000..0x8FFF`, initializes
`SS=0` and `SP=0x9000`, and runs the entire guest sequence without callbacks.
The subroutine copies its in-call `SP` into `DX` for inspection afterward.

Verified state:

| Observation | Expected value |
| --- | --- |
| Addition result in `AX` | `12` |
| Initial `SP` | `0x9000` |
| In-call `SP`, captured in `DX` | `0x8FFE` |
| Final `SP` | `0x9000` |
| Saved return word at `0x8FFE` | `0x1006` |
| Final `CS:IP` | `0000:100E` |
| Final `SS` | `0x0000` |

The lesson checks every value above and exits with code 0 on success. It proves
this real-mode near-call stack round trip. It does not test protected-mode
descriptors, far calls, a host gateway, or stack overflow protection.

## Tutorial 03: protected-mode far call

Select **Tutorial 03 - protected-mode far call** and press F5, or run:

```powershell
dotnet run --project src/AfterDarker.Tutorials --launch-profile "Tutorial 03 - protected-mode far call"
```

Read [`Tutorial03FarCall.cs`](../src/AfterDarker.Tutorials/Lessons/Tutorial03FarCall.cs).
This is one same-privilege guest-to-guest far call. No host hooks or callbacks
are installed. The caller executes `MOV AX, 7`, `CALL FAR 0010:0200`, then
`MOV [DS:0020], AX`. The callee captures its CS/SP, adds five, and executes `RETF`.

The lesson builds this Global Descriptor Table (entry zero is null):

| Selector | Role | Base | Inclusive limit | Attributes |
| --- | --- | --- | --- | --- |
| `0008` | Caller code | `0x10000` | `0x0FFF` | 16-bit executable/readable |
| `0010` | Callee code | `0x20000` | `0x0FFF` | 16-bit executable/readable |
| `0018` | Result data | `0x30000` | `0x0FFF` | Read/write |
| `0020` | Stack | `0x40000` | `0x0FFF` | Read/write, 16-bit SP |

All descriptors are present, privilege level zero, byte-granular, and have
D/B=0. The descriptor table itself is mapped at `0x50000` and installed through
GDTR. The helper encoding GDTR uses Unicorn's native x64 `uc_x86_mmr` layout;
that API buffer is distinct from the guest's eight-byte segment descriptors.

**Engine setup detail:** Unicorn 2.1.3's `UC_MODE_16` initialization and segment
register writes assume real-mode addressing. This lesson opens `UC_MODE_32`,
which initializes protected mode, then loads 16-bit code/stack descriptors.
The descriptor's D/B bit determines the guest instruction/stack width; all
guest instructions here are 16-bit. `CR0.PE=1` is checked explicitly.

With this setup, `EmuStart` writes its start argument into EIP as an offset,
but checks the stop address against the linear execution address. The observed
working call uses start `0`, stop `0x1000B`. These are version-specific API
details to retain when building a future adapter, not a general claim that all
emulator addresses use one coordinate system.
References: [Unicorn x86 register handling](https://github.com/unicorn-engine/unicorn/blob/2.1.3/qemu/target/i386/unicorn.c),
[execution start](https://github.com/unicorn-engine/unicorn/blob/2.1.3/uc.c),
and [native register structure](https://github.com/unicorn-engine/unicorn/blob/2.1.3/include/unicorn/x86.h).

Success checks:

- Callee `CS=0010`, captured in `BX`, and in-call `SP=0FFC`, captured in `DX`.
- Saved IP `0008` at linear `0x40FFC` and saved CS `0008` at `0x40FFE`.
- Restored `SP=1000`, final `CS:IP=0008:000B`, `DS=0018`, and `SS=0020`.
- `AX=12` and the guest's store changed result memory at linear `0x30020`
  from `0xCCCC` to `12`. C# does not write that successful result.

A temporary negative experiment changed the single `RETF` opcode to `RET`.
The bounded run left CS in the callee segment, SP two bytes short of restoration,
and result memory unchanged. The assertions rejected it with exit code 1;
restoring `RETF` restored success. This establishes that checking AX alone would
have missed a broken return.

This proves the stated descriptor-base and far-call round trip. It does not
prove limit/access enforcement, privilege transitions, host trap handling,
host-managed return simulation, or After Dark compatibility.

## Shared runner

[`ITutorial`](../src/AfterDarker.Tutorials/ITutorial.cs) exposes `Id`, `Title`,
and `Run()`. [`Program.cs`](../src/AfterDarker.Tutorials/Program.cs) registers
lesson instances explicitly and invokes the selected instance through that
interface. There is no reflection or plugin-loading machinery to learn first.

Use `--list` to list lessons, or pass an ID such as `01`, `02`, or `03`:

```powershell
dotnet run --project src/AfterDarker.Tutorials --no-launch-profile -- --list
dotnet run --project src/AfterDarker.Tutorials --no-launch-profile -- 01
dotnet run --project src/AfterDarker.Tutorials --no-launch-profile -- 02
dotnet run --project src/AfterDarker.Tutorials --no-launch-profile -- 03
```

Add future lessons as separate implementation classes in the same application,
register them in `Program.cs`, and add launch profiles as needed. Each lesson
owns and closes its engine so experiments do not share guest state. The binding
version used here requires an explicit `Close()` to release the native engine;
its `Dispose()` only handles binding allocations.

## Dependencies and Windows setup

- **UnicornEngine.Unicorn 2.1.3**: the upstream .NET binding, restored by NuGet.
  It is implemented in F#, which accounts for its transitive **FSharp.Core
  6.0.7** dependency. Our application and tutorials are C#.
- **Unicorn 2.1.3 Windows MinGW x64 DLL**: the CPU engine. This NuGet version
  omits a Windows DLL, so `tools/restore-unicorn.ps1` downloads the matching
  [official release](https://github.com/unicorn-engine/unicorn/releases/tag/2.1.3),
  checks pinned archive and DLL SHA-256 values, and caches it under ignored
  `artifacts/`. The build copies it beside our executable as `unicorn.dll`.
- Upstream declares Unicorn under
  [GPLv2](https://github.com/unicorn-engine/unicorn/blob/2.1.3/COPYING);
  FSharp.Core declares MIT. The repository's own license remains undecided.
  Downloads and binaries remain local; this milestone does not publish a bundle.

The first build needs network access to NuGet and GitHub. Subsequent builds
reuse the cached dependencies. `packages.lock.json` records managed dependency
versions and hashes. Native hashes are recorded in the restore script.

**Observed compatibility requirement:** an otherwise successful .NET 10 build
terminated with `0xC0000409` inside native emulation. The Windows debugger
identified subcode `0xD`, `FAST_FAIL_INVALID_SET_OF_CONTEXT`, in the C runtime's
`longjmp` validation. Both upstream MSVC and MinGW 2.1.3 builds showed this
failure. Disabling CET alone did not fix it; disabling Control Flow Guard (CFG)
on the generated apphost did. The final project retains the SDK's default CET
setting and uses the MinGW DLL to avoid the MSVC release's debug-runtime dependency.

The build runs `tools/configure-tutorial-apphost.ps1`, which locates Microsoft's
`editbin` and applies `/GUARD:NO` only to this project's generated executable.
The script restricts the target to the tutorial's `bin/` directory. This opts
the tutorial process out of CFG; it does not change machine-wide security
settings or patch the shared .NET host. This is a tutorial compatibility
tradeoff to revisit before any broader runtime/distribution decision.
See [upstream's CFG report](https://github.com/unicorn-engine/unicorn/issues/2281).

Launch via F5, `dotnet run`, or the generated `.exe`. Running `dotnet
AfterDarker.Tutorials.dll` uses the shared .NET host, whose CFG setting the
project does not alter, and reproduced the native failure on this machine.

## Verification recorded for this milestone

The native dependency was downloaded and the console app was built and run on
Windows x64 with .NET SDK 10.0.302. The guest returned `AX=12`, reached
`IP=0x1006`, and the process exited with code 0. The Visual Studio launch profile
is included; an interactive Visual Studio F5 session has not been exercised by
the automated checks.

Tutorial 02 was subsequently built and run through its launch profile on the
same host. All listed stack, return-address, register, and completion checks
passed with exit code 0. Tutorial 01 still passes through the shared runner.

Tutorial 03 was built and run on Windows x64 with .NET SDK 10.0.401 and the
same pinned Unicorn 2.1.3 dependencies. Its normal run exits 0; the temporary
wrong-return probe exits 1. The launch profile is exercised by `dotnet run`;
interactive Visual Studio F5 remains a manual check.
