# Mondrian session: explicit guest lifetime

Steps 1 and 2 of the live-window work give one `MondrianSession` a loaded
Unicorn guest, its memory, service state, and optional software surface. The
host chooses when to call it and when to dispose it. No continuation callback
is needed to keep the native engine alive.

Start with the actual caller in
[Tutorial09MondrianFrames.Capture](../src/AfterDarker.Tutorials/Lessons/Tutorial09MondrianFrames.cs),
then follow the methods in
[MondrianSession](../src/AfterDarker.Runtime/MondrianSession.cs):

```csharp
using var session = new MondrianSession(file, new(Speed: 100));
session.Initialize(); // Original DLL startup, PREINITIALIZE, INITIALIZE.
session.Blank();      // Original MODULE(BLANK), prepares the surface.

byte[] rgb = new byte[session.PixelByteCount]; // Allocate once for repeated copies.
var first = session.DrawFrame();
session.CopyPixelsTo(rgb);

// C# has control here. Registers, guest memory and the surface remain alive.
var second = session.DrawFrame();
session.CopyPixelsTo(rgb); // Same host array, newly completed image.
var evidence = session.GetResult();
// Leaving the using scope releases the native engine.
```

Construction validates the known artifact and prepares/maps its segments,
records, import gateways and callers. It does not execute guest instructions.
The code that formerly occupied `MondrianRunner.Execute` is now split between
the constructor, `Initialize`, and named `Blank`/`DrawFrame` methods. Argument
decoding, gateway handling, register setup, and return verification retain the
same mechanism. Tutorial 08 constructs an initialization-only session and stops
after `Initialize`, so it still performs exactly three phases and eleven imports.

## State and ownership

```text
constructor → Loaded → Initialize → Initialized → Blank → Ready
                                                         ↑     |
                                                         └ DrawFrame

failure during a guest call → Faulted → Dispose → Disposed
any live state             → Dispose → Disposed
```

- `Initialize` runs once. `DrawFrame` requires a completed `Blank`.
- An invalid call order is rejected before touching native execution and leaves
  the existing state intact. A ready session can explicitly call `Blank` again.
- A failure while executing a lifecycle call marks the session `Faulted`.
  The guest might be halfway through a procedure, with an incomplete stack
  frame or acquired locks. Further execution is rejected; the owner disposes it.
- `Dispose` is idempotent. Instance operations after disposal reject access
  before reaching Unicorn. A constructor failure after engine creation also
  releases that engine, because the caller's `using` cannot own a failed constructor.
- `CopyPixelsTo` writes into host-owned storage and retains no reference to it.
  `CopyPixels` still exists for an explicitly allocated, detached snapshot.
  `GetResult` snapshots retained diagnostics and lifetime counters; subsequent
  calls do not modify previously returned observations.
  Take that result before disposing; the returned observations survive disposal.
- The host owns any supplied `TextWriter`. The session does not dispose it.
- A session has one sequential owner. No scheduler, synchronization or worker
  thread is introduced in this step.

## Assembly boundary

`AfterDarker.Runtime` is a class library referencing `AfterDarker.Core` and the
same existing Unicorn 2.1.3 package. `SegmentedGuest` moved into it without a
CPU behavior change. The console app now references this library; a future WPF
app can do the same without referencing the educational executable.

Core remains independent of Unicorn. Its NE parser, API behavior and raster
semantics are unchanged. Native Unicorn restoration/copying now belongs
to the runtime project and propagates to hosts. Executable-specific CFG setup
still belongs to each host. Earlier CPU tutorials retain their direct managed
Unicorn package reference. No package version or third-party dependency changed.

## Bounded diagnostics (step 2)

The default `DiagnosticOptions.Recent` keeps at most **256 records of each kind**:
import calls, completed lifecycle phases, and serviced interrupts. Each history
uses a preallocated ring; a new record replaces the oldest slot. A snapshot
returns the retained records in chronological order. See
[DiagnosticHistory](../src/AfterDarker.Runtime/DiagnosticHistory.cs).

