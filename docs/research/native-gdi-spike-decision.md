# Decision: retain hand rolled GDI after the native spike

**Accepted 2026-09-21.** Production playback uses our **hand rolled** C# GDI
implementations. **Native** means modern Windows GDI plus our Win16 compatibility
work. The native playback backend is removed. This record supersedes the earlier
recommendation to keep both implementations.

## Goal and reason for the decision

We were looking for a **quicker and easier way to deliver remaining original AD
modules**, not native rendering for its own sake. First we replaced Gravity's
existing rendering with native calls. Then we enabled previously unsupported
Puzzle using native ScrollDC. Finally we implemented Puzzle using the existing
hand rolled machinery and compared the two honest attempts.

Native did remove responsibility for a drawing algorithm. For Puzzle, however,
our existing overlap-safe bitmap copier made the hand rolled addition small.
Neither path removed Win16 argument decoding, pointer validation, guest handles,
return registers, stack cleanup or the After Dark host contract. Maintaining a
second renderer and its ownership rules did not buy a clear delivery win.
The owner chose to retain the hand rolled implementation and remove the spike.
This is a scope and engineering judgment, not proof that native reuse is always
inferior or that future reuse cannot help.

## What was actually observed

The same original Puzzle artifact (SHA-256
`1E25FBC9567DA400A09A7056D90178BD6D329BA7B262F44573904C8922F53F5D`)
ran in independent guests with identical settings, seed, stepping clocks and dimensions.
Neither implementation recreated Puzzle's tile-selection logic.

| Controlled 320x240 comparison | Result for both paths |
| --- | --- |
| Completed draws | 1,800 |
| Changed frames | 1,639 |
| Pixel differences | Zero on every compared frame |
| Compared import calls | 30,183; identities, arguments, replies and post-return registers matched |
| Guest instructions | 388,768 |
| ScrollDC / UnionRect calls | 1,721 / 83 |
| Shutdown | CLOSE/WEP completed and owned resources were released |

Three additional 600-draw comparisons (128x128/seed 0, 321x239/seed 13,
640x480/seed 27) also matched. Odd dimensions exercised CopyRect. The 459-case
rectangle/delta/origin oracle matrix matched after the origin correction below.
Both WPF implementations passed playback, readback, stop, restart and close;
native Gravity/Puzzle switching was also exercised.

The earlier Gravity proof ran 600 draws at 320x240. Ball state agreed, but 594
frames differed, by up to 4,495 of 76,800 pixels, primarily around ellipse
rasterization. Thus modern GDI was not automatically pixel-equivalent to our
renderer, and neither comparison establishes Windows 3.1 visual fidelity.

The pre-removal combined suite passed 392 tests (369 public, 12 local Gravity,
11 local Puzzle). Those are **historical spike counts**, not the current suite.
Current commands and evidence live in [Puzzle execution](puzzle-execution.md)
and [current status](../current-status.md).

## Why the short native adapter was not the whole cost

A reduced excerpt of the retired native method shows its real attraction:

```csharp
// Illustrative excerpt; NativeGdi and Context belonged to the removed backend.
nint context = Context(hdc).Handle;
NativeGdi.Rectangle[]? nativeScroll = scroll is { } value ? [ToNative(value)] : null;
NativeGdi.Rectangle[]? nativeClip = clip is { } bounds ? [ToNative(bounds)] : null;
NativeGdi.Rectangle[]? nativeUpdate = needsUpdate ? new NativeGdi.Rectangle[1] : null;
bool success = NativeGdi.ScrollDC(context, horizontal, vertical,
    nativeScroll, nativeClip, 0, nativeUpdate);
// Narrow the output coordinates to Win16 and write them to checked guest memory.
```

Windows handles overlapping pixel movement, clipping and exposed-area bounds.
But `Context(hdc)` depends on a native DC/DIB ownership system; copied RGB output
requires conversion from its native pixel layout, and resource/thread lifetime
must fit our asynchronous guest worker. No guest pointer can become a native
pointer directly.

The hand rolled mechanism reuses `PixelSurface.CopyRegion`:

```text
clip       = requested clip intersected with surface and rectangular DC clip
source     = requested scroll rectangle intersected with clip
moved      = translated source intersected with clip
copy       = existing CopyRegion, from moved - delta to moved
exposed    = bounding box of source minus moved
```

