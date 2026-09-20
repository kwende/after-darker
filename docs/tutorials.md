# After Darker tutorials

One C# console application hosts small lessons through `ITutorial`. Each lesson
is an inspectable experiment: prediction, execution, observation, and a stated
proof boundary. Work through one together before implementing the next.

For the distinction between memory backing, a Win16 handle and an address,
start with [Tutorial 10: the local heap](win16-local-heap.md). It makes actual
guest allocation calls, writes through the returned near pointer, frees and
reuses storage, and verifies explicit zero-initialization. Use F5 profile
**Tutorial 10 - local heap and guest writes**, or:

```powershell
dotnet run --project src/AfterDarker.Tutorials --no-launch-profile -- 10
```

This lesson uses the same heap and gateway as Lasers, with a tiny source-authored
guest program and no proprietary input. The earlier lessons remain unchanged.

## Run tutorial 01

For inspecting an original local `.AD` file without executing it, jump to
[tutorial 05](#tutorial-05-read-a-windows-ne-file).

Prerequisites: Windows x64, .NET 10 SDK, and Visual Studio with .NET 10 support
and the **Desktop development with C++** tools (for Microsoft's `editbin`).
These were already installed on the development machine. Windows PowerShell
and Windows `tar.exe` handle the native dependency restore.

1. Open `AfterDarker.sln` in Visual Studio.
2. Select `AfterDarker.Tutorials` as the startup project. The solution also has
   a test runner executable; the tutorial project remains the F5 learning tool.
3. Press **F5** using the `Tutorial 01 - 16-bit addition` launch profile.

The program exits without waiting for keyboard input. Visual Studio may keep
its debug console open afterward according to the IDE's settings. Put a
breakpoint on `RegRead` in `Tutorial01Addition.Execute` to inspect the host code.
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
The stack allocation and register initialization are directly in `Execute()`;
`Run()` invokes it, checks the lesson's expectations, and prints success.
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

## Tutorial 04: host gateway

Select **Tutorial 04 - host gateway** and press F5, or run:

```powershell
dotnet run --project src/AfterDarker.Tutorials --launch-profile "Tutorial 04 - host gateway"
```

Read [`Tutorial04HostGateway.cs`](../src/AfterDarker.Tutorials/Lessons/Tutorial04HostGateway.cs).
It repeats tutorial 03's memory/register setup so the mechanism remains visible.
Both lessons use the tested `SegmentDescriptor16.Encode` helper for the eight
descriptor bytes. The callee address now names a synthetic C# service:

```text
x86 pushes two arguments and executes CALL FAR 0010:0200
    -> Unicorn's code hook records the address and calls EmuStop
    -> EmuStart returns to C#
    -> C# looks up Tutorial!HostAdd and reads its arguments from the guest stack
    -> the handler computes the result; C# writes AX and simulates RETF 4
    -> EmuStart resumes after CALL FAR
    -> x86 stores AX in data memory
    -> C# reads that memory to verify the result
```

The hook watches the reserved gateway segment. It reports linear `0x20200`;
the binding lookup uses the guest's `CS:IP=0010:0200`. This is our chosen
address and hook, not a built-in x86 trap or an operating-system service.
Host argument decoding and service execution happen **after the hook returns**.

Both calls use far Pascal argument order and callee cleanup. At each gateway
stop, `SS=0020`, `SP=0FF8`, and the eight-byte frame is:

| Relative to SP | Meaning | First call | Second call |
| --- | --- | --- | --- |
| `+0` | Saved return IP | `000B` | `0019` |
| `+2` | Saved return CS | `0008` | `0008` |
| `+4` | Last argument pushed: right | `5` | `5` |
| `+6` | First argument pushed: left | `7` | `-7` |

C# reads the frame after checking its bounds against the known stack page.
The byte decoding and cleanup calculation are now shared through
[`FarPascalWordFrame`](../src/AfterDarker.Core/Win16/FarPascalWordFrame.cs),
with independent unit tests for order, signedness, and invalid frames.
It restores the saved CS:IP and advances SP by eight: four bytes for the far
return address plus four for the arguments. This simulates `RETF 4`; tutorial
04 contains no guest `RETF` instruction. The next `EmuStart` begins at the saved
IP offset, with the caller's descriptor loaded back into CS.

The second call repeats the whole boundary on the same engine and checks signed
16-bit marshaling. Guest stores must replace `0xCCCC` markers with `12`
(`0x000C`) at `0018:0020` and `-2` (`0xFFFE`) at `0018:0022`. C# supplies AX but
does not write those result locations. Completion requires two host calls,
`CS:IP=0008:001C`, and `SP=1000`, with DS/SS and protected mode preserved.

A guard instruction at the gateway would change AX if allowed to execute;
the lesson checks that it has not. Temporary negative probes also confirmed
that an unknown entry, an incorrect argument offset, omitted argument cleanup,
and a missing guest store each fail with exit code 1. The source was restored
after each probe. All runs have instruction and time limits.

This proves the two-call synthetic gateway path and this fixed, same-privilege
ABI. It does not add NE import resolution, real Win16 services, a general
pointer translator, or reverse callbacks. Segment protection remains a
separate proof boundary.

## Shared runner

The tutorials remain first-class examples alongside [the automated tests](testing.md).
Each lesson now has two entry points:

- `Run()` preserves the narrated console lesson and its success checks.
- `Execute(...)` performs the same emulation and returns a typed `Result` of
  actual observations. Tests assert these values directly. With no writer
  supplied, it is quiet; `Run()` supplies `Console.Out`.

Guest byte arrays, memory maps, registers, hooks, and return simulation remain
in the lesson classes. Only the descriptor encoder and far Pascal word-frame
decoder have moved to `AfterDarker.Core`. This keeps tests and examples on the
same implementation without turning the lessons into calls to an opaque runner.

Tutorial 01 accepts alternative word operands. Tutorial 04 accepts an optional
typed host handler so tests can distinguish argument order from arithmetic and
verify values other than the lesson's fixed sums. The default F5 behavior is
unchanged. `Execute()` returns observations rather than declaring final success;
`Run()` and the test methods each verify their own expectations. Checks needed
to safely dispatch the gateway still execute inside `Execute()`.

[`ITutorial`](../src/AfterDarker.Tutorials/ITutorial.cs) exposes `Id`, `Title`,
and `Run()`. [`Program.cs`](../src/AfterDarker.Tutorials/Program.cs) registers
lesson instances explicitly and invokes the selected instance through that
interface. There is no reflection or plugin-loading machinery to learn first.

Use `--list` to list lessons, or pass an ID such as `01`, `02`, `03`, or `04`:

```powershell
dotnet run --project src/AfterDarker.Tutorials --no-launch-profile -- --list
dotnet run --project src/AfterDarker.Tutorials --no-launch-profile -- 01
dotnet run --project src/AfterDarker.Tutorials --no-launch-profile -- 02
dotnet run --project src/AfterDarker.Tutorials --no-launch-profile -- 03
dotnet run --project src/AfterDarker.Tutorials --no-launch-profile -- 04
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
The script restricts targets to the tutorial and test executables inside their
respective `bin/` directories. This opts those processes out of CFG; it does not change machine-wide security
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

Tutorial 04 was built and run through its launch profile on the same Windows
x64 host with .NET SDK 10.0.401 and Unicorn 2.1.3. Both guest stores, both host
exits/resumes, and final registers pass. Four temporary negative probes each
exit 1; the restored lesson and regression runs of tutorials 01–03 exit 0.

## Tutorial 05: read a Windows NE file

Select **Tutorial 05 - NE file inspection** in Visual Studio and press F5.
Enter the full path to a local `.AD` file when prompted. Quoted paths and paths
with spaces are accepted. It displays the report and exits. Alternatively:

```powershell
dotnet run --project src/AfterDarker.Tutorials --no-launch-profile -- 05 "C:\repos\after-darker\ad\Mondrian.ad"
```

Relative paths are resolved against the process's working directory. Other
file extensions are accepted if their bytes identify a Windows NE executable;
compressed `.A$` files, PE files, and non-Windows NE variants are not supported.
Missing/unreadable files or malformed metadata produce a diagnostic and exit 1.
Tutorial 01 remains the default when no lesson is selected.

Start reading at
[`Tutorial05NeInspection`](../src/AfterDarker.Tutorials/Lessons/Tutorial05NeInspection.cs).
Its `Execute(path)` returns a `NeImage`, then `Run()` passes that value to
`NeInspectionReport.Write`. Put a breakpoint after `Execute` to inspect the
records. The lesson creates no Unicorn engine. The shared app's existing build
prerequisites still apply because it also hosts the CPU tutorials.

### Typed results

The parser is in [`NeReader`](../src/AfterDarker.Core/Ne/NeReader.cs), independent
of file-system paths when called with bytes, and independent of the emulator,
After Dark semantics, and console presentation. Its public result types are
in [`NeImage`](../src/AfterDarker.Core/Ne/NeImage.cs):

| Type / property | What a consumer receives |
| --- | --- |
| `NeHeader` | NE location, flags, OS/version, automatic data segment, heap/stack requests, startup segment/offset, initial stack fields, alignment |
| `NeSegment` | Segment number, nullable disk offset, stored byte count, minimum allocation, flags and code/data classification |
| `NeEntry` and `NeName` | Ordinals, fixed/movable addresses or constants, entry flags, resident/nonresident names; ordinal-zero module descriptions stay separate |
| `NeImport` / `NeImage.Imports` | Distinct module-plus-ordinal or module-plus-name identities, without inventing names missing from the file |
| `NeRelocation` | Each raw fixup record, its disk position, source head, flags/type, raw target fields, and optional import identity |
| `NeResource` | Numeric or named type and ID, aligned stored byte range, and flags |
| `NeAddress` | A segment number and offset; explicitly not a selector or flat file address |

Collections returned by the parser are read-only. Imported identities contain
either an ordinal or a name. Resource identifiers contain either a number or a
name. Entries contain either an address or a constant value. These distinctions
are preserved rather than reduced to formatted strings.

The separate
[`AfterDarkCallPlan`](../src/AfterDarker.Core/Win16/AfterDarkCallPlan.cs) describes
the candidate SDK lifecycle in typed form:

```csharp
NeImage image = NeReader.ReadFile(path);
NeAddress? startup = image.Header.Startup;
NeEntry? module = image.FindExport("MODULE");
AfterDarkCallPlan? plan = AfterDarkCallPlan.FromImage(image);

// These are values to inspect/pass to later code, not calls into the guest.
foreach (NeResource resource in image.Resources)
{
    NeResourceIdentifier type = resource.Type;
    NeResourceIdentifier id = resource.Id;
    int fileOffset = resource.FileOffset;
    int storedLength = resource.Length;
}
```

`AfterDarkCallPlan.FromImage` returns null unless a library exports `MODULE`
into a code segment. It does not guess that ordinal 1 is always the dispatcher.
Each `AfterDarkInvocation` holds an enum message, resolved entry address, and
whether it is the repeated frame request. DLL startup is a separate address.
The plan expresses a known external SDK contract, not a signature discovered
inside NE or proof that this module implements it correctly.

### Reading the report

The report separates file facts from reference annotations and the proposed
After Dark calling contract. On the inspected Mondrian input it reports:

```text
Header startup: S4:0000
MODULE -> S1:003E
17 distinct imports; 44 total relocation records (27 internal, 0 OS fixups).
PREINITIALIZE: enter S1:003E, message=12
INITIALIZE: enter S1:003E, message=0
BLANK: enter S1:003E, message=1
DRAWFRAME (repeat): enter S1:003E, message=2
CLOSE: enter S1:003E, message=3
```

The report explains the far Pascal argument order, entry stack layout, AX
return, and argument cleanup. It also states the required loader/data/stack
preconditions and avoids claiming generic NE metadata supplies full Win16 ABIs.
An imported ordinal's address cannot be obtained from the caller's file alone;
the eventual loader must resolve a dependency export or assign a host gateway.

Names for known imported ordinals are display-only annotations drawn from the
existing [Wine 10.0 import census](research/ad-import-census.md). The small
embedded TSV contains only module/ordinal/name/kind facts derived from
`docs/research/ad-imports.csv`; unresolved ordinals stay unresolved. It is not a
new package dependency or callable service registry. A reference declaration
such as `pascal`, `stub`, or `equate` does not establish our support or a complete
ABI. Named imports remain the names actually present in the input.

Resource types/IDs are interpreted as numbers when the high bit is set and as
resource-table-relative counted strings otherwise. Resource payload offsets
and lengths use the resource table's own alignment shift. Mondrian has seven
custom resource entries (types 1000 and 2000); the report does not assign them
animation or settings semantics. Standard type labels include bitmap, icon,
dialog, string table, and raw data. Individual strings, images, and custom
payloads are not decoded; stored lengths can include alignment padding.

### Parser boundaries and verification

The reader checks signatures, ranges, table terminators, entry segment/ordinal
references, import module indices, and resource payload ranges. It supports
fixed/movable/constant entries, ordinal holes, zero-filled segments, the 64 KiB
segment-size encoding, and ordinal/named imports. Iterated segments are rejected
explicitly. Inputs are limited to 64 MiB and 100,000 metadata items.

Raw relocation records are preserved, including internal references and OS
fixups. This lesson does not validate/expand relocation chains, resolve all
internal fixup targets, apply patches, decode instructions, decode resource
payloads, construct SDK records, load executable memory, or run original code.
Parsing success is not loader compatibility or a comprehensive NE validator.

Generated test fixtures cover both ordinary and malformed metadata. Automated
checks also exercise the tutorial's prompted and explicit-path entry points.
All 29 private local modules were inspected successfully; those files and their
full reports remain ignored and are never unit-test dependencies. The launch
profile was exercised via `dotnet run`; interactive Visual Studio F5 remains a
manual check.

Format references: Microsoft's Windows 3.1 Programmer's Reference, Volume 4,
[Chapter 6: Executable-File Header Format](https://bitsavers.trailing-edge.com/pdf/microsoft/windows_3.1/Windows_3.1_Programmers_Reference_Volume_4_Resources_1992.pdf),
Wine 10.0's [NE header definitions](https://github.com/wine-mirror/wine/blob/wine-10.0/include/winnt.h),
and [NE table inspection](https://github.com/wine-mirror/wine/blob/wine-10.0/dlls/krnl386.exe16/ne_module.c).
The implementation is local C# code; Wine is a layout/reference source, not a
linked or vendored runtime dependency.

## Tutorial 06: load and execute a Win16 library

This lesson has its own [detailed walkthrough](tutorial-06-load-library.md),
covering raw-byte loading, selector assignment, import and export-prologue
patches, every startup register, the guest far-call stack, host exits, and the
guest-written return value. It uses the same console app and `ITutorial`
registration as the earlier lessons.

```powershell
./tools/build-win16-fixture.ps1
dotnet run --project src/AfterDarker.Tutorials --launch-profile "Tutorial 06 - load Win16 library"
dotnet run --project src/AfterDarker.Tutorials --launch-profile "Tutorial 06 - instruction trace"
```

The project-owned DLL's startup returns 1, `HELLOWORLD` returns 42 and the
caller stores it, then `WEP(1)` returns 1 with the stack restored. The host's
LocalInit response is a checked test double, not an implemented Windows heap.
GetVersion is deterministic; MessageBox fails by name if reached. No AD module
executes in this lesson. See the walkthrough for tests and remaining boundaries.

## Tutorial 07: reconnect references without executing the module

The [relocation walkthrough](tutorial-07-relocations.md) explains the two-pass
loader, fixed versus entry-ordinal targets, chain links, field widths, typed
patch records, and the distinction between an import address and its implementation.

```powershell
dotnet run --project src/AfterDarker.Tutorials --no-launch-profile -- 07 --step
dotnet run --project src/AfterDarker.Tutorials --no-launch-profile -- 07 ad/Mondrian.ad --step
```

The first command uses original generated NE metadata and works without private
files or Watcom. The second uses a local module. Enter advances after each
patch; `q` cancels. F5 profiles are **Tutorial 07 - relocation walkthrough**
and **Tutorial 07 - local NE relocations** (prompts for a path). Omit `--step`
for continuous output. Both paths call the same shared Core loader; no CPU is
created and no Windows service is invoked.

## Tutorial 08: initialize the original Mondrian module

Select **Tutorial 08 - initialize local Mondrian** for a prompted path, or
**Tutorial 08 - initialization trace** to see instruction bytes and registers.

```powershell
dotnet run --project src/AfterDarker.Tutorials --no-launch-profile -- 08 ad/Mondrian.ad
```

The [complete walkthrough](tutorial-08-mondrian-initialize.md) follows prepared
segments into Unicorn, the startup register inputs, backed guest handles/records,
and imported-call versus DOS-interrupt return paths. Original DLL startup,
PREINITIALIZE, and INITIALIZE run and return; the lesson checks guest state and
stops before drawing. This version-specific lesson requires the analyzed local
module, but does not require Watcom. The source file remains unchanged.


## Tutorial 09: capture original Mondrian frames

Select **Tutorial 09 - capture Mondrian PNG frames** and supply your local module
path, or run `dotnet run --project src/AfterDarker.Tutorials --no-launch-profile -- 09 ad/Mondrian.ad`.
The optional final argument selects the number of changed images (default 30).
The [drawing walkthrough](tutorial-09-mondrian-frames.md) follows original code
through the four additional APIs into a persistent software surface, then PNG
capture. Each run writes an ignored output directory with a play/step viewer and
JSON evidence. Synthetic clocks and presentation cadence are explicit host choices.
