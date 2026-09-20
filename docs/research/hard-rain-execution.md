# Hard Rain: original drops and stateful ellipse drawing

Verified on 2026-09-20. Supported artifact SHA-256:

`0A93389EFC588962EAF0FE683836EFCB934BF1D036B2316FF202212A0800B331`

Hard Rain is the ninth supported module in **File > Load AD file…**. Original
x86 code chooses the colors and centers, grows the rings, switches pen width,
erases expired drops, and initializes replacements. C# supplies Windows drawing
behavior and the After Dark host records. It does not recreate the animation.

## What this module added

An HDC now has **independent pen and brush selections**. Selecting a pen returns
the previous pen; selecting a brush returns the previous brush. A fresh context
has a black pen and white brush. Hard Rain selects BLACK_BRUSH for its interiors
and later restores the saved brush handle. The erasure path selects black pen
and brush explicitly. Stock objects are host-owned; created pens retain the
existing 256-handle bound, selected-object deletion protection and reuse.

[Win16Pen](../../src/AfterDarker.Core/Win16/Win16Pen.cs) retains color **and width**.
CreatePen accepts solid RGB/PALETTERGB widths 0, 1 and 2, with zero normalized to
one under identity mapping. Ellipse consumes the width; LineTo still rejects a
wide pen before changing pixels or current position. General wide lines, owned
brushes, null objects, additional pen styles and arbitrary mapping modes remain
outside this increment.

