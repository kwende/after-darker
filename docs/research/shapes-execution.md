# Shapes: original drawing with direct RGB colors

Verified on 2026-09-20. Supported SHA-256:

`16E51D41E4BA8E05EBF9ED5E4A59FDCFD32255634304E38DAC2BA93AE1C4C67C`

Shapes is the tenth supported original module in **File > Load AD file…**.
Its guest code chooses every color, rectangle extent and ellipse/rectangle
decision. Each DRAWFRAME paints one new shape over the previous image. The host
supplies GDI services and presents the resulting pixels; it does not generate
replacement animation.

## Host choices and the palette boundary

The fixed controls are **Clear Screen First = 1, Color = 1**, with unused
controls 3 and 4 zero. The destination advertises 24-bit color. Speed is not a
module control and the WPF selector is disabled. The existing 60-Hz host pacer
is a modern presentation choice, not reconstructed historical timing.

BLANK returns the SDK's **RGB_PAL (11)**. The profile explicitly accepts that
request, while the shared color resolver uses PALETTERGB's RGB components
directly. The owner authorized this adaptation: getting the original varied
colors and shapes running takes precedence over historical palette matching.
No indexed palette or palette pointer is manufactured. PALETTEINDEX remains
unsupported and fails before creating an object.

This has an artifact-based justification. At S2:00E5–0124 the guest obtains
three random color components and constructs `0x02BBGGRR` for CreateSolidBrush.
S2:00BF–00E3 constructs equal components when Color is off or the host reports
one-bit color. Thus the grayscale branch computes gray itself; it does not
depend solely on palette matching. That branch is a **static finding**, not
an enabled or runtime-tested UI setting. **Scope decision, 2026-09-20:** the
owner explicitly chose color-only Shapes playback. Grayscale controls, execution
tests and palette behavior are deliberately out of scope, not outstanding work
required to complete Shapes. A future contributor can add them independently.
This now follows the [project-wide color playback policy](../../AGENTS.md#color-playback-policy),
which applies to every screensaver we support.

[Win16Color](../../src/AfterDarker.Core/Win16/Win16Color.cs) names the shared
RGB/PALETTERGB policy used by both pens and brushes. Microsoft's
[PALETTERGB contract](https://learn.microsoft.com/en-us/windows/win32/api/wingdi/nf-wingdi-palettergb)
explains the palette-relative request; drawing the components directly is our
explicit true-color adaptation.

## Shared Windows additions

| Call or behavior | Contract and implementation |
| --- | --- |
| GDI #66 CreateSolidBrush | One DWORD COLORREF, four argument bytes, HBRUSH in AX; DX preserved. Creates a bounded guest handle, never a native pointer. |
| GetStockObject(NULL_PEN = 8) | Returns a distinct stock pen handle. Selecting it disables outlines; it does not mean an invalid/null handle. |
| SelectObject / DeleteObject | Owned brushes share the existing independent brush slot. Deletion fails while any HDC selects the object. Released handles are reused. |
| GDI #27 Rectangle | HDC and four signed coordinates, ten argument bytes, BOOL in AX, DX preserved. Supports null or one-pixel pens and solid brushes. |
| Ellipse with NULL_PEN | Fills the software ellipse spans entirely with the selected brush. Existing outlined ellipses retain their rendering policy. |
| LineTo with NULL_PEN | Moves the current point without changing pixels. Ellipse and Rectangle preserve that point. |

Signatures are checked against the pinned Watcom `h/win/win16.h`; ordinals and
Pascal layouts agree with
[Wine 10.0's Win16 GDI spec](https://github.com/wine-mirror/wine/blob/wine-10.0/dlls/gdi.exe16/gdi.exe16.spec).
The gateway performs the existing far return and cleanup: eight stack bytes
for CreateSolidBrush, fourteen for Rectangle, including the far return address.

[Win16Drawing](../../src/AfterDarker.Core/Win16/Win16Drawing.cs) owns separate
256-object pen and brush pools with disjoint handle ranges. Stock objects do
not consume those pools. Playback diagnostics now expose live/peak brushes,
and successful shutdown requires no owned brushes remaining.

Rectangle bounds were compared with a native Windows oracle. A null pen removes
one additional right/bottom row relative to the ordinary half-open bounds;
a 1x1 rectangle with a one-pixel outline paints nothing. These initially
surprising differences are retained and tested. Clipping never invents a border
at the window edge. Existing FillRect/InvertRect behavior remains separate;
FillRect still accepts only the previously supported black stock brush.

Ellipse filling reuses the deterministic integer boundary algorithm. It is an
approximation of native GDI, including for arbitrary aspect ratios in Shapes.
Hard Rain's measured circle bound must not be generalized to these ellipses.
No additional rendering package, native production GDI, or antialiasing is used.

## Guest state and failure bounds

The artifact has five NE segments, with automatic data in segment 5 and a
1,024-byte initial local-heap reservation. No dynamic local allocations are
needed by the tested path. Shape coordinates and temporary object handles are
stack locals; the accumulating image lives on the host's drawing surface.

| Data offset | Meaning | Static anchor |
| --- | --- | --- |
| 0x0010 | Compatibility flag | S1:0095–00BD |
| 0x0052 | 32-bit random-generator state | S3:0090–00D3 |
| 0x0382 | Current HDC | S1:004C–004F |
| 0x0384 | AD_SYSTEM far pointer | S1:0059–0061 |
| 0x0388 | DLL instance | S1:0017 |
| 0x038A | AD_MODULE far pointer | S1:0078–0080 |

[ShapesState](../../src/AfterDarker.Runtime/Modules/ShapesState.cs) exposes those
globals as detached observations. [ShapesProfile](../../src/AfterDarker.Runtime/Modules/ShapesProfile.cs)
owns the hash, controls, record-pointer checks and palette-reply policy.

S2:0132–0142 skips coordinate initialization when both dimensions are at most
four; larger drawing paths divide by half the width and height. The profile
requires **at least 5x5** before creating a guest, avoiding uninitialized stack
coordinates and zero divisors. Maximum dimensions remain 2048x2048. The normal
50,000-instruction and 128-service-exit per-call budgets suffice.

## Verification and previews

- Public tests cover brush identity/capacity/reuse, selection in multiple HDCs,
  deletion protection, direct RGB decoding, indexed-color rejection, null pens,
  unchanged shape current points, and rejected wide rectangles before mutation.
- Source-authored x86 callers exercise both shapes, brush creation/selection/
  deletion, signed coordinates, DWORD word order, AX/DX results and far-return
  stack cleanup. Disabled services fail by name before reading arguments.
- Rectangle comparison checks 110 bounds at each of two pen settings against
  modern Windows, including reversed, clipped, empty, tiny and full-int16 bounds.
- Six opt-in Shapes cases cover 1,000 draws, independent deterministic guests,
  dimensions 5x5/321x239/2048x2048, cleanup, and invalid dimensions/artifacts.
  The long run executes **521,164 instructions**, **489 rectangles**, **511
  ellipses**, and **1,000 distinct colors**. It checks each shape choice, color,
  extent and final random seed against the original algorithm. Peak brushes: one;
  remaining brushes, pens, heap allocations and locks after shutdown: zero.
- Actual WPF acceptance passes standalone, restart, close while playing, and
  switching both ways with Hard Rain, including pixel readback and cleanup.
- The full regression passes **367 tests**, including **259 public cases**, the
  compiler-built Win16 fixture and all ten supported original modules.

To run the original module tests and optionally capture the first 120 completed
guest draws as exact 640x480 RGB PNGs:

```powershell
$env:AFTER_DARKER_SHAPES = (Resolve-Path ad/Shapes.ad).Path
$env:AFTER_DARKER_SHAPES_CAPTURE = Join-Path (Get-Location) 'artifacts/shapes/frames'
dotnet test -p:TestLocalShapes=true --filter TestCategory=LocalShapes --report-trx
```

Omit the capture variable for ordinary tests. The local mobile preview encodes
those images at 20 guest updates per second, three times slower than the live
60-Hz policy, for inspection. Unchanged images may share a longer GIF hold;
the preview's decoded RGB images were checked against the PNGs. Preview images,
GIF, original module and disassembly stay ignored under `artifacts/` and `ad/`.

This proves the fixed color configuration, not every control, revision, palette
effect or historical rendering detail. No loader or heap behavior changed.
