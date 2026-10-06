# One After Dark entry in Ocuvera Toasters

September 29, 2026 source assessment; monitor scope narrowed September 30;
deployment simplified October 4. After Darker was clean on `main` at
`a5683f2`, including Spheres; Ocuvera was clean on `main` at `db2e78e`.
The initial assessment changed no consumer code. The October 4
[deployment increment](localappdata-publishing.md) now implements project references,
both LocalAppData profiles and the installed `.scr` native-engine diagnostic.
The owner subsequently authorized [scene selection/lifetime integration](ocuvera-integration.md),
now locally verified for all 18 supported artifacts. The work packages below retain
the original plan; the linked implementation guide is current. This plan supersedes the older recommendation to register every
original as an individual Ocuvera catalog entry.

The owner proposes one **After Dark** entry that selects a random supported
original each time it runs. The owner also explicitly authorizes small changes
to Ocuvera's interfaces when they simplify integration. Do not preserve an
awkward synchronous boundary merely for architectural purity. The owner also
prefers Visual Studio folder publishing to `%LOCALAPPDATA%\OcuveraToasters`
on two owned machines, both with Visual Studio. Running from Release output is
also acceptable for the first proof. Neither an installer, NuGet publication
nor a single-file `.scr` is a first-delivery requirement. This is scoping;
it does not by itself authorize implementation or change the remaining v1 list.

## Recommended shape

```text
Windows launches ocuvera.scr
  -> Ocuvera chooses its After Dark entry
  -> entry chooses one available, supported original
  -> AfterDarker.Runtime loads its bytes and runs original Win16 code
  -> worker publishes copied RGB frames
  -> Ocuvera scene copies frames to its own WriteableBitmap
  -> Ocuvera requests stop, awaits guest cleanup, then rotates or exits
```

There is one process. Our WPF executable, WineVDM and the original AD launcher
are not dependencies. Preserve Core/Runtime as shared libraries consumed by
our tutorials, our WPF host and Ocuvera. Put the small WPF scene adapter in
Ocuvera; do not begin by designing another general plugin framework.

Proposed Ocuvera types:

- `AfterDarkModule`: implements `IScreensaverModule`, stable ID `after-dark`,
  label `After Dark (Original Win16)`, ordinary duration/enablement behavior.
- `AfterDarkScene`: owns its Image, RGB24 WriteableBitmap and one playback
  lifetime. `Update` only presents available pixels; `Resize` scales the Image.
- `AfterDarkLibrary`: discovers local files and maintains the eligible module
  pool. A small shuffled bag avoids repeating the same original while others
  remain available. Persisting that inner bag is optional for the first proof.

Ocuvera already uses a persisted shuffled bag for the outer collection. Giving
After Dark one entry gives the whole family one slot in that rotation; adding
more supported AD files does not crowd out native scenes. This is a deliberate
selection policy. Individual original entries or weighting can be added later
without changing the runtime. Keep each original's identity in logs and UI
diagnostics, even though the outer entry has one shared ID.

## What is already reusable

| Existing component | Role in this integration |
| --- | --- |
| `SupportedModules.Open` / `IAnimationSession` | Recognize exact bytes, load NE segments/resources, execute lifecycle, copy pixels, shut down |
| `AfterDarkPlayback.RunAsync` | One serialized background owner, 60-Hz pacing, cancellation between bounded guest calls, intermediate images |
| `LatestFrameMailbox` | Bounded, copied RGB24 transfer to one presenter |
| Ocuvera `IScreensaverModule` | Identity, duration, monitor policy and scene creation |
| Ocuvera `ScreensaverSceneHost` | Canvas attachment and `CompositionTarget.Rendering` updates |
| Ocuvera Whitney scene | Existing Image/WriteableBitmap presentation pattern; RGB24 is also permissible |

There are currently 18 supported artifacts overall, 14 in Milestone 1. The
adapter should work with the installed runtime's available supported profiles;
it must not hardcode those counts or the milestone list. Selection can later
be filtered by an explicit allowlist. Ocuvera's native recreations stay distinct.

## Work package 1: deliver an installation folder

**Implemented locally October 4:** see [configuration and verification](localappdata-publishing.md).
The second machine remains untested. The following records the chosen design.

The first consumer can reference the existing Runtime project, which references
Core and the pinned managed Unicorn binding. A documented build property can
locate the producer checkout; only the development machine needs that checkout.
Keep the projects separate, without copying loader/GDI source into Ocuvera.
Formal NuGet packaging remains optional until independently versioned consumption
is useful. The user's goal is a working installed library, not a public feed.

