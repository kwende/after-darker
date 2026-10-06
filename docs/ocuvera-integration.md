# Original After Dark modules inside Ocuvera

Implemented October 4, 2026. Ocuvera now has one **After Dark (original modules)**
entry alongside its existing native recreations. This is original Win16 execution
inside the x64 `.scr`, through the same runtime used by our tutorials and WPF player.

## Use it

Publish Ocuvera with its **LocalAppData** profile as described in
[the deployment guide](localappdata-publishing.md). Open its settings (launch
`screensaver.exe /c`), select **After Dark (original modules)**, choose the folder
containing your original `.AD` files, and optionally click **Check supported originals**.
Leave the entry enabled and save. The next screensaver startup scans that folder
recursively. Native recreations retain their separate entries and settings.

To try the After Dark entry immediately instead of waiting for the outer random bag:

```powershell
& "$env:LOCALAPPDATA\OcuveraToasters\screensaver.exe" /s /debug /module:after-dark
```

Escape exits debug playback; normal playback also exits on mouse/key input.
Use the executable for command-line arguments: shell association launching of a
`.scr` can substitute `/S`. Windows can still use the adjacent `ocuvera.scr` normally.
Windows screensaver registration and timeouts are separate from publishing.

The path is saved as `AfterDarkModuleDirectory` in
`%LOCALAPPDATA%\ocuvera-screensaver\settings.json`, outside the installation folder.
Set this on each machine; no checkout path is compiled into the player. Missing
or unsupported inputs remove After Dark from random eligibility without affecting
native scenes. Forced selection with an empty pool reports a setup error.

## Ownership and selection

```text
Ocuvera's persisted outer random bag
  -> After Dark entry (one slot, 3–7 minute duration)
  -> process-local shuffled bag of distinct supported original hashes
  -> AfterDarkPlayer -> existing AfterDarkPlayback worker -> Win16 guest
  -> copied RGB24 frames -> one mailbox consumer -> WPF WriteableBitmap
```

The runtime's `SupportedModules.Artifacts` owns stable original IDs, names and
hashes. Ocuvera does not maintain another hash switch. Discovery deduplicates
identical bytes; a second copy does not make that original more likely. Distinct
supported revisions remain distinct inputs. The actual bytes are revalidated at
load to catch a changed file. Scans skip reparse points/unreadable subfolders and
bound file size, depth and visited file count; skipped-input diagnostics are local.

An execution failure excludes that revision for this process and tries the next
available original. Exhausting the pool asks Ocuvera to continue with a native
scene. This is bounded recovery from managed exceptions, not native-crash isolation.
Discovery and playback failures are recorded in
`%LOCALAPPDATA%\ocuvera-screensaver\after-dark.log` (bounded rolling log).

Current supported resources are embedded in the module and decoded by the shared
runtime. No AD launcher, Windows DLL collection or external helper process is
introduced. No proprietary files are copied into builds or publish output.

## The lifetime invariant

**No replacement guest starts until the previous worker has completed.**

Ocuvera's `IScreensaverScene.StopAsync` defaults to immediate completion for
native scenes. The After Dark scene cancels pacing and awaits its worker. The
existing worker finishes the active bounded guest call, executes CLOSE/WEP from
a valid guest stack, and disposes Unicorn. Faulted guest execution skips unsafe
guest cleanup but still releases the native engine.

The scene host first unsubscribes rendering, awaits stop, then detaches/disposes
presentation. Windows close after that. The manager serializes rotation with
exit; exit prevents a pending rotation from creating another scene. Mouse/key
and window-close requests enter this awaited path before Application.Shutdown.
Repeated stop requests share completion. Nothing blocks the WPF dispatcher waiting
for a guest, and guest execution never occurs in a render callback.

## Presentation adaptations

- One 640×480 color guest, aspect-preserving nearest-neighbor scaling with black
  unused space. Resize changes the image presentation, not guest memory dimensions.
- Primary monitor only via `PrimaryOnlyWithBlanking`. Normal playback blanks
  secondary monitors; Ocuvera's debug mode deliberately omits secondary windows.
- Existing fixed profile controls, silent audio policy, and generated white/pattern
  backgrounds remain. No new Win16 APIs were needed.
- Rainstorm/Zot! intermediate image checkpoints flow through the original worker
  and mailbox. The bounded latest-frame mailbox can drop images if the UI stalls;
  it does not promise every transient frame is presented.

## Evidence and reproduction

The published LocalAppData `.scr`, started directly from another working directory,
passed the real window/scene/rotation acceptance for **all 18 available supported
originals**. For each: present at least eight images, rotate to native Whitney,
verify clean guest cleanup, rotate back, present again, then race repeated stop
with advance. All returned CLOSE=0/WEP=1 with no retained guest locks or owned GDI/
local-heap allocations. Rainstorm presented an intermediate flash and Zot! presented
seven in the observed first passes. Source-owned tests separately prove ordering
with a deliberately unfinished worker, empty-pool eligibility and bag/exclusion rules.

- After Darker: **357 public tests passed**.
- Ocuvera: **6 public tests passed** with `dotnet run --project tests/Ocuvera.Tests/Ocuvera.Tests.csproj`.
- Local acceptance report/pixels: ignored `artifacts/ocuvera-integration/`.
- Ordinary configured launch (not the acceptance command) selected Magic from
  the saved folder setting. A window-close request completed guest CLOSE/WEP and
  exited the installed process. The local folder setting was added while preserving
  existing settings; Windows screensaver registration was not changed.
- The Ocuvera opt-in `--verify-after-dark-playback <collection> <output-directory>`
  command exercises the installed host and writes a local report and PNGs. Launch
  with direct process creation or the `.exe`; outputs are private original-module media.

This is short playback/rotation proof, not a long-running soak or historical visual
fidelity proof. Physical multi-monitor placement/blanking, second-machine execution,
and the Visual Studio Publish UI remain to be checked. The CLI did exercise the
checked-in publish profile. Twelve milestone selections still lack original-code
runtime support; integration does not make unknown revisions runnable.

**Mechanism to remember:** Ocuvera owns selection and windows; After Darker owns
the guest; copied frames and awaited stop connect them.
