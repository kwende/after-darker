# Readiness audit of the extracted Windows 98 collection

Date: 2026-09-20. The input is the preserved collection described in the
[extraction inventory](windows98-collection-inventory.md). This audit uses the
working tree after the four bitmap modules, including the existing local heap,
resource decoder, bitmap/DC support, polygons and clipping regions.

**Result: sixteen modules already work through the production player; Spheres
and Warp are two additional candidates that rendered 600 calls and shut down
using existing Win16 implementations.** Neither new candidate is registered in
SupportedModules or offered in WPF yet. Each still needs a checked profile and
integration tests; Warp also requires a lifecycle step omitted by the current
session. No production loader, Windows service or rendering implementation was
changed for this audit.

## Counts and what they mean

The extracted folder contains 65 distinct module hashes: 29 duplicate the
previous collection and 36 are additional modules. The original copies remain
untouched. Module paths below are relative to the extracted AFTERDRK directory;
AD20/AD30/MAD identify source folders, not certified pristine installer releases.

| Scope | Result |
| --- | --- |
| 16 already-supported AD20 modules | Actual SupportedModules.Open profiles completed initialization, 30 draws and shutdown |
| 13 previously unsupported AD20 modules | 11 still stop in the loader; GraphStat and Sounder reach unsupported initialization services |
| 2 additional AD30 modules | Spheres and Warp completed extended diagnostic drawing and clean shutdown with explicit host choices |
| 2 other additional AD30 modules | Puzzle reaches drawing; Satori reaches initialization; both need additional Windows behavior |
| 32 other additional modules | First failure remains in loading: 24 additive relocations, 5 OS fixups, 2 export prologues, 1 segment-count limit |

These are first failures, not exhaustive implementation estimates. Fixing a
loader case may expose another missing behavior. In particular, a file that
imports only familiar functions can request an unsupported raster mode or
calling convention when it executes.

The [machine-readable audit](windows98-readiness-audit.json) contains all 65
hashes, classifications, current registry hash, guarded imports and the selected
extended-probe results. Detailed traces and images remain local under ignored
`artifacts/windows98-audit/`.

## The two inexpensive additions

### Spheres: existing ellipse drawing plus a primary-color request

Artifact SHA-256:
`E9F416BB9DE55020522C77FF45C4D2DD22F6DD80C96C52FE0EABE400859A453F`.

**Observed:** DLL startup, PREINITIALIZE and INITIALIZE succeed. BLANK returns
13, which the recovered SDK defines as PRIMARY_PAL. The strict baseline stops
there rather than silently accepting an unknown host request.

An explicit diagnostic continuation accepts that request on the RGB24 surface,
analogous to the existing direct-RGB adaptations for other modules. It does not
manufacture palette handles or implement indexed palettes. The original code
then uses CreateSolidBrush and Ellipse through the existing renderer.

With controls **55, 5, 100, 1** (maximum size, offset, clear interval and initial
clear), 600 DRAWFRAME calls produce 597 changed completed images. Every captured
draw contains colored pixels; the final image has 196 distinct RGB colors.
The guest creates/deletes 600 brushes and executes 600 ellipses. CLOSE/WEP finish
after 196,496 instructions with WEP=1, balanced stack and no outstanding locks,
owned GDI objects, memory DCs, bitmaps, regions or local allocations.

**Work remaining:** add a hash-specific profile, explicitly accept PRIMARY_PAL
under the true-color policy, verify observed module state and run the normal
integration/WPF cases. No new Win16 API was needed for the tested configuration.

### Warp: MODULESELECTED initializes its color capability

Artifact SHA-256:
`EF06487ABA54ED687771E3E151B8DDEC2F93E6A7C7BFBD91B4C68C10351E8AC7`.

**Observed:** the baseline completes 30 draws and shutdown. A longer probe with
the Color control enabled still draws white stars if MODULESELECTED is omitted.
Static inspection identifies the additional color gate at data offset 0x0372:
the selection handler at S2:0853 initializes it and checks AD_SYSTEM color depth.
Star creation checks both that gate and the module's Color control. Setting
the checkbox alone therefore does not reproduce the complete lifecycle.