Publish an ordinary Windows x64 application folder containing `ocuvera.scr`,
AfterDarker managed libraries, the managed Unicorn binding, `unicorn.dll`, other
Ocuvera dependencies and required runtime metadata. Use framework-dependent
output with the matching .NET 10 Windows Desktop runtime installed on both
development machines; Visual Studio presence alone is not the runtime check.
This is the contents of a complete folder publish, not just a hand-picked DLL
copy. Explicitly prove the native asset propagates to publish: today's Runtime
project restores it under repository-local `artifacts/` and marks it for output
copy, which is not yet evidence of the intended installed layout.

Pin Windows x64 for development and release. Ocuvera's single-file profile
already selects `win-x64`; its ordinary project does not explicitly set an
architecture. Build/download/hash verification and any `editbin` work happen
on each build machine. Both target machines will have Visual Studio; there is
no requirement to prove deployment to a clean machine without developer tools.

The important unresolved deployment proof is the existing Unicorn/CFG apphost
workaround. Our helper currently accepts only named After Darker outputs and
requires Visual Studio's `editbin`. For this controlled consumer, prefer an
explicit Ocuvera build step for its generated apphost, with checked target
ordering, rather than silently patching arbitrary package consumers. Configure
both development and folder-publish outputs before copying to `.scr` and before any
signing. Do not modify the shared .NET host or machine security policy.