GDI ordinal 24 now binds to `Ellipse(HDC, int, int, int, int)`: five Pascal words,
BOOL in AX, DX preserved, and ten argument bytes plus the four-byte far return
removed from the stack. The pinned Watcom `h/win/win16.h` declares this signature.
The existing gateway performs the return; [Win16ApiDispatcher](../../src/AfterDarker.Core/Win16/Win16ApiDispatcher.cs)
decodes the signed coordinates. [Win16Drawing.Ellipse](../../src/AfterDarker.Core/Win16/Win16Drawing.cs)
uses both selected objects without changing the current point. Microsoft's
[Ellipse contract](https://learn.microsoft.com/en-us/windows/win32/api/wingdi/nf-wingdi-ellipse)
describes those object/current-point semantics; its Win32 declaration is not
the source of our Win16 stack layout.

## The host field that prevented a divide-by-zero

The recovered SDK declares `AD_SYSTEM.ptAspect` at offsets 0x0C and 0x0E.
Hard Rain's S2:0301–0331 computes the horizontal radius from the vertical radius
using `radius * ptAspect.y / ptAspect.x`. The common record previously left
those unused words zero. Once the pen/brush calls worked, the first drawing call
reached a real divide-by-zero at S3:011A in its integer division helper.

[HardRainProfile](../../src/AfterDarker.Runtime/Modules/HardRainProfile.cs) supplies
**1:1**, matching the square pixels of our software surface. The shared
[host contract](../../src/AfterDarker.Core/AfterDark/AfterDarkHostContract.cs)
names both offsets; existing profiles keep their proven records. This is host
device information, not a change to the guest calculation. Source provenance
for MODULE.H remains in [the SDK research](after-dark-sdk.md).

## Controls and guest state

Fixed host choices are **five drops, size 20, Clear Screen First**. The numeric
resource ranges are 1–9 drops and 5–35 for size. These values are validated host
inputs, not reconstructed saved settings or certified original slider defaults.
Hard Rain has no speed control; the WPF speed selector is disabled. The existing
60-Hz host pacer remains a modern timing policy.

| Data location | Meaning | Static evidence |
| --- | --- | --- |
| 0x0014 | Compatibility flag | S1 PREINITIALIZE branch |
| 0x0502 | Saved DLL instance | S1:0014–0017 |
| 0x0012, 0x0010 | Drop count and size | S2:001C–002B reads controls 1 and 2 |
| 0x0504 | Next drop index | S2:00AC–00C2 advances and wraps it |
| 0x0382 + index × 18 | Drop record array | S2:0151–0274 generates a drop |
| Record +0, +2, +4 | Red, green, blue words | Original six-color selection |
| Record +6, +8 | Signed center X/Y | Guest random values modulo dimensions |
| Record +10, +14 | Current and maximum radius, DWORDs | Radius starts at 4; maximum is `random % 20 + 7` |
| 0x04FE, 0x0506 | Locked system/module pointers | S1 GlobalLock replies |

[HardRainState](../../src/AfterDarker.Runtime/Modules/HardRainState.cs) exposes
detached, typed observations. The drop array is static storage in the original
data segment; this module does not allocate a history from the local heap.

Each DRAWFRAME visits one of the five records. While the radius is at most half
its maximum it selects width 2; later it selects width 1. The colored ellipse
is called at S2:0358. If `radius + 3` exceeds the maximum, S2:0420 paints a
slightly larger black ellipse and the guest generates a new drop. Otherwise
the radius increases by three. No new intermediate-frame checkpoint is needed.

## Software raster policy and fidelity limit

[EllipseRasterizer](../../src/AfterDarker.Core/Rendering/EllipseRasterizer.cs)
builds row spans from an integer ellipse boundary. The one-pixel outline is the
boundary itself. Width two is the band between ellipses expanded and contracted
by one pixel. Pen pixels take precedence over the brush interior. Coordinates
are normalized before tracing; clipping happens against the original curve,
so an off-screen ellipse is not squeezed into the visible area. Empty extents
paint nothing. The walk and its 64-bit arithmetic are bounded for signed Win16
coordinates; only visible row spans are retained.

The boundary walk adapts Alois Zingl's MIT-licensed algorithm. See
[source attribution and license](../../third-party/README.md). No native GDI
calls or new runtime packages are used by production rendering.

**Known difference:** this software raster is not pixel-exact modern GDI at
either width. A native Windows oracle compares radii 4–27, covering the sizes
used by these controls plus erasure and one extra radius. Across 24 circles it observes **990 differing
pixels at width 1** and **3,178 at width 2**. For every differing pixel, the same
RGB color exists within one pixel horizontally/vertically/diagonally in the
other image, checked in both directions. This quantifies a small edge-position
difference, not exact raster conformance or a Windows 3.1 fidelity claim.
Other ellipse proportions do not inherit that measured one-pixel bound.

## Verification and scope

- Public tests exercise separate brush/pen slots, selection return identities,
  stock lifetime, width retention, unchanged current point, clipping, reversed
  and empty bounds, full signed-coordinate arithmetic and rejected wide LineTo.
- Tiny source-authored x86 callers check Ellipse's signed argument order, AX/DX,
  caller register preservation and 14-byte stack cleanup; invalid HDCs and
  disabled drawing stop before guest success.
- Six private cases cover 1,000 draws, independent guests over 200 draws, sizes
  1x1/321x239/2048x2048, empty shutdown and modified-artifact rejection. The
  deterministic 1,000-draw run executes **308,751 instructions**, **1,211 ellipses**
  and **211 regenerations**, with a peak of one created pen and none left alive.
  Each record's radius and next-drop index are checked against original control
  flow; no allocations or locks remain at shutdown.
- Actual WPF acceptance passes standalone Hard Rain and switching both ways
  with Rainstorm, including RGB readback, unsupported-file rejection, Stop,
  restart and close while playing. The captured output was inspected.
- The complete regression passes **352 cases** across public tests, the
  compiler-built fixture and all nine supported modules. The default suite
  separately passes **250 public cases** without private files or Watcom.
  Hard Rain's six cases pass again after the final profile cleanup and stronger
  per-draw checks of the guest's pen width and color.

The normal limits suffice: 50,000 instructions and 128 service exits per guest
invocation, plus existing native-time bounds. No loader, allocator, callback,
sound, resource-decoding or additional Windows service work was needed.

```powershell
$env:AFTER_DARKER_HARD_RAIN = (Resolve-Path 'ad/Hard Rain.ad').Path
dotnet test -p:TestLocalHardRain=true --filter TestCategory=LocalHardRain --report-trx
dotnet run --project src/AfterDarker.Wpf --no-launch-profile -- --smoke 'ad/Hard Rain.ad' artifacts/wpf-smoke/hard-rain
```

Original files, disassembly, oracle probes and captures remain ignored. Other
settings, module revisions, historical timing and indefinite endurance are not
proven. Shapes is the next candidate; it is not implemented in this branch.