The actual pixel work is still a short call:

```csharp
changed = context.Draw(destination => destination.CopyRegion(destination,
    moved.Left, moved.Top, moved.Right - moved.Left, moved.Bottom - moved.Top,
    moved.Left - horizontal, moved.Top - vertical));
```

The subtraction yields up to four strips; their bounding box goes back to the
guest. ScrollDC does not erase those pixels. Puzzle then calls FillRect on that
returned rectangle. Returning success without the rectangle would be wrong.

| Cost at the time of comparison | Hand rolled | Native |
| --- | --- | --- |
| New scrolling file, including comments/blanks | 74 lines | 23-line adapter plus 3-line P/Invoke declaration |
| Overlap-safe copier | Reuses existing 56-line CopyRegion | Windows implementation |
| New geometry semantics | Our code and conformance tests | Windows; bridge tests still necessary |
| Win16 signatures, checked memory, rectangle helpers, profile | Shared costs | Shared costs |
| Prerequisite additional renderer infrastructure | None | Gravity spike: 542 lines across five drawing files, 98-line interface, worker integration |

The native per-call adapter was shorter and clear. It saved roughly 48 lines of
new scrolling implementation here, not all the roughly 150 Core/Runtime lines
initially attributed to enabling Puzzle. Most of that work was shared. The
hand rolled renderer also represents a preexisting investment: these counts do
not price rewriting it from scratch. Counts include comments/whitespace and
are scope evidence, not effort or speed measurements.

CopyRect is void in Win16 but returns BOOL in Win32. This is **shared ABI work**,
not a penalty unique to native. Our Win16 registry handles it in either case.
The hand rolled attempt followed the native attempt and benefited from its
settings, traces and ABI discoveries. We have no defensible independent
implementation-time ratio, and performed no throughput benchmark.

## A useful correction we keep

The first hand rolled ScrollDC passed all 441 zero-origin cases but returned
exposed bounds in device coordinates. The native memory-DC oracle returned them
translated back by the window origin, including empty bounds at that origin
for nonzero movement. A zero-distance call left our zero-initialized output
rectangle at zero. Correcting the hand rolled implementation made the additional
18 origin cases agree. Puzzle uses a zero origin, but the shared API needed this
coverage. This was real algorithmic work saved by native delegation.

Both spike implementations rejected nonzero update-region handles. The retained
hand rolled implementation also explicitly rejects selected elliptic clips;
rectangular clips are supported. Arbitrary mapping modes, complex regions and
window invalidation were not proven.

## What remains, what was removed

Retained: Puzzle's supported profile, checked CopyRect/UnionRect/ScrollDC imports,
hand rolled scrolling, output-write preflight, lesson 12 and tests. Direct Windows
calls remain only in conformance oracles, extending an existing testing practice.

Removed: native production drawing files, IWin16Drawing abstraction introduced
for the spike, backend options/results, native WPF menu/flag/profiles, native-only
dedicated-thread changes and comparison lessons. Playback uses the prior
sequential asynchronous owner. Public pixel/lifecycle contracts do not require
native resources. No new external package was introduced.

The spike was never committed as a production backend. Concise mechanisms and
results are preserved here instead of dead production code. A full pre-removal
source snapshot is retained locally under ignored `artifacts/native-spike-archive/`.
Private reports include:

- `artifacts/puzzle-comparison/20260921-145950-1d6593ff/comparison.json`
- `artifacts/puzzle/20260921-142341-b7360fdc/report.json`
- `artifacts/puzzle-comparison/wpf-software/report.json`

These paths are provenance for the owner's local evidence, not public test
prerequisites. Original modules and captures are deliberately not distributed.

## What would justify revisiting reuse

Do not repeat a generic “try native GDI” Gravity/Puzzle spike. Bring a concrete
missing operation or family, estimate the total avoided implementation, and
include ABI, handle/state translation, deployment, ownership and regression
costs. A substantially harder drawing operation could change the result.

Before more screensavers, the owner wants a separate discussion of Wine and
other faster options: what can actually be reused inside our .NET application,
what still belongs to the Win16/AD bridge, and the delivery/dependency/licensing
consequences. This decision does not reject Wine or pretend we evaluated every
reuse strategy. [The earlier reuse assessment](win16-reuse-assessment.md) is
background to refresh, not a settled answer to that next discussion.
