# Live After Dark player

Open `AfterDarker.sln`, set **AfterDarker.Wpf** as the startup project, and press
F5. With the analyzed `ad/Mondrian.ad` present, the window starts automatically.
Choose **File > Load AD file…** to select **Mondrian**, **Spiral Gyra**, or **Rainstorm**. Loading
starts playback automatically; the menu can also switch modules while playing.
The previous guest shuts down before the new one starts. Unsupported files or
versions are rejected by their content hash before stopping an active guest.
Stop lets you change speed and Run a fresh guest. No original modules are distributed.

Rainstorm uses fixed strength/lightning/drops/wind controls (`60/50/52/40`);
its speed selector is disabled because that module has no speed control. Rain
is visible, but its two inversions within a DRAWFRAME are presented only after
both complete, so the brief lightning image is not displayed. See the
[Rainstorm evidence and presentation boundary](research/rainstorm-execution.md).

From the repository root:

```powershell
dotnet run --project src/AfterDarker.Wpf --no-launch-profile
# Or supply a path:
dotnet run --project src/AfterDarker.Wpf --no-launch-profile -- C:\path\Mondrian.ad
```

Both supported modules execute their original, hash-checked Win16 code through
`AfterDarkSession<TState>`. Mondrian's tutorial facade uses that same runtime.
Spiral adds five pen/line imports; see the [execution notes](research/spiral-gyra-execution.md).
The file picker accepts AD files generally, but only the two analyzed versions
are executable today. A renamed supported file works; an unknown file named
Mondrian.ad does not bypass validation.

## Ownership and presentation

```text
serialized background task                 WPF dispatcher
  IAnimationSession                             WriteableBitmap
  original DRAWFRAME                          640 x 480 RGB24
  reusable RGB buffers                       reusable RGB buffer
        -> one pending frame (locked copy) -> WritePixels -> Image
```

`AfterDarkPlayback` is the worker; it never accesses WPF objects. Awaiting its
pacer may move it between pool threads, but no two guest calls overlap. The
mailbox atomically transfers pixels with their counters. A slow presenter
receives the newest pending frame; it cannot accumulate stale queued frames.
An unchanged guest image does not produce another transfer.

The UI owns the bitmap and calls
[`WritePixels`](https://learn.microsoft.com/en-us/dotnet/api/system.windows.media.imaging.writeablebitmap.writepixels?view=windowsdesktop-10.0)
on its dispatcher, respecting WPF's
[thread affinity](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/threading-model).
Window resizing scales the fixed 640x480 guest image with nearest-neighbor
sampling; it does not change the dimensions the module sees.

Live `GetTickCount` follows monotonic elapsed milliseconds. DOS date/time and
the initial random seed derive from civil time captured at session creation.
The host requests approximately 60 draws per second, accounting for time
already spent drawing; it never issues a catch-up burst. The original module
still decides whether a call should draw, according to its speed setting.
Timing, initial seed, scaling and presentation cadence are modern host choices,
not proof of original After Dark pacing. Console captures retain fixed time.

Diagnostics retain only the latest 256 records per kind, with lifetime totals.
Native invocation budgets still apply independently to every guest call.
The executable has the same narrowly scoped `/GUARD:NO` apphost adjustment as
the tutorials; see [the engine setup](tutorials.md). No new NuGet dependency is
needed; WPF ships in the .NET Windows desktop framework.

## Local acceptance run

```powershell
dotnet run --project src/AfterDarker.Wpf --no-launch-profile -- --smoke ad/Mondrian.ad artifacts/wpf-smoke/manual
```

This opt-in mode drives the real WPF dispatcher and presenter, waits for 30
presentations, reads the bitmap back to check exact published RGB bytes, and
saves `frame.png`, a rendering of the window's own content (`window.png`), and
`report.json`. Outputs stay in ignored local artifacts. A nonzero exit signals
failure. This proves actual WPF presentation of original-code output; it does
not establish Windows 3.1 visual fidelity or indefinite endurance.

## Stop, close and restart

Stop cancels the host loop and pacing wait. An in-flight guest call is allowed
to finish under its existing instruction and time budgets. The worker then
calls original MODULE(CLOSE), followed by WEP(1), verifies their return frames
and balanced locks, and releases Unicorn. Closing the window awaits that same
worker asynchronously; the dispatcher remains free to process events. Run
creates fresh guest memory, timing, handles and pixels each time.

Cancellation is deliberately observed between guest calls. Interrupting a call
halfway through would leave its stack unsuitable for another CALL FAR to CLOSE.
If execution or cleanup fails, no further guest calls are attempted, and the
native engine is still disposed. The UI shows the symbolic failure. Cleanup
ignores the cancelled pacing token but retains bounded native execution: 50,000
instructions per Mondrian invocation or 200,000 for Spiral Gyra/Rainstorm, one-second
native slices and a five-second
cumulative native execution budget. These are cooperative runtime safeguards,
not an out-of-process watchdog for a defective native library.

CLOSE uses the existing MODULE ABI (three WORD arguments, RETF 6). WEP is a
separate export with one WORD argument and RETF 2; its AX=1 is verified. Mondrian CLOSE
may service 200 saved rectangles plus blanking and locks, so its service budget
is explicitly 208; its other calls retain 128. Spiral uses 768 services because
one invocation can perform 30 drawing iterations. Its CLOSE restores the original
pen and deletes its allocated pens; shutdown checks that none remain.

Rainstorm allows 1,024 service exits per invocation because its 52 drops each
perform point tests and pen/line operations. Its peak owned-pen count was one
in the 300-draw verification; its stock black pen is host-owned.
With the current system record, CLOSE optionally clears then inverts the saved
rectangles; it does not necessarily leave black pixels. The UI retains the last
presented frame after Stop. Console lessons still end at their original boundary
and dispose without running CLOSE/WEP.

The acceptance run also stops, verifies CLOSE/WEP, starts a fresh session,
presents three more images, and closes the real window while that guest is
active. Both shutdowns must complete, WEP must return one, and no global locks
may remain. A 30-second initial-presentation timeout and bounded restart/close
waits make acceptance failures diagnosable. This is a bounded lifecycle proof,
not a multi-hour endurance test.


To launch Spiral Gyra directly:

```powershell
dotnet run --project src/AfterDarker.Wpf --no-launch-profile -- "ad/Spiral Gyra.ad"
```

To verify switching in the actual WPF application, give smoke mode a final
optional destination module. It validates Stop/restart, then invokes the same
load routine used by the menu while playback is active:

```powershell
dotnet run --project src/AfterDarker.Wpf --no-launch-profile -- --smoke "ad/Spiral Gyra.ad" artifacts/wpf-smoke/spiral-switch ad/Mondrian.ad
```

The acceptance run also checks that unsupported content leaves the active guest
untouched. `report.json` records the module names, completed CLOSE/WEP phases,
and remaining/peak pen counts. The native file dialog itself is not automated.
