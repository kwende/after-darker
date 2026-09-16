# Mondrian: the original-code visuals path

Date: 2026-09-16. This is static artifact analysis, not an execution result.

## Finding and scope

The inspected Mondrian module is a strong first target for a constrained host.
Its normal DLL startup and `PREINITIALIZE -> INITIALIZE -> BLANK -> DRAWFRAME
-> CLOSE` path appears to require **nine Windows imports and two DOS clock
services**. The module generates rectangles and asks Windows to invert them.
It keeps its rectangle history in its own data segment.

No file access, thread creation, synchronization, task yielding, window
creation, message pump, dialog, helper DLL, or host-to-guest callback was found
on that path. This conclusion applies to this artifact and the paths described
here, not to the entire After Dark collection or arbitrary execution states.

The first milestone should use fixed host-supplied option values and an owned
render surface. Options UI, settings persistence, and desktop integration are
outside that milestone. The module's original drawing code still executes.

## Artifact and method

| Property | Observed value |
| --- | --- |
| Input | Local, private `Mondrian.ad` |
| Size | 8,704 bytes |
| SHA-256 | `781979da1a6a6fdf99eebec4dab67e7a645bfc8787be1671e20f13a8ca6b1aed` |
| Format | Win16 NE DLL, five segments; automatic data segment 5 |
| Initial local heap request | 1,024 bytes |
| DLL initialization address | `S4:0000` |
| Named entries | `MODULE` at `S1:003E`, `WEP` at `S1:0027`, `___EXPORTEDSTUB` at `S3:07E0` |
| Relocation records | 27 internal, 17 ordinal imports, no named imports or OS fixups |

`S2:00F5` means **NE segment number 2, offset 00F5**. These are research
coordinates, not runtime selectors or linear addresses.

The investigation followed NE entry points, relocation chains, direct calls,
conditional branches, and the dispatcher's bounded 13-entry jump table.
Recursive decoding covered 30 function roots, including all NE entries and
the DLL startup, with no unresolved control transfers in that graph. Capstone
and Iced agreed on every instruction length in those traversals. Iced supplies
the readable recursive listings because Capstone 5 displays some 16-bit
sign-extension instructions with misleading 32-bit names.

This is structural decoding plus manual interpretation of state and branches;
it is not symbolic execution or a mathematical reachability proof. Instructions
outside the traversed graph exist, including runtime startup/error machinery
with additional DOS interrupts and indirect calls. Their presence does not
make them part of the selected DLL lifecycle. A future runtime must stop with
a useful diagnostic if execution departs from the supported contracts.

Lifecycle labels are corroborated by the previously inspected After Dark 2.0
SDK example, retained in local research history. That historical SDK is not
assumed to be the exact source for this binary. The addresses, branches,
field accesses, and import sites below were checked against this binary.

## Lifecycle and control flow

```text
DLL startup: initialize local heap, record instance handle
    |
MODULE(message, HDC, systemHandle)
    |-- GlobalLock(systemHandle)
    |-- read moduleHandle from system block; GlobalLock(moduleHandle)
    |
    |-- 12 PREINITIALIZE: validate three system fields; mark compatible
    |--  0 INITIALIZE: read clock, seed guest RNG, read supplied options
    |--  1 BLANK: optionally fill the surface with a stock brush
    |--  2 DRAWFRAME: check timing; create or remove one inverted rectangle
    |--  3 CLOSE: optionally blank; invert remaining saved rectangles
    |
    |-- GlobalUnlock(moduleHandle), GlobalUnlock(systemHandle)
    `-- far return, removing the three WORD arguments
