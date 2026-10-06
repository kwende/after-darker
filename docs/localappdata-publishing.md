# Publish from Visual Studio to LocalAppData

Both applications have a checked-in **LocalAppData** folder-publish profile.
No administrator session, installer, System32 copy or package feed is required.
The profiles publish Release / Windows x64 / framework-dependent applications.

| Project | Destination | Executable |
| --- | --- | --- |
| AfterDarker.Wpf | `%LOCALAPPDATA%\AfterDarker` | `AfterDarker.Wpf.exe` |
| Ocuvera `screensaver` | `%LOCALAPPDATA%\OcuveraToasters` | `ocuvera.scr` |

## Use it

1. Install Visual Studio's .NET desktop workload and the .NET 10 Windows Desktop
   runtime/SDK. The **Desktop development with C++** workload supplies `editbin`
   for the existing Unicorn compatibility step.
2. On each machine, keep `after-darker` and `ocuvera-toasters` as sibling checkouts.
   For another layout, set the Ocuvera MSBuild property `AfterDarkerRoot` to the
   After Darker checkout, for example through an untracked project `.user` file.
3. Right-click the executable project in Visual Studio, select **Publish**,
   choose **LocalAppData**, then **Publish**. If the profile is not selected
   automatically, select the existing `Properties/PublishProfiles/LocalAppData.pubxml`.
4. Run the executable from the destination above. Stop any running copy before
   republishing, since Windows can lock loaded files.

The `.scr` is the screensaver executable; After Darker's WPF app remains a
standalone player. Select the installed `ocuvera.scr` as your Windows screensaver
once (its Explorer **Install** context-menu action opens screensaver settings).
Publishing does not change the selected screensaver, idle timeout or lock settings.
The user can apply those settings separately; no registry mutation is hidden in a build.

Equivalent CLI commands, each from its own repository root:

```powershell
# After Darker
dotnet publish src/AfterDarker.Wpf/AfterDarker.Wpf.csproj -c Release -p:PublishProfile=LocalAppData

# Ocuvera Toasters
dotnet publish screensaver/screensaver.csproj -c Release -p:PublishProfile=LocalAppData
```

Keep AD files in a separate local collection; select them by absolute path in
the standalone player. Neither profile copies proprietary inputs or deletes
the destination wholesale. Ordinary F5 builds remain under the checkout's
`bin` directory; only Publish updates LocalAppData. Native dependencies resolve
beside the installed executable, independent of its working directory.

## What the build does

Runtime's `unicorn.dll` item explicitly copies to both build and publish output.
Ocuvera references Runtime, which references Core and the managed Unicorn binding.
No source is copied into Ocuvera, and installed apps do not need the checkout.

The executable projects explicitly import
[`AfterDarker.UnicornAppHost.targets`](../build/AfterDarker.UnicornAppHost.targets).
Referencing Runtime alone does **not** modify a consumer executable. The imported
targets invoke [`configure-unicorn-apphost.ps1`](../tools/configure-unicorn-apphost.ps1)
against the consuming project's named executable directly in its output directory.
The script rejects the shared `dotnet.exe`, runs `editbin /GUARD:NO`, and checks
that the final PE header's `GUARD_CF` flag is clear. It does not change Windows
mitigation settings. See [the original failure](tutorials.md#dependencies-and-windows-setup).

Build and Publish can produce different apphost files, so each final output is
configured. Ocuvera's `.scr` copy targets explicitly depend on configuration:

```text
generate executable -> configure that executable -> copy identical bytes to .scr
```

This ordering prevents a freshly generated, unconfigured executable from
replacing the working `.scr`. The earlier restricted tutorial/test build script
remains in place for those hosts. Single-file publishing is not required and
was not revalidated in this change.

## Installed-process diagnostic

From the Ocuvera checkout:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/verify-after-dark-installation.ps1
```

This launches the installed `.scr` directly with `--verify-after-dark`, from a
temporary working directory, without opening screensaver windows. It executes
source-owned 16-bit instructions, stops before ADD through a native code hook,
resumes and checks AX=12. It writes a uniquely named JSON report in the user's
temporary directory and fails on a bad exit/result or a 20-second timeout.

The script uses `UseShellExecute=false` intentionally. During verification,
shell association launching of `.scr` substituted `/S` for the diagnostic
arguments and started normal playback. Direct process creation preserves the
arguments and tests the exact installed executable. An unmanaged fail-fast may
leave no JSON report; the exit status/missing report is still a failed check.

## Verified October 4, 2026

- Both LocalAppData profiles published successfully on the current machine.
- Core, Runtime, managed Unicorn and the verified native DLL reached the consumer
  directory. Ocuvera's `.exe` and `.scr` were byte-identical with CFG disabled.
- The installed `.scr` diagnostic passed: x64 process, hook stop/resume and AX=12.
- The published WPF player ran private Spheres from another working directory;
  frame readback, stop/restart and close while playing passed, with clean CLOSE/WEP.
- **354 public tests passed**, including the new source-owned execution probe.
- CLI publish used the checked-in Visual Studio profiles. The Visual Studio
  Publish UI itself and the second machine were not exercised.

This records the deployment increment. The subsequent
[original-module integration](ocuvera-integration.md) now supplies the random After
Dark entry, module-folder setting, and awaited rotation/exit. No new Win16 API was
introduced by either increment.
