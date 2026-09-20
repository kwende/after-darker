# Magic: reusing the local heap for a line history

Date: 2026-09-19. Branch `codex/magic-player` began at reviewed main `7427aaf`
(Lasers merged through PR #16). This increment implements only Magic.

## Artifact and chosen settings

The supported local artifact has SHA-256:

```text
777DFD7BEA0084E3947EFCD2E4E3E3ACD9BA22445F51D6AB8C19EFC7ABCC2763
```

**Artifact facts:** five NE segments; automatic data segment 5; startup
`S4:0000`; MODULE `S1:004C`; 24 imported identities; 96 loader patches;
initial local heap 1,024 bytes.
Resources include four SDK control records (type 1000) and three type-2000
records. The production profile supplies the full 34-byte AD_MODULE record:

| Control | Host value | Guest interpretation |
| --- | ---: | --- |
| Lines | 60 | Original lookup selects a 100-line history |
| Line Speed | 100 | `(100 - value) * 2 = 0`: advance on every DRAWFRAME |
| Color Speed | 85 | `(100 - value) * 5 = 75`: change color after 76 updates |
| Mirror | 1 | Horizontal: also draw each line reflected across the width |

These are **explicit host choices**, not reconstructed user preferences or a
claim about all historical defaults. In particular, the Lines resource's raw
default is 8, but the module's switch accepts values such as 0, 12, 18, ... 60,
... 100. Passing that raw default through unchanged would not select a known
case. We choose a value whose effect is established by the machine code.
The Mirror resource labels are None, Horizontal, Vertical and Both.

WPF disables its generic speed selector for this fixed configuration. The
system record advertises 24-bit color, with palette and sound fields zero.
The profile rejects dimensions below 3 before guest creation: initialization
divides by `width - 2` and `height - 2`. The shared maximum remains 2048.
Boundary tests establish safe execution and cleanup, not useful visuals on a
3x3 surface.

## What changed, and what could be reused

**Observed runtime behavior:** the existing loader, segmented execution,
Pascal stack marshaling, LocalInit/Alloc/Lock/Unlock/Free and GDI pen/line
services suffice for this path. No shared Win16 implementation changed.
The additions are [MagicProfile](../../src/AfterDarker.Runtime/Modules/MagicProfile.cs),
[MagicState](../../src/AfterDarker.Runtime/Modules/MagicState.cs), hash-gated
selection, WPF integration and private execution tests.

INITIALIZE requests `LocalAlloc(0042, 1520)` (movable and zero-initialized),
locks the handle, saves its near offset, and unlocks it before returning.
The 1,520 bytes hold 152 ten-byte records; our selected trail uses 100 slots.
The original allocation size does not shrink to the selected trail length.

Each record contains five WORDs, in order: first X, second X, first Y, second
Y, color index. The guest addresses records as `DS:[historyOffset + index * 10]`.
Ordinary x86 instructions read/write these bytes directly; C# does not calculate
endpoints, maintain the ring or choose the colors.

```text
LocalLock(history handle) -> near offset in the module's DS
    -> read oldest line and draw it black, including its mirror
    -> advance endpoints, bouncing at the edges
    -> draw new colored line and its mirror
    -> save endpoint/color words in that same slot
    -> advance circular index; wrap at 100
LocalUnlock(history handle)
```

The original code owns a 21-color cycle on this true-color path. It also
periodically changes endpoint velocities: the motion counter wraps after 701
updates. Each DRAWFRAME makes four LineTo calls (two black, two colored).
Each line creates, selects, restores and deletes a pen; only one owned pen is
live at a time, and none is retained between frames.

**Modern adaptation:** as with Lasers, the profile opts into pre-mapping the
full 64-KiB data segment so the allocator can grow beyond the initial 1-KiB
reservation. Static storage ends at `0424`, leaving 64,476 heap bytes. In the
observed run, handle `0002` resolves to `0028:0424`. The handle identity and
growth policy belong to our host; the original module supplies the size,
flags and contents. See the [heap ownership guide](../win16-local-heap.md).

BLANK fills black and returns `10`, the SDK's HSV_PAL request. Magic uses the
same explicit true-color policy as Spiral Gyra and Lasers: accept that request
and consume the module's COLORREFs directly. No indexed-palette implementation
is claimed. CLOSE fills black, frees the history and clears the saved handle;
WEP returns 1.

## Static anchors

These locations refer only to the hashed artifact. Full disassembly and local
reports remain under ignored `artifacts/magic/`. SDK record declarations and
provenance are documented in [the SDK research](after-dark-sdk.md).

| Location | Mechanism |
| --- | --- |
| S1:001B..001E | Startup stores the instance at data `0400` |
| S1:006B..0093 | Locks system/module records; stores pointers at `03F4` and `041E` |
| S1:00A8..00D0 | Host compatibility check and flag at data `0016` |
| S2:002B..0054 | Converts speed controls; stores mirror choice at `0394` |
| S2:0057..00D4, 01FD..0281 | Maps Lines control to trail length; 60 selects 100 |
| S2:00DA..00F0 | Allocates 1,520 bytes, saves handle at `0010` and locked offset at `0012` |
| S2:0333..035F | Initial coordinate divisions require dimensions greater than 2 |
| S2:0400..0467 | BLANK clears the surface and requests HSV_PAL |
| S2:0468..057A | DRAWFRAME: speed gate, lock, erase/draw/save, counters and unlock |
| S2:068D..07BF | Reads the old line from the ring and erases it and its mirror |
| S2:07C0..0838 | Writes five words into the selected ten-byte history slot |
| S2:0839..08CD | Creates a pen, draws one line, restores/deletes the pen |
| S2:08CE..09C3 | Draws the new line and selected mirrored copies |
| S2:0A94..0ABF | Increments color index, wrapping 22 back to 1 on this path |
| S2:0AC0..0B41 | Chooses new endpoint velocities |
| S2:057B..05E8 | CLOSE clears the surface, frees history, zeros the handle |

## Verification and limits

The complete regression passed **312 cases** with the compiler fixture and all
six private modules enabled. The compiler/module-free public suite separately
passed **235 cases**.

Six private integration cases establish:

- One movable allocation of 1,520 bytes beyond the initial reservation, with
  guest handle/offset globals matching the shared allocator's snapshot.
- 1,700 draws at 640x480. Every returned state has the expected history index,
  motion counter and color index, crossing 17 ring wraps, two motion-counter
  wraps and a full color cycle. Before CLOSE: **1,507,579 instructions**,
  **6,800 LineTo calls**, one allocation, zero outstanding local locks and
  zero live pens. RGB SHA-256:
  `3E456CDE5DE0778A889DD66174B54C12A28C20F25A384AF1001361A7F8D18C2C`.
- Independent guests produce identical pixels for 160 draws, crossing ring
  wrap and color changes. Closing one leaves the other's heap usable.
- 101 draws and cleanup at 3x3, 2048x2048 and asymmetric 321x239 dimensions.
- Shutdown before drawing, idempotent shutdown, small-dimension rejection
  and rejection of an unknown artifact revision.

Actual WPF acceptance passed **Magic alone**, then **Magic -> Lasers** and
**Lasers -> Magic**. Standalone Magic presented 30 images and verified exact
bitmap RGB readback, Stop/restart and close while playing. The report names
Magic and the captured MAGIC window was visually inspected. Cleanup completed
with SP `1000`, caller DS `0048`, zero global/local locks, zero pens and no local
allocations. Reports and images remain in ignored `artifacts/wpf-smoke/magic-*`
and `artifacts/wpf-smoke/lasers-to-magic`.

This proves original-code execution, meaningful drawing, reuse of the heap
and bounded lifecycle behavior. **Reasoned inference:** the shared heap is
also a useful foundation for String Theory and Zot!, but their later paths
remain untested. Neither is enabled here. Other Magic settings, indexed-color
behavior, historical pixel/pacing fidelity and indefinite endurance remain
outside this result. The host's approximately 60 calls/second is a modern
pacing choice; Magic itself does not request a Windows timer on this path.

Reproduce without copying the original module into test output:

```powershell
$env:AFTER_DARKER_MAGIC = (Resolve-Path ad/Magic.ad).Path
dotnet test -p:TestLocalMagic=true --filter TestCategory=LocalMagic --report-trx
dotnet run --project src/AfterDarker.Wpf --no-launch-profile -- ad/Magic.ad
# Actual-window acceptance:
dotnet run --project src/AfterDarker.Wpf --no-launch-profile -- --smoke ad/Magic.ad artifacts/wpf-smoke/magic-only
```

The reusable idea: **heap APIs provide storage; the original module owns the
animation data and the algorithm that updates it.** Magic reuses that boundary
without any Magic-specific logic inside the allocator or GDI renderer.