Add a checked-in Visual Studio **LocalAppData** folder-publish profile: Release,
win-x64, framework-dependent, `UseAppHost=true`, `PublishSingleFile=false`.
Set both `PublishDir` and `PublishUrl` to
`$(LOCALAPPDATA)\OcuveraToasters\` so CLI and Visual Studio use the same destination.
The intended workflow is **Publish -> LocalAppData -> Publish** on either machine.
Apply the CFG step and create `.scr` automatically, in the correct build order.
Keep ordinary F5 builds separate from publishing so debugging does not overwrite
the installed copy. Stop the screensaver before replacing loaded binaries.

Place the complete application and dependencies together in that folder and
point Windows at its `ocuvera.scr`. Use a configured external directory for AD
files, keeping user files/settings outside the publish output. Application
assets resolve relative to the executable, not the working directory. Ocuvera's
existing script that copies a single file to System32 is not needed for this
workflow. A PowerShell helper for setting the screensaver path is optional;
automated installation/registration must not delay the first running scene.

Running the complete Release output directly is acceptable for the first
integration proof. AppData provides a stable location once publishing is wired.
No installer, upgrade framework, registry-based dependency discovery, or
System32 deployment is required.

The local native DLL measures 21,567,488 bytes (about 20.6 MiB), not a Wine
installation; total folder-publish size remains unmeasured. Carry
forward dependency notices; broader project-license/public-package decisions
remain separate from this personal deployment proof.

Acceptance: run a source-owned guest fixture from the staged installation folder
with a different working directory, ordinary executable and renamed `.scr`.
Inspect final native assets and executable flags. Repeat the Visual Studio
publish/run on the second owned machine when available. Do not create a clean-
machine deployment project or require an installer first. Local AD acceptance
follows separately.

Optional later: Core/Runtime NuGet packages with
`runtimes/win-x64/native/unicorn.dll` use standard
[NuGet native-asset selection](https://learn.microsoft.com/en-us/nuget/create-packages/native-files-in-net-packages).
Single-file deployment uses [.NET's native bundling/extraction support](https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview#native-libraries).
Neither experiment should block this folder installation.

## Work package 2: make stop awaitable in Ocuvera

Current rotation calls `ScreensaverWindowManager.Dispose`, which closes windows;
`MainWindow.Closed` synchronously disposes scenes. Input handlers directly call
`Application.Shutdown`. None of these paths can currently await the guest.

A small proposed scene extension is sufficient:

```csharp
// Proposed addition to Ocuvera's IScreensaverScene, not implemented yet.
ValueTask StopAsync() => ValueTask.CompletedTask;
```

Native scenes retain Attach/Detach/Resize/Update/Dispose and inherit this no-op
unless they later need asynchronous cleanup. The host unsubscribes rendering,
awaits StopAsync, then detaches/disposes. After Dark's override cancels and awaits
its worker, observes the result or error and releases its cancellation source.
Repeated stop requests share one completion; synchronous Dispose is not a place
to block the dispatcher or abandon active work.

Propagate this through scene host, windows, manager rotation and normal exit.
Serialize rotations so timer ticks, failure recovery and exit cannot launch
overlapping replacements. Stop the timer before awaiting. Route mouse/key and
window-close exit requests through an idempotent coordinator, await all guests,
then call Application.Shutdown. `OnExit` is too late to initiate normal async
cleanup. Ocuvera already uses `OnExplicitShutdown`, which helps this design.

A small runtime-owned `AfterDarkPlayer` could wrap the current worker, mailbox,
completion task and idempotent StopAsync/IAsyncDisposable ownership, avoiding a
copy of our WPF window's lifetime code. This is convenience around the existing
engine, not a new engine interface. Keep `IAnimationSession` for tutorials and
tests. Observe startup/drawing failures during playback, not only at shutdown.
Bound retries to remaining eligible artifacts, exclude failures for the process,
and ask the manager to continue to another scene when the AD pool is exhausted.
A managed failure is recoverable; an in-process native crash is not isolated.

Acceptance: rotate Spheres -> Whitney -> Spheres; exit during initialization,
drawing and an intermediate-image hold; race repeated stop/rotation requests.
Use fake workers for deterministic lifecycle tests and local modules for real
acceptance. Worker completion and zero retained guest resources are required.

## Work package 3: discover files and their actual resources

Add a configured external module directory to Ocuvera settings. Scan off the UI
thread, identify by hash and deduplicate identical files before random selection.
Preserve distinct revisions; only supported hashes enter the pool. Report missing,
unreadable and unsupported inputs without breaking native scenes. If the pool is
empty, omit After Dark from random eligibility; forced selection gets a clear
diagnostic. Rescan on configuration change/explicit refresh, not every render tick.
Revalidate the actual bytes at load so a file change after discovery is handled.

Expose a small typed supported-artifact catalog from Runtime: stable identity,
display name, hashes, valid dimensions and default options. Discovery must not
copy our hash switch or infer support from a filename. For the first playback
proof, one explicitly supplied path is sufficient; catalog/discovery follows.

**Current resource handling is already inside the runtime.** AfterDarkSession
constructs a NeResourceCatalog from the supplied file bytes; LoadBitmap resolves
resources from that catalog. Supported fixed profiles use these embedded
resources or host-generated starting images. They do not require shipping the
original AD launcher, extracting bitmap files or locating Windows DLLs.

Future required helper DLLs/external data should become explicit dependencies
of a supported artifact, resolved from its configured collection root and
validated by identity. Finding a helper on disk does not implement Win16 helper
loading; do not claim that packaging solves unimplemented modules. Add the
dependency resolver when a supported profile actually needs it. Never distribute
the owner's original AD files in NuGet or embed them in the `.scr`.

## Work package 4: present frames on the primary monitor

Start with a 640x480 guest, scaled with preserved aspect ratio into the Canvas.
All currently supported profiles accept that resolution. A monitor resize/DPI
change adjusts presentation only; it does not mutate guest memory dimensions.
Keep grayscale/audio out of this integration and preserve fixed profile options.

Use the existing intermediate-frame worker path so Rainstorm/Zot! flashes survive
their guest draw calls. Do not replace it with a loop that only snapshots after
DRAWFRAME. Current bounded mailbox/hold semantics remain; a stalled UI is not
guaranteed to see every transient frame.

The owner's September 30 decision is firm: **original AD playback only runs on
the primary monitor**, not as a temporary first step. Use Ocuvera's existing
**PrimaryOnlyWithBlanking** mode: one guest, one worker and one mailbox consumer;
secondary displays remain black. Ocuvera retains its other monitor modes for
native scenes.

No per-monitor guest creation, mirrored playback, frame fan-out, shared-image
broadcasting or cross-monitor seed coordination is required. Test a machine with
multiple displays to verify correct primary placement and secondary blanking,
not to exercise simultaneous AD playback. Preserve guest resolution independently
of the primary display's physical size and DPI.

Do not turn all existing native scenes into async renderers. Only the lifetime
needs awaiting; their current render callbacks can stay synchronous.

## Delivery sequence and effort boundary

1. **Installed executable proof:** project-referenced library, complete folder
   publish, installed `.scr` execution and explicit apphost configuration. Moderate build work;
   highest uncertainty until publish execution succeeds.
2. **One original in the collection:** awaited lifetime, minimal scene adapter,
   configured Spheres file, primary-display playback and rotation to native
   scenes. Rendering glue is small; lifecycle coordination touches several files.
3. **One random After Dark entry:** typed catalog, directory setting, deduplication,
   inner selection and bounded failure handling. No new GDI behavior required.
4. **Personal deployment acceptance:** Visual Studio LocalAppData publish,
   resource-bearing Nocturnes/Gravity, Rainstorm/Zot!
   intermediate images, all available supported profiles, primary placement and
   secondary blanking,
   repeated rotations and final published `.scr` soak (memory, handles, CPU,
   shutdown). Public tests remain independent of proprietary inputs.

This is bounded integration work, not another Win16 compatibility project.
Do not promise a calendar duration before the apphost/CFG/folder-publish experiment;
that is the unresolved step most likely to change the estimate. The library
boundary and adapter can be exercised with today's 18 supported profiles without waiting
for all 26 v1 selections. Completion of the remaining selections is a separate
track, and this assessment does not silently reorder the owner's milestone.

**Durable model:** Ocuvera chooses the slot and owns windows; the After Dark entry
chooses an original; After Darker owns execution; copied frames and awaited stop
connect them.