```

The modern host calls `DRAWFRAME` repeatedly. The draw routine returns when
timing says it is too early; it does not wait for another thread or run a
Windows message loop. Drawing changes are incremental, so the host surface
must persist between calls.

| Stage | Binary evidence | Required behavior |
| --- | --- | --- |
| DLL startup | `S4:0000`, `S1:0000` | Calls `LocalInit` for a nonzero supplied heap size; library initialization rejects a zero heap size and saves its instance handle. |
| Common dispatcher | `S1:003E` | Locks two handles, stores HDC and guest pointers, dispatches, unlocks, and executes `RETF 6`. |
| Preinitialize | `S1:0095`, `S2:0000` | Checks host compatibility fields and sets `DS:0016`; the module-specific helper is otherwise empty. |
| Initialize | `S1:00CE`, `S2:0012` | Clock-based RNG seed, option values, rectangle count reset, and initial tick/calibration state. |
| Blank | `S2:00A8` | If the clear option is nonzero, `SetRect`, `GetStockObject(4)`, and `FillRect`. |
| Draw | `S2:00F5` | Tick-based gating, guest RNG, rectangle storage, `SetRect`, and `InvertRect`. |
| Close | `S2:02B7` | Optional call to Blank, then a reverse walk over saved rectangles with `InvertRect`. |
| Library exit | `S1:0027` | `WEP` returns success without another Windows service. |

Do not equate every returned AX with a well-defined status. The empty
preinitialization helper leaves AX from its prologue rather than explicitly
returning zero. Some dispatcher error/unsupported-message paths can return an
uninitialized local value. The host contract and tests must distinguish defined
results from incidental register contents.

## Windows services on the successful path

| Import | Representative CALL site | Minimum responsibility demonstrated here |
| --- | --- | --- |
| `KERNEL.LocalInit` | `S4:0016` | Establish the requested local heap in guest memory during DLL startup. No local allocation/free calls appear in the selected drawing path. |
| `KERNEL.GlobalLock` | `S1:0059`, `S1:0078` | Turn each supplied system/module handle into a valid guest far pointer returned in DX:AX. |
| `KERNEL.GlobalUnlock` | `S1:01D1`, `S1:01DA` | Honor the corresponding handle/lock contract. This is memory-handle locking, not a thread synchronization primitive. |
| `KERNEL.GetDOSEnvironment` | `S3:0244` | Provide a guest-readable environment for the C runtime's timezone lookup. An empty environment is a proposed constrained input. |
| `USER.GetTickCount` | `S2:0074`, `S2:010D`, and five further sites | Return a progressing 32-bit millisecond clock in DX:AX. Drawing calibration and pacing inspect it. |
| `USER.SetRect` | `S2:00CE`, `S2:029A` | Write four signed 16-bit coordinates through a checked guest pointer. |
| `GDI.GetStockObject` | `S2:00E0` | Return a guest handle for stock object 4, the black brush. |
| `USER.FillRect` | `S2:00E6` | Fill the supplied rectangle on the host-owned surface using that brush. |
| `USER.InvertRect` | `S2:0226`, `S2:02EC` | Invert the rectangle's pixels, respecting the applicable Win16 rectangle/DC semantics. |

The steady draw path uses five of these imports: the two global-handle
operations, the tick counter, `SetRect`, and `InvertRect`. The nine-import
count includes startup and background clearing. Disabling clearing also skips
`GetStockObject` and `FillRect`, but a clear initial background makes a better
first reproducible baseline.

All imported addresses still need relocation bindings. The eight imports below
can initially bind to named unsupported-service traps, rather than receiving
invented successful behavior:

| Imports excluded from the successful visuals path | Why |
| --- | --- |
| `SetTextColor`, `SetBkColor`, `SetTextAlign`, `SetBkMode`, `TextOut` | Dispatcher error text at `S1:00FE..014A` when `DS:0016` is zero. Successful preinitialization selects the actual draw routine instead. |
| `OutputDebugString`, `FatalAppExit`, `FatalExit` | Runtime diagnostic/error routines outside the traversed entry-point graph. Imported CALL sites are `S3:0463/046A`, `S3:07BB`, and `S3:07C4`. |

This is not permission to conceal errors. Calling an excluded service should
report the service name, guest address, and current lifecycle operation.

## The dependency the import census missed: DOS clock interrupts

The sibling Microsoft MS-DOS checkout provides implementation references for
these services. See the [DOS source guide](dos-source-reference.md) for its
location, pinned revision, and date/time handler entry points.

Initialization calls an embedded C-runtime time routine at `S3:002E`. It
executes `INT 21h` with AH=2Ah at `S3:003B` and `S3:0053`, and AH=2Ch at
`S3:0043`. These return the date and time; the date is read again to detect a
midnight change. The converted time seeds the guest's random generator.

The Microsoft MS-DOS Encyclopedia documents the
[date/time interrupt contracts](https://msarchive.pcjs.org/mspl13/msdos/encyclopedia/section5/):

| DOS service | Inputs and outputs relevant to the host |
| --- | --- |
| Get date | AH=2Ah; return CX=year, DH=month, DL=day, AL=day of week. |
| Get time | AH=2Ch; return CH=hour, CL=minute, DH=second, DL=hundredths. |

The timezone helper at `S3:0224` searches for `TZ` and falls back to
`GetDOSEnvironment`. It does not open a file. A valid empty, double-NUL-terminated
environment would select the runtime's embedded defaults; it does **not** mean
UTC. Initial data contains a 28,800-second timezone offset and an enabled
daylight-time flag.

For a deterministic first run, we can supply a fixed valid civil date/time and
an empty environment, while advancing a separate virtual tick clock coherently
across draw calls. Those are proposed host adaptations, not observed original
Windows behavior. The date/time pair must be consistent so the midnight retry
terminates. Tick values must advance: a constant stub can leave drawing in its
calibration early-return path indefinitely across host invocations.

Supporting these two interrupt contracts does not require booting DOS. It does
require a separately tested interrupt interception/return path; our existing
far-call gateway tutorial has not proven that boundary.

## Host records: observed fields, not a recovered full SDK schema

The dispatcher accepts three WORD arguments in far Pascal order: message,
HDC, and system handle. The host supplies two memory handles and associated
guest blocks. They must stay valid throughout each dispatcher call.

| Block | Offset | Observed use |
| --- | --- | --- |
| System | `+14h` | Signed WORD compared against 200; values below 200 fail preinitialization. |
| System | `+20h` | WORD handle of the module record, passed to `GlobalLock`. |
| System | `+28h` | WORD close flag; zero causes Close to call Blank before processing saved rectangles. |
| System | `+2Ch` | WORD must equal `0042h` for successful preinitialization. |
| System | `+34h` | WORD must equal `0048h` for successful preinitialization. |
| Module | `+02h`, `+04h` | Signed WORD width and height; used as divisors when choosing coordinates. Supply positive values, at most 32767. |
| Module | `+06h` | WORD speed option. Recognized values 0/25/50/75/100 map to frame-count thresholds 140/70/30/0/0. |
| Module | `+08h` | WORD clear-background option; nonzero enables the Blank fill. |

These accesses establish a minimum of 36h bytes for the system block and 0Ah
bytes for the selected path's module fields. They do not establish complete
structure sizes or the meaning of every magic/version field. `MODULESELECTED`
additionally writes control IDs at module offsets 0Eh through 14h and an
instance handle at 16h. Reserving a larger record does not substitute for
recovering its full schema when another path needs it.

For the first visuals run, supply explicit dimensions, one recognized speed,
and a nonzero clear flag directly in these records. Skip About, module
selection, and button messages. In this binary, About and the module-specific
selection helper are empty; the button helper changes speed in memory. There
is no settings-file path to implement in this selected lifecycle.

## What the guest drawing code does

After the timing gate admits a change, the module either creates a rectangle
or removes the newest saved one:

- Below 50 saved rectangles, it creates another.
- From 50 through 199, a guest-generated random bit selects creation/removal.
- At 200, it removes the newest rectangle.
- Creating uses four random coordinates, stores the rectangle, and inverts it.
- Removing inverts the stored rectangle again, then drops it from the history.

The count is at `DS:03A8`. The 200 eight-byte rectangle slots occupy
`DS:03AC..09EB`, already inside the module's data segment. This is a history
stack in static memory, not the CPU return stack or a heap allocation.

The guest contains its own random-number generator (`S3:0090` seed,
`S3:00A4` next value), using a 32-bit multiply/add state update and returning
a 15-bit result. That computation remains original x86 code.

The Microsoft Windows 3.0
[Programmer's Reference](https://www.bitsavers.org/pdf/microsoft/windows_3.0/Windows_Programmers_Reference_1990.pdf)
describes `InvertRect` as reversing pixel colors; applying it twice to the same
rectangle restores them. This explains why the module saves rectangle geometry
instead of saved pixel blocks. It also makes inversion a useful deterministic
test invariant.

Do not silently sort coordinates or choose modern rectangle behavior by habit.
The guest generates each corner independently. Reversed/empty rectangles,
edge exclusion, clipping, and raster behavior still need explicit Win16
conformance decisions/tests. Modern
[SetRect documentation](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setrect)
describes coordinate assignment, not normalization; it is useful context, not
proof of every Win16 rendering detail.

## What this establishes, and what it does not

- **Artifact facts:** segment/import/entry metadata, instruction paths, jump
  table targets, accessed fields, static rectangle storage, clock interrupts.
- **Reasoned inference:** valid supplied records and the selected lifecycle
  should permit useful original-code visuals with the nine services above.
  No difficult scheduling primitive appears on that path.
- **Proposed modern adaptations:** fixed options, deterministic seed time,
  virtual tick progression, an owned surface, and later presentation/scaling.
- **Observed runtime behavior:** none from Mondrian in this investigation.
  No original module was executed and no pixel fidelity was tested.

The remaining work is substantial but concrete: NE loading/relocations,
selectors and guest-pointer validation, module startup and data/stack setup,
actual Win16 ABI handling, the clock interrupt boundary, handles, lifecycle
records, and rectangle rendering. Static analysis does not prove Unicorn
handles every instruction/exception this module can encounter.

A useful future execution checkpoint is: run original startup and the selected
lifecycle with deterministic inputs, observe original code calling
`InvertRect` with valid guest-produced geometry, and verify changed pixels on
our surface. That is an original-module execution proof. Comparison with
historical output is a later visual-fidelity proof. A legacy Windows debugger
can be reserved for a concrete unresolved behavior or such a comparison.

## Reproduce locally

The hash-specific [research inspector](../../tools/inspect-mondrian-paths.py)
requires the owner's local artifact. It refuses other hashes. It is not a
general hardened NE loader. From the repository root:

```powershell
python -m pip install --target artifacts/mondrian/python capstone==5.0.6 iced-x86==1.21.0
python -B tools/inspect-mondrian-paths.py ad/Mondrian.ad
```

Capstone (BSD license) supplies instruction groups/operands; Iced (MIT license)
independently checks lengths and formats 16-bit instructions. Both dependencies
are research-only, installed under ignored artifacts; neither is added to the
C# runtime or test projects.

The tool emits metadata, a call graph, recursive function listings, and linear
navigation listings under ignored `artifacts/mondrian/`. Internal relocations
are substituted with segment numbers for display only. Imported operands in
listings retain on-disk placeholder/chain values; their annotations identify
the actual imports. Linear listings can decode data as instructions and are
not reachability evidence.

The checked invariants are 30 decoded roots, no instruction-length disagreements,
no unresolved transfers within that graph, three reachable interrupt sites,
and 14 reachable imports out of 17. The reduction from 14 to nine follows the
documented successful preinitialization branch, not an automatic whole-program
proof. Original bytes and complete disassembly remain private and ignored.
