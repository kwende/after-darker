# Spheres: original shading bands in the production player

Verified September 28, 2026. Spheres now has a production profile in the shared
runtime and WPF player. No new Win16 API or raster implementation was needed.
The original module chooses every sphere, shade and ellipse; C# supplies its
host records and renders the resulting GDI operations.

## Artifact and fixed configuration

- Private input: `ad/windows98-2026-09-20/AFTERDRK/AD30/SPHERES.AD`.
- SHA-256: `E9F416BB9DE55020522C77FF45C4D2DD22F6DD80C96C52FE0EABE400859A453F`.
- Resident name: `SPHERES`; five NE segments, automatic data in segment 5.
- Controls: **Max Size 55, Offset 5, Clear Every 100, Clear Screen First enabled**.
- Host: 24-bit color, square pixel aspect (`1:1`), shared frame pacing.
- Supported guest dimensions: the shared `1..2048` range on each axis. Tiny
  surfaces are valid clipped drawings, not a useful viewing configuration.

The [profile](../../src/AfterDarker.Runtime/Modules/SpheresProfile.cs) binds these
inputs to this exact artifact. The [state record](../../src/AfterDarker.Runtime/Modules/SpheresState.cs)
exposes detached observations for diagnostics and tests, without taking animation
ownership away from the guest. No heap, loader, import ABI or presentation
interface changes were required; the Ocuvera adapter requirements are unchanged.

## The mechanism: a frame is one band

Static disassembly provides these anchors; runtime tests confirm the cycle:

1. `S2:0012` reads the controls and resets the sphere counter. `S2:01DF` chooses
   a color family, radius and center for the first sphere. With Max Size 55,
   the radius is `random % 55 + 30`, giving 30 through 84 pixels.
2. `S2:00B5` handles DRAWFRAME by calling the band renderer at `S2:032F`.
   It selects a null pen and creates/selects a solid brush. Each call draws one
   ellipse, restores the old objects and deletes the temporary brush.
3. `S2:04A8..04D3` advances the band index and shrinks the radius using the
   module's table. After band 33, the guest chooses the next sphere. Thus
   **34 DRAWFRAME calls complete one sphere**. Existing spheres remain visible
   underneath subsequent ones.
4. `S2:00C2..0109` detects the transition to sphere 101, reinitializes the
   cycle and clears to black. With this profile, draw 3,400 ends in a black
   image and draw 3,401 starts the next sphere. This is guest behavior, not a
   host frame counter or synthetic scene restart.

The guest seeds its CRT random generator during startup. Deterministic session
time reproduces state and pixels; normal WPF playback uses the session's live
startup time. The host's shared pacing means about 0.57 seconds per sphere at
60 DRAWFRAME calls per second, before scheduling delays. This is a modern
pacing choice, not a measurement of the original After Dark launcher.

Relevant offsets in this artifact's automatic data segment:

| Offset | Meaning |
| --- | --- |
| `009E` | PREINITIALIZE compatibility flag |
| `00E2` | 32-bit random-generator state |
| `042A` | Original radius of the current sphere |
| `042C` | Saved HDC |
| `0432` | AD_SYSTEM far pointer |
| `0438` | One-based sphere number since clearing |
| `043E` | Next shading band, 0 through 33 |
| `0444` | DLL instance handle |
| `044C`, `044E` | Sphere center X and Y |
| `0450` | AD_MODULE far pointer |

## Primary-palette policy and fidelity boundary

BLANK returns `13`, the recovered SDK's `PRIMARY_PAL` request. The profile
explicitly accepts that reply. The common default still rejects unexpected
nonzero replies for other modules.

This is a **modern adaptation**: our host advertises 24-bit color and the existing
hand rolled GDI renders the RGB components of `PALETTERGB` directly. We do not
create a historical indexed palette or manufacture a guest palette handle.
The tested guest path does not require one. Supplying square pixel aspect also
matters: the band renderer divides horizontal aspect by vertical aspect; a
zero-filled aspect record would divide by zero.

Captured images were visually inspected and contain overlapping, shaded colored
spheres. That verifies coherent output, not pixel-for-pixel agreement with the
original Windows palette or ellipse rasterizer. Other controls, grayscale and
the original configuration dialog are outside this supported profile.

## Verification and reproduction

The public suite plus six opt-in Spheres cases passed: **359 tests, no failures
or skips**. The tests cover:

- 3,401 original draws: 100 complete spheres, periodic black clear and resumed
  drawing; 1,126,111 instructions before shutdown, 204 distinct guest colors.
- Exactly 3,401 ellipse, brush-create and brush-delete calls; peak one owned
  brush and none retained between draws.
- Independent deterministic guests with matching state/pixels, unrelated speed
  settings ignored, and one guest continuing after the other shuts down.
- `1x1`, `321x239` and `2048x2048` clipping/dimensions; invalid dimensions and
  modified artifacts rejected before execution.
- CLOSE/WEP, restored stack and host DS, no outstanding locks or local allocations,
  and repeatable shutdown.

```powershell
$env:AFTER_DARKER_SPHERES = (Resolve-Path ad/windows98-2026-09-20/AFTERDRK/AD30/SPHERES.AD).Path
$env:AFTER_DARKER_SPHERES_CAPTURE = Join-Path $PWD artifacts/spheres/captures
dotnet test --project tests/AfterDarker.Tests -p:TestLocalSpheres=true --report-trx
```

WPF built with zero warnings/errors. Its smoke run verified copied pixel readback
from the WriteableBitmap, unsupported-file rejection without disturbing playback,
stop/restart, switching from Spheres to Shapes, and close while playing with
clean shutdown. The first run had 125 draws / 124 changed images at capture.

```powershell
dotnet build src/AfterDarker.Wpf
& src/AfterDarker.Wpf/bin/Debug/net10.0-windows/win-x64/AfterDarker.Wpf.exe --smoke ad/windows98-2026-09-20/AFTERDRK/AD30/SPHERES.AD artifacts/spheres/wpf ad/Shapes.ad
```

Choose the **Spheres** launch profile for F5, or use **File > Load AD file…**.
Private evidence stays under ignored `artifacts/spheres/`: selected draw PNGs
under `captures/`, and WPF `report.json`, `frame.png` and `window.png` under
`wpf/`. Original module bytes are never copied into test output or distributed.

This closes Spheres' production-runtime work in Milestone 1. Ocuvera integration
remains a separate pending milestone phase.
