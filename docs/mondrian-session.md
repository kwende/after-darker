# Mondrian session: explicit guest lifetime

This is step 1 of the live-window work. One `MondrianSession` owns one loaded
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

var first = session.DrawFrame();
byte[] rgb = session.CopyPixels();

// C# has control here. Registers, guest memory and the surface remain alive.
var second = session.DrawFrame();
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
- `CopyPixels` returns a detached array. `GetResult` snapshots the diagnostic
  lists; subsequent calls do not append to previously returned observations.
  Take that result before disposing; the returned observations survive disposal.
- The host owns any supplied `TextWriter`. The session does not dispose it.
- A session has one sequential owner. No scheduler, synchronization or worker
  thread is introduced in this step.

## Assembly boundary

`AfterDarker.Runtime` is a class library referencing `AfterDarker.Core` and the
same existing Unicorn 2.1.3 package. `SegmentedGuest` moved into it without a
CPU behavior change. The console app now references this library; a future WPF
app can do the same without referencing the educational executable.

Core remains independent of Unicorn. Its NE parser, API implementations and
pixel operations did not change. Native Unicorn restoration/copying now belongs
to the runtime project and propagates to hosts. Executable-specific CFG setup
still belongs to each host. Earlier CPU tutorials retain their direct managed
Unicorn package reference. No package version or third-party dependency changed.

## Verification and boundaries

The existing initialization and frame tests still pass. Eight new local-module
cases verify separate calls preserve state, snapshots remain detached, two live
sessions are independent, initialization-only behavior stays narrow, disposal
works from three stages, and initialization/drawing failures prohibit retry.
The 30th image's recorded pre-refactor pixel hash is also asserted.

A fresh output-directory build verified the runtime's native asset reaches the
console host. Its 30-frame capture matches the earlier capture's PNG bytes and
report, including 11,859 instructions. The build uses a new directory under the
project's `bin`, as required by the existing apphost configuration guard.

This step does not make execution indefinite yet. Diagnostic lists and image
copies remain as before (step 2); the deterministic clock remains as before
(step 3). WPF/workers are step 4. Cancellation and original CLOSE/WEP are step 5.
Here, `Dispose` releases native resources without invoking guest shutdown code.
