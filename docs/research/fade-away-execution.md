# Fade Away: erasing a host-provided image

Verified on 2026-09-19. Supported artifact SHA-256:

`D224AF9A0EA75F3B4C24A837F9743B13851D3ACB03BE3607E0C9094DF6E28B21`

The WPF player now loads this original Fade Away module using its **Radar**
effect (control 1 = 4). It starts with the all-white image requested by the
owner. The original Win16 code chooses and draws every line that erases it.
This increment supports one fixed effect, not all seven Fade Away styles.

## Initial pixels belong to the host

The module needs an existing image to change. Previously every surface started
black, which hid the black-on-black work in several Fade Away paths.

The new boundary is explicit:

```text
profile creates initial RGB image
    -> session copies it to the software surface once
    -> original startup / INITIALIZE / BLANK
    -> original DRAWFRAME calls modify the same surface
    -> original completion leaves the surface black
```

[ModuleProfile.CreateInitialPixels](../../src/AfterDarker.Runtime/Modules/ModuleProfile.cs)
returns null for existing profiles, preserving their black start. Fade Away
returns a white RGB buffer. [PixelSurface.LoadRgb](../../src/AfterDarker.Core/Rendering/PixelSurface.cs)
validates its length and copies it without retaining the caller's storage.
The image is not restored during DRAWFRAME, BLANK, or completion. A fresh session
creates a new white image.

This is a **modern host input**, separate from guest drawing. A future real
screensaver host can capture a desktop image and prepare matching top-to-bottom,
tightly packed RGB pixels for this same loading path. Desktop capture, scaling,
fullscreen `.scr` integration, and multi-monitor capture are not implemented
here. No desktop screenshot is taken by this increment.

## What changed in Windows support

Radar uses the stock black pen and the existing line and rectangle-fill services.
**No new Windows API behavior was needed.** Rainstorm's stock-pen addition was
already sufficient for the observed drawing path.

The DLL also imports `GDI!Ellipse` (#24), `GDI!Rectangle` (#27), and `GDI!PatBlt`
(#29) for its other styles. Their identities come from Wine 10.0's
`gdi.exe16.spec`, documented with the [import census](ad-import-census.md).
They now have named registry entries with `Handler.Unsupported` and no guessed
argument/return layouts. The loader can relocate their addresses, but the
gateway refuses to execute them if reached. Public native tests exercise all
three failures before any argument read or guest result store.

Binding an import is not the same as implementing it. No blanket unknown-import
fallback or fabricated success response was added. Squares, Slow Zoom, Dissolve,
and other effect choices remain outside the public profile even where the
earlier sweep observed a short successful path.

## Radar is a finite two-pass eraser

**Artifact facts:** initialization at S2:001C–0082 saves style 4, resets the
finished flag, sets an edge step of 2, and starts the endpoint at `(width/2, 0)`.
Radar at S2:026D–037A selects the stock black pen and draws from the surface
center to that endpoint. It moves around the rectangular perimeter. On finishing
the first circuit it changes the step to 1. On finishing the second circuit it
sets its completion flag and calls the plain black fill. S2:00AA–00B3 then makes
later DRAWFRAME calls return without issuing more drawing operations.

**Observed runtime behavior:** white becomes a growing radial pattern of black
lines. After both passes the guest performs one full black fill. Further draws
and the original BLANK handler keep the image black; the host does not restart
the effect. Closing and starting a fresh session produces white again.

| Surface | Draws to original completion | Instructions through completion |
| --- | ---: | ---: |
| 320x240 | 1,696 | 259,255 |
| 640x480 | 3,376 | 515,335 |
| 1x1 | 22 | 4,081 |
| 2048x2048 | 12,304 | 1,875,559 |
| 321x239 | 1,694 | 258,948 |

At 640x480, 3,376 original LineTo calls and one FillRect complete the fade. No
owned pens are allocated: the selected black pen belongs to the host. As with
the other modules, requested presentation cadence is a modern adaptation;
these draw counts do not establish the original machine's timing.

## Where the module-specific knowledge lives

[FadeAwayProfile](../../src/AfterDarker.Runtime/Modules/FadeAwayProfile.cs)
supplies the white image, style 4, SDK-sized 34-byte module storage and a 24-bit
system color field. The other controls are zero. Shared compatibility-tail
words remain unchanged. There is no original speed control for this effect,
so the WPF speed selector is disabled and its title identifies Radar.

[FadeAwayState](../../src/AfterDarker.Runtime/Modules/FadeAwayState.cs) decodes
the guest globals without calculating animation state in the host:

| Data offset | Meaning |
| --- | --- |
| `0018` | Compatibility flag written by the dispatcher at S1:00BD |
| `03A0` | DLL instance saved by startup at S1:0017 |
| `039E` | Style saved from module control 1 |
| `0010` | Finished flag |
| `0394` | Edge step, initially 2 and then 1 |
| `03A2` | Current edge stage, 0 through 4 |
| `039A`, `039C` | Endpoint X and Y |
| `0396`, `03A4` | System and module far pointers from GlobalLock |

The profile verifies the initialized style, start point, step, completion flag
and record pointers. Execution, stack returns, startup conventions and cleanup
continue through the shared session. Normal limits remain 50,000 instructions
and 128 managed exits per invocation, plus existing time bounds.

## Verification and limits

- Three new public image-buffer cases verify RGB order, owned copies, revision
  tracking and rejection of wrong lengths without mutation. Three native gateway
  cases verify the unused-import guards.
- Seven opt-in Fade Away cases cover full completion at five sizes, coarse/fine
  passes, partially erased intermediate images, all-black completion, 20 later
  idle draws, BLANK after completion, deterministic independent guests, white
  restart, shutdown before drawing and changed-artifact rejection.
- All **274 tests** passed with the compiler and all four original-module
  suites enabled. The public suite contains **209 cases**.
- Actual WPF acceptance presents 30 frames, checks exact bitmap RGB readback,
  rejects unsupported input while playing, stops/restarts, switches to and from
  Rainstorm and closes during playback. Both directions end with zero locks or
  owned pens and successful CLOSE/WEP. The captured Radar image was inspected.
  A separate standalone run also identifies `Module: Fade Away` and
  `SwitchedFrom: null`, avoiding ambiguity with the module-switching checks.
  These short window checks do not claim full-fade GUI timing; full completion
  is verified through the same runtime in the private integration tests.

```powershell
$env:AFTER_DARKER_FADE_AWAY = (Resolve-Path 'ad/Fade Away.ad').Path
dotnet test -p:TestLocalFadeAway=true --filter "TestCategory=LocalFadeAway"
dotnet run --project src/AfterDarker.Wpf --no-launch-profile -- --smoke "ad/Fade Away.ad" artifacts/wpf-smoke/fade-away-only
dotnet run --project src/AfterDarker.Wpf --no-launch-profile -- --smoke "ad/Fade Away.ad" artifacts/wpf-smoke/fade-away-to-rainstorm ad/Rainstorm.ad
dotnet run --project src/AfterDarker.Wpf --no-launch-profile -- --smoke ad/Rainstorm.ad artifacts/wpf-smoke/rainstorm-to-fade-away "ad/Fade Away.ad"
```

The artifact remains hash-gated and private; images/reports remain ignored.
This is proof of the original Radar path with supplied white pixels, not every
effect, a desktop capture implementation, other module revisions, or historical
pixel/timing fidelity. Work stops after this module for the owner's review.
