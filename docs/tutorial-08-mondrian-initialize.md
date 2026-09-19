# Tutorial 08: initialize original Mondrian code

This lesson runs the original, locally supplied Mondrian module through DLL
startup, `MODULE(PREINITIALIZE, ...)`, and `MODULE(INITIALIZE, ...)`, then exits.
The guest reads real memory behind the handles we supply and writes its own
initial state. We inspect that state and its return frames. Tutorial 09 continues into drawing; this lesson still stops here.

## Run it

Select **Tutorial 08 - initialize local Mondrian** in Visual Studio and press
F5. Enter the path to your local `Mondrian.ad`. For instruction bytes and
registers, select **Tutorial 08 - initialization trace** instead.

```powershell
dotnet run --project src/AfterDarker.Tutorials --no-launch-profile -- 08 ad/Mondrian.ad
dotnet run --project src/AfterDarker.Tutorials --no-launch-profile -- 08 ad/Mondrian.ad --trace
```

No Watcom build is needed for this lesson. The normal Unicorn restoration and
apphost configuration still apply. Only the binary analyzed in
[the research report](research/mondrian-static-analysis.md) is accepted:

```text
SHA-256 781979da1a6a6fdf99eebec4dab67e7a645bfc8787be1671e20f13a8ca6b1aed
```

That restriction matters: our host records and guest-state observations come
from this artifact's field accesses, not a complete recovered SDK schema.
An unknown revision fails before CPU creation. Original files remain read-only
and ignored by Git; the test/build process never copies them into output.

## Reading order and breakpoints

Start at [MondrianSession](../src/AfterDarker.Runtime/MondrianSession.cs),
the execution path shared by tutorials 08 and 09. Tutorial 08 constructs an
initialization-only session, calls `Initialize`, snapshots the result, and
disposes it. See [session lifetime](mondrian-session.md). The stages remain visible:

1. `NeLoadPlan.CreateWithImportResolver`: prepare and relocate segment arrays.
2. `guest.Map`: copy the prepared arrays into Unicorn and build descriptors.
3. `CreateRecords` and `Blocks.Register`: allocate guest data before publishing handles.
4. The register assignments: establish the startup ABI and our caller's stack.
5. `RunPhase`: enter the guest and verify each completed return before continuing.
6. `DispatchImport`: read an actual far Pascal frame and return a service result.
7. `MondrianInitialization.Observe`: read state produced by the original code.

Follow `guest.RunUntil` into
[SegmentedGuest](../src/AfterDarker.Runtime/SegmentedGuest.cs) to see the
execution loop and the two hook boundaries. This helper owns the native CPU;
it has no knowledge of After Dark records or Windows services.

For the implementations alone, start at
[Win16Api](../src/AfterDarker.Core/Win16/Win16Api.cs). It exposes ordinary named
C# methods, shared by tutorials 06 and 08; no stack decoding, import ordinals,
or register writes appear there. [Win16ApiState](../src/AfterDarker.Core/Win16/Win16ApiState.cs)
holds one guest's memory, handles, heap reservation, version, and clock inputs.
[Win16Imports](../src/AfterDarker.Core/Win16/Win16Imports.cs) separately owns
the import table and argument/result conversion. The API's memory interface
accepts guest far pointers, never native host pointers. The named
Mondrian field offsets and input records are in
[MondrianInitialization](../src/AfterDarker.Core/AfterDark/MondrianInitialization.cs).

## Bytes first, execution later

```text
NE file
  -> parse metadata
  -> assign segment selectors/bases
  -> copy and relocate C# arrays
  -> map guest memory and copy prepared arrays into Unicorn
  -> install the segment descriptor table
  -> set startup registers
  -> execute a small host-authored CALL FAR into the original DLL
```

`MemWrite` does not execute code. Once `EmuStart` runs, Unicorn fetches the bytes
at CS:IP, decodes instructions, and follows the DLL's branches and calls.
Our tiny caller is machine code too: it performs the far call, then stores AX
in host-provided **guest** memory after the DLL returns.

The five file segments use selectors `0008` through `0028`. Runtime locations:

| Role | Selector | Linear base | Contents |
| --- | --- | --- | --- |
| File S1 | `0008` | `10000` | MODULE dispatcher and library functions |
| File S2 | `0010` | `20000` | Module lifecycle helpers |
| File S3 | `0018` | `30000` | Compiled runtime helpers |
| File S4 | `0020` | `40000` | DLL startup |
| File S5 | `0028` | `50000` | Automatic data plus reserved heap tail |
| Caller | `0030` | `60000` | Tiny far-call programs (08 uses the first three) |
| Gateway | `0038` | `70000` | Synthetic import addresses; hooks stop before execution |
| Stack | `0040` | `80000` | Caller-supplied stack, initially SP=`1000` |
| Host data | `0048` | `90000` | System/module records, empty environment, result slots |