```csharp
// Default for a long-lived host: recent history with lifetime totals.
using var session = new MondrianSession(file);

// Optional alternatives: a smaller recent window, counters only, or all details.
var small = new DiagnosticOptions(HistoryCapacity: 8);
var countersOnly = new DiagnosticOptions(HistoryCapacity: 0);
var full = DiagnosticOptions.Full; // Explicitly unbounded; use for a bounded experiment.
```

Supply that policy via the session's `diagnostics:` parameter. Tutorials 08 and
09 explicitly select `Full`; their short, bounded experiments retain all the
details previously available for inspection. `trace: true` and any supplied
`TextWriter` are still opt-in output; their external storage is the host's concern.

`GetResult().Calls.Count` now describes **retained** calls, not total calls.
`GetResult().Diagnostics` reports total calls, phases, interrupts, and per-import
counts independently of the retained window. The import-count dictionary can
only acquire keys from the fixed binding table. Capture reports use those totals.
Initialization checks the total interrupt count, so a tiny or zero-length history
cannot accidentally invalidate otherwise correct guest execution.

Lifetime counters use saturating 64-bit values. The per-invocation instruction
budget has its own counter, reset only at the next host invocation. Dropping
history or saturating a diagnostic total cannot disable a CPU safety budget.
Full history is intentionally an exception to bounded retention, and returned
snapshots are host-owned: retaining every snapshot indefinitely would still grow
the host's memory.

## Reusing pixel buffers (step 2)

`PixelByteCount` describes tightly packed RGB storage. `CopyPixelsTo` requires
exactly that size and checks the session state before copying. It allocates no
new frame array and never exposes the surface's mutable internal buffer. The
host must finish consuming a buffer before overwriting it with another copy.
No cross-thread ownership scheme is introduced until step 4.

Tutorial 09 now allocates two arrays, **previous** and **current**, once:

```text
DRAWFRAME → copy into current → compare against previous
    no change: reuse current on the next call
    changed: consume current synchronously, then swap the two arrays
```

The capture callback is now `FrameSink(Frame, ReadOnlySpan<byte>)`. The span is
borrowed only for that synchronous callback; a consumer needing a persistent
snapshot must explicitly call `ToArray()`. `PngWriter` accepts the span directly.
This changes the old callback's ownership contract, which supplied a separate
array per changed frame. The console PNG sink consumes the data before returning.

At 640x480, the comparison buffers occupy 1,843,200 bytes in total. Their number
does not grow with draw calls. PNG encoding still has its own transient output
buffers, and guest execution still creates smaller temporary managed records;
this is not a claim of allocation-free emulation.

## Verification and boundaries

The existing initialization and frame tests still pass. Eight step-1 local-module
cases verify separate calls preserve state, snapshots remain detached, two live
sessions are independent, initialization-only behavior stays narrow, disposal
works from three stages, and initialization/drawing failures prohibit retry.
The 30th image's recorded pre-refactor pixel hash is also asserted.

A fresh output-directory build verified the runtime's native asset reaches the
console host. Its 30-frame capture matches the earlier capture's PNG bytes and
report, including 11,859 instructions. The build uses a new directory under the
project's `bin`, as required by the existing apphost configuration guard.

Step 2 adds ring wrap/order, zero/full retention, snapshot independence, buffer
size/isolation and zero-allocation copy tests. A synthetic INT loop checks that
history remains bounded and repeated host exits cannot reset a call's instruction
budget. Local runs with capacities 0, 1 and 8 match full recording in guest state,
pixels and totals. A 5,000-draw run at 64x48 retains only 256 calls and phases,
keeps complete totals, and reuses one host pixel array. Repeated session copies
into an existing buffer measured zero managed allocations. All 30 default PNG
files and capture-report contents still match step 1 exactly.

These are bounded-retention and buffer-reuse proofs, not an hours-long native
memory-leak or real-time responsiveness test. The deterministic clock remains
for step 3. WPF/workers are step 4. Cancellation and original CLOSE/WEP are step 5.
`Dispose` still releases native resources without invoking guest shutdown code.