Sending the SDK's **MODULESELECTED (5)** after DLL startup and before
PREINITIALIZE enables the original color path without patching guest data or
adding a Windows handler. Controls **0, 30, 0, 1** select fast inward movement,
30 small stars and color. The speed's zero value is corroborated by the original
switch at S2:07E7; it is not interpreted as a stopped animation.

This configuration completes 600 changing draws, all with colored pixels, and
clean CLOSE/WEP after 5,372,658 instructions. WEP returns 1, the stack balances,
and all tracked guest objects and locks are released. Drawing uses the existing
SetPixel, PtInRect, rectangle and stock-brush services.

**Important bound:** 100 stars with mixed sizes completed 600 draws but exceeded
the 4,096-service-exit budget during CLOSE. That attempt is a failure, retained
in the report. The 30-small-star profile completes within the existing bounds.

**Work remaining:** add a tested profile and an explicit opt-in MODULESELECTED
lifecycle step in the shared session, then verify WPF behavior and cleanup.
Do not claim that all star counts, sizes and speed options are already supported.

## Next candidates after those two

**Puzzle (`AD30/PUZZLE.AD`)** completes startup, initialization and BLANK, then
stops on **USER!UnionRect (#80)** in its first DRAWFRAME. Its other unimplemented
imports include CopyRect and ScrollDC. This looks like the next useful expansion
of rectangle and image-moving behavior; only UnionRect was reached in the probe.
The configured sound calls use the existing unavailable-audio implementation.
No claim is made that adding UnionRect alone finishes the module.

**Satori (`AD30/SATORI.AD`)** reaches **GDI!CreatePalette (#360)** during
INITIALIZE. Imports also include GetPaletteEntries, SetPaletteEntries,
AnimatePalette, SelectPalette and RealizePalette. This is an actual palette
service request, unlike Spheres' host-level request followed by direct colors.
Its color path needs investigation; forcing the project's color mode does not
make palette animation disappear.

## Every additional module's first barrier

"Additive relocation" includes imported offset/far-pointer forms as well as
internal selector forms. "OS fixup" is a distinct relocation kind associated
with the floating-point runtime investigation. "Prologue" means the loader
does not recognize an exported function's DS setup. These failures come from
the actual C# loader, not an import-count heuristic.

| File | First observed barrier or successful continuation |
| --- | --- |
| AD30/ARTIST.AD | OS fixup |
| AD30/BADDOG.AD | Additive relocation |
| AD30/BUGS.AD | Additive relocation |
| AD30/CLOX3.AD | Additive relocation |
| AD30/CYCLE.AD | Additive relocation |
| AD30/DOSSHELL.AD | Additive relocation |
| AD30/FISH3.AD | Additive relocation |
| AD30/FROST.AD | Additive relocation |
| AD30/GUTS.AD | Export prologue |
| AD30/LOGO.AD | Additive relocation |
| AD30/MESSAGES.AD | Additive relocation |
| AD30/NIRVANA.AD | Additive relocation |
| AD30/NONSENSE.AD | Export prologue |
| AD30/PHOTON.AD | Additive relocation |
| AD30/PUZZLE.AD | DRAWFRAME: UnionRect |
| AD30/RATRACE.AD | Additive relocation |
| AD30/RAY.AD | Additive relocation; OS fixups also present |
| AD30/REBOUND.AD | Additive relocation |
| AD30/ROSE.AD | OS fixup |
| AD30/SATORI.AD | INITIALIZE: CreatePalette |
| AD30/SLIDES3.AD | OS fixup |
| AD30/SPHERES.AD | 600 draws and shutdown after explicit PRIMARY_PAL continuation |
| AD30/SPLIGHT.AD | Additive relocation |
| AD30/TOASTER3.AD | Additive relocation |
| AD30/TOILET.AD | Additive relocation |
| AD30/WARP.AD | 600 color draws and shutdown with MODULESELECTED and constrained controls |
| AD30/WMORPH.AD | OS fixup |
| AD30/YBYH.AD | Additive relocation; OS fixups also present |
| AD30/ZOOOMMM.AD | Additive relocation |
| MAD/BOGGLINS.AD | Additive relocation |
| MAD/BORIS.AD | 31 segments exceed the loader's 16-segment placement limit |
| MAD/CONFETTI.AD | OS fixup |
| MAD/MMAS.AD | Additive relocation |
| MAD/MOWIN.AD | Additive relocation |
| MAD/RAINDROP.AD | Additive relocation; OS fixups also present |
| MAD/TUNNEL.AD | Additive relocation |

## Recovered helpers change the research options, not current support

Twelve of the additional AD3 modules reference both **ADXPL300** and **ADTOOL**.
The extracted ADXPL300 identifies itself as the After Dark cross-platform
library; its static import table contains 140 identities across AD_SND,
KERNEL, GDI, USER and MMSYSTEM. The recovered ADTOOL identifies itself as a
Toolhelp wrapper and has ten KERNEL import identities. Those are file-level
observations, not proof that every import is used during playback.

Six new MAD modules reference AD_RSRC, supplementing the original five callers.
AD_RSRC's recovered name tables expose only WEP by name, so obtaining the DLL
does not automatically name its other ordinal contracts. Its code is now
available for analysis. Executing actual helpers through a future multi-module
NE loader is another option; the present loader does not recursively load them.
Helper availability therefore removes a missing-artifact problem without
establishing missing loader, ABI or Windows behavior.

## Method, reproduction and limits

The probe binds imports through the **actual C# Win16Imports registry**, including
named AD_SND services. Unrecognized imports receive distinct fail-on-use
gateways, never fabricated successful replies. This diagnostic binding lets the
loader expose its own failures and unused imports remain unused; it does not
change the production player's allowlist or make unknown imported constants safe.

Recognized hashes use their production profiles. Other modules receive the
documented SDK records plus the existing compatibility tail, 320x240 RGB24,
AD version 200, a fixed civil-time seed, diagnostic resource-derived controls
and a clock advancing 2,500 ms per read. Numeric-control midpoints are diagnostic
inputs, not certified original engine defaults. Sound controls are disabled;
color checkboxes use their enabled value. Startup receives the normal register
convention; calls use real far-call caller code and the production gateway.

Each unsupported-module invocation is bounded by two million instructions,
4,096 service exits, three-second native slices and the existing cumulative
native-time guard. Each file gets its own process with a 45-second watchdog;
the two 600-draw investigations allow 120 seconds. The probe checks stack balance
after every successful phase. The extended successes also check WEP and owned
resource/lock cleanup. Failure leaves the guest for host disposal, never an
attempt to resume an interrupted lifecycle call.

```powershell
dotnet build tests/AfterDarker.Tests -p:AuditLocalModules=true
python tools/audit-local-modules.py ad/windows98-2026-09-20 --output artifacts/my-audit/baseline

python tools/audit-local-modules.py ad/windows98-2026-09-20 --match AD30/SPHERES --frames 600 --timeout 120 --direct-rgb-palette-requests --controls 55,5,100,1 --output artifacts/my-audit/spheres
python tools/audit-local-modules.py ad/windows98-2026-09-20 --match AD30/WARP --frames 600 --timeout 120 --module-selected --controls 0,30,0,1 --output artifacts/my-audit/warp
```

Use a new output directory for each attempt; reports are not overwritten by
filename. The C# source is [ModuleReadinessProbe.cs](../../tools/ModuleReadinessProbe.cs).
The opt-in test writes an investigation report: a passing test process is **not**
a module compatibility assertion. Read Error/FailedPhase/ProcessFailure and
Outcome. Ordinary builds exclude this probe; the 329 public regression tests
still pass without private inputs. No proprietary files or images are committed.

Observed captures establish original code producing changing pixels for these
configurations. They do not establish historical visual fidelity, full settings
coverage, arbitrary dimensions/seeds, live WPF behavior or Ocuvera integration.

## Supported-profile follow-up — 2026-09-21

Puzzle's analyzed AD30 artifact is now supported through hand rolled ScrollDC,
CopyRect and UnionRect. This supersedes its older readiness result above, not
other files with the same name. See [execution evidence](puzzle-execution.md).
The intervening native experiment was retired; its [decision record](native-gdi-spike-decision.md)
explains the comparison. Historical audit results remain observations of the
runtime at the time they were collected.