The bases and selectors are our allocation choices. `SegmentedGuest.Map`
creates page-aligned memory but describes the actual allocation length in each
16-bit segment descriptor. Host pointer translation checks the recorded length;
this is not a new claim that all x86 protection behavior has been proven.

## A handle must lead to real guest memory

This is the distinction that prevents a placeholder return from becoming a bad
dereference in the guest:

```text
MODULE receives system HANDLE 0101
    |
    +-- GlobalLock(0101) returns DX:AX = 0048:0100
    |       |
    |       +-- guest reads WORD at system record +20h: module HANDLE 0102
    |
    +-- GlobalLock(0102) returns DX:AX = 0048:0200
            |
            +-- guest reads options from this module record
```

`0101` and `0102` are lookup keys. `0048:0100` and `0048:0200` are addresses with
allocated backing memory. GlobalLock also returns the selector in CX to match
the Win16 entry's behavior. Each lock is counted, each unlock releases one, and
the lesson requires zero outstanding locks after each phase. The final unlock
returns zero because no locks remain; this is not failure.

The two records occupy separate ranges of host data. Their named offsets encode
the fields observed in the research report: compatibility checks in the system
record; positive width/height, speed, and clear-background options in the module
record. Compatibility value `0048` at system offset `34h` happens to equal our
chosen host-data selector; those are independent values with different roles.

An unknown handle throws a symbolic managed error before a zero/invalid pointer
can be returned to the guest. This is our strict unsupported-input policy, not
a general implementation of every Windows invalid-handle return convention.

## Registers and the three calls

DLL startup begins at the NE header entry, `S4:0000`, which resolves to
`0020:0000`. Its compiler startup receives:

| Register | Input | Why |
| --- | --- | --- |
| DS | `0028` | DLL automatic data segment |
| DI | `0028` | Instance identity saved by this DLL |
| CX | `0400` | Header's 1,024-byte local heap request |
| ES:SI | `0000:0000` | No command line; this startup does not dereference it |
| SS:SP | `0040:1000` | Valid caller-owned stack |
| BP | `0000` | Initial frame-chain sentinel |

CR0.PE is enabled; the descriptors' D/B bits are clear for 16-bit execution.
The direction flag starts clear. The caller's CALL FAR supplies the return
CS:IP on the stack; setting startup CS:IP alone would not create that frame.

Startup calls LocalInit, then an internal library routine through a relocated
far call. We require a nonzero startup result and the saved instance identity
before invoking MODULE.

For the exported MODULE calls, caller DS becomes `0048`. The export's patched
prologue establishes DLL DS and later restores caller DS. Arguments are:

```c
MODULE(12 /* PREINITIALIZE */, 0x0103 /* reserved HDC */, 0x0101 /* system handle */);
MODULE( 0 /* INITIALIZE    */, 0x0103 /* reserved HDC */, 0x0101 /* system handle */);
```

Pascal pushes the three WORDs left-to-right. The guest's `RETF 6` removes them
as well as its four-byte return address. Our caller stores AX after return.
Each phase must return to the intended caller address, with SP=`1000`, SS=`0040`,
BP=0, restored DS, and a guest-written result matching AX.

PREINITIALIZE sets the compatibility flag to 1. Its AX is incidental here:
the empty helper returns its prologue's value, observed as `0028`. We do not
mistake that for an SDK success code. INITIALIZE consumes the options, obtains
time, seeds its own RNG, and initializes timing and rectangle state.

## Services actually supplied

| Service | Behavior and limit |
| --- | --- |
| KERNEL LocalInit | Validate and record the exact zero-filled 1,024-byte reservation after static data. No LocalAlloc/LocalFree or Windows heap metadata is implemented. |
| KERNEL GlobalLock | Resolve registered, resident host blocks to checked guest pointers; track locks; return DX:AX and selector in CX. |
| KERNEL GlobalUnlock | Release a matching lock and return remaining count; reject unknown/unbalanced use. |
| KERNEL GetDOSEnvironment | Return `0048:0300`, a mapped, double-NUL-terminated empty environment. |
| USER GetTickCount | Return a deterministic 32-bit tick, beginning at `12345678`, increasing by 16 per request. |
| DOS INT 21h / AH=2Ah | Return the fixed civil date, including weekday. |
| DOS INT 21h / AH=2Ch | Return the matching fixed civil time. |

LocalInit is deliberately a **checked reservation model**, not a Windows heap
allocator. Static analysis found no local allocation/free or heap-bookkeeping
reads on this path; execution of this artifact agrees. The global records have
separate real storage and a handle registry. Their pointer validity does not
depend on pretending an unimplemented allocator succeeded.

The other twelve imports have named gateway addresses but no implemented ABI
or successful return. If reached, they stop the lesson before argument decoding.
This includes all drawing operations. HDC `0103` is reserved identity only;
there is no device context or pixel surface to dereference yet.

The civil clock is fixed at 1993-06-15 12:34:56. The empty environment selects
the module runtime's built-in timezone conversion; it does not select UTC.
Both clock choices and supplied options are modern deterministic inputs.

## Why INT does not return like CALL FAR

```text
Imported CALL FAR                      INT 21h
  guest pushes return CS:IP              Unicorn recognizes software INT
  hook stops at gateway                  interrupt hook stops execution
  EmuStart returns to C#                 EmuStart returns to C#
  read Pascal arguments                  read AH; supply date/time registers
  write result registers                 IP already points after INT
  simulate RETF + argument cleanup        no interrupt frame to remove
  resume at restored CS:IP                resume at unchanged current CS:IP
```

That right-hand behavior is a tested Unicorn interception boundary, not a claim
that hardware INT never creates a frame. The synthetic protected-mode test
checks IP advancement, unchanged SS:SP and flags, and guest stores after resume.
The runtime checks these invariants again when Mondrian issues an interrupt.
CPU exceptions are rejected rather than dispatched as DOS services.

## Observed result and proof boundary

On the analyzed module, three phases return successfully, with **11 imported
service calls** and **three DOS interrupts** (date, time, date). Default inputs
produce compatibility=1, threshold=30, clear=1, rectangle count=0, guest-converted
time=`2C1E2460`, RNG seed=`00002460`, and tick=`12345678`. Both guest pointers
match the host records and all locks are released. Changing supplied speed
across 0/25/50/75/100 makes the original code select thresholds 140/70/30/0/0.

This proves original Mondrian **initialization**, including original internal
calls, imported calls, DOS clock requests, and guest memory consumption. It does
not prove BLANK, DRAWFRAME, CLOSE, WEP, drawing fidelity, a Windows heap allocator,
other module revisions, or general Win16 compatibility. Disposal releases our
emulated machine; no guest shutdown lifecycle is invoked in this lesson.

## Tests and sources

`dotnet test` runs self-contained layout, handle lifetime, clock packing, invalid
input, instruction-budget, and CPU/gateway tests. The synthetic import program
uses the **same DispatchImport** as the lesson, checks every supported import's
arguments/results/cleanup, and dereferences the returned pointer in guest code.
Optional local-module tests check the lifecycle and all five speed options:

```powershell
$env:AFTER_DARKER_MONDRIAN = (Resolve-Path ad/Mondrian.ad).Path
dotnet test -p:TestLocalMondrian=true
# Include the existing compiler-built regression fixture too:
dotnet test -p:TestLocalMondrian=true -p:BuildWin16Fixture=true
```

The default suite never reads private files. See [testing](testing.md) for counts.
CLI execution and prompted/trace paths are tested; interactive Visual Studio
F5/debugger use remains a manual check.

ABI and behavior references:

- [Wine 10.0 KERNEL declarations](https://github.com/wine-mirror/wine/blob/wine-10.0/dlls/krnl386.exe16/krnl386.exe16.spec)
  and [USER declarations](https://github.com/wine-mirror/wine/blob/wine-10.0/dlls/user.exe16/user.exe16.spec).
- [Wine Win16 GlobalLock/GlobalUnlock](https://github.com/wine-mirror/wine/blob/wine-10.0/dlls/krnl386.exe16/global.c#L419), including CX and remaining-lock behavior.
- Microsoft MS-DOS **4.0**, revision `2d04cacc5322951f187bb17e017c12920ac8ebe2`,
  `v4.0/src/DOS/TIME.ASM`, `$GET_DATE` and `$GET_TIME`; paired with the documented
  AH=2Ah/2Ch contracts in [our DOS source reference](research/dos-source-reference.md).

These are behavior references, not vendored implementations or new dependencies.
