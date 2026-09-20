# Stained Glass: original patterns through shared GDI

The analyzed module plays in the WPF player with **Complexity 10, Duplication
100 and Color 100**. These are fixed inputs for this supported profile; the
settings dialog and other configurations are not enabled. All pattern construction,
random choices and duplication decisions execute in the original Win16 code.
The host supplies Windows services and pixels, not a replacement animation.

## Artifact and invocation facts

- SHA-256: `694BDDF2C92E84A2171D3C3885CBBD2A02067D934B9C6F8CE6E298B40AB4096C`.
- Nine NE segments; automatic data is segment 9, mapped at selector `0048`.
- DLL startup: `S4:0000`; MODULE: `S1:003E`; WEP: `S1:0027`.
- The normal loader applies 569 relocation patches. Host slots follow the image:
  caller `0050`, gateway `0058`, stack `0060`, host records `0068`.
- The initial local heap reservation is 4,096 bytes. Tested playback makes no
  local allocations; drawing handles belong to the shared GDI object pools.
- Control resources name Complexity, Duplication and Color. The fourth control
  word is zero. The profile advertises a color display and square-pixel aspect.
- Accepted surfaces are 64–2,048 pixels in each dimension. The minimum is a
  conservative exercised boundary for nested cells, not a reconstructed historical
  minimum. WPF retains its 640x480 logical surface and scales it for presentation.

Startup, PREINITIALIZE, INITIALIZE, BLANK, repeated DRAWFRAME, CLOSE and WEP all
use the existing session and checked Pascal far-return mechanism. No loader,
stack-return, register convention or guest machine-code patch was added for this module.

## What the module taught the runtime

**An HDC has a coordinate origin.** `SetWindowOrg` changes the point in logical
coordinates which maps to the device's `(0,0)`:

```text
device X = logical X - window-origin X
device Y = logical Y - window-origin Y
```

Each DC stores its own origin. Guest inputs remain signed 16-bit values, but
subtraction stays in 32 bits, so `32767 - (-32768)` clips off-screen instead of
wrapping onto a visible pixel. The module's coordinates and current point are
not rewritten. Get/SetWindowOrg return packed signed words in DX:AX.

**Drawing can combine colors instead of replacing them.** ROP2 belongs to the
DC. The common raster helper implements its 16 Boolean operations on RGB bits.
For XOR, `destination ^= source`; drawing the same coverage twice restores the
old image. An outline/fill overlap or two overlapping pen end caps must therefore
mix each covered pixel only once. Hollow brushes preserve interiors. Explicit
FillRect/FrameRect, SetPixel and SRCCOPY instead use their own copy semantics;
they do not accidentally inherit the DC's ROP2.

**Existing pixels are drawing input.** Stained Glass calls BitBlt to duplicate
regions. A source snapshot preserves the image as it was before the operation,
including when source and destination overlap in the same surface. Copying
directly from the changing destination would smear pixels in one direction.
Source and destination origins are applied independently; clipping bounds the
work. Temporary storage uses a pooled buffer bounded by one source surface.
This adds no compatible bitmap/DC allocator or resource loader.

**A rectangle helper can modify guest memory without drawing.** OffsetRect and
InflateRect write wrapping Win16 coordinates back through checked far pointers.
IntersectRect reads both inputs before writing, because output may alias either
input. Empty intersections write a zero RECT. EqualRect compares all four
coordinates. Win16's OffsetRect/InflateRect return void, unlike their Win32 BOOL
counterparts; the gateway preserves AX/DX for these void calls.

An additional raster surprise came from the Windows oracle: FrameRect paints
four explicit brush strips. A zero-width/height rectangle can still paint
adjacent strips; reversed edges fail. Treating every zero extent as empty
disagreed with native results. This is documented, bounded behavior in the
brush rasterizer, separately tested from selected-pen Rectangle.

## Shared service contracts

| Import | Pascal argument bytes | Return | Added behavior |
| --- | ---: | --- | --- |
| GDI #97 GetWindowOrg | 2 | DX:AX | Current origin, packed Y:X |
| GDI #11 SetWindowOrg | 6 | DX:AX | Set signed origin, return previous |
| GDI #4 SetROP2 | 4 | AX | Set binary mix, return previous; zero for invalid mode |
| GDI #31 SetPixel | 10 | DX:AX | RGB COLORREF, or `FFFFFFFF` when clipped |
| GDI #34 BitBlt | 20 | AX | SRCCOPY between registered surfaces, including self-copy |
| USER #83 FrameRect | 8 | AX | Explicit solid/null brush border |
| USER #77 OffsetRect | 8 | void | Translate guest RECT |
| USER #78 InflateRect | 8 | void | Grow/shrink guest RECT |
| USER #79 IntersectRect | 12 | AX | Alias-safe intersection and nonempty result |
| USER #244 EqualRect | 8 | AX | Exact coordinate equality |

Existing GetStockObject adds WHITE_BRUSH and NULL_BRUSH. FillRect now accepts
owned solid brushes as well as stock brushes. LineTo adds width-three solid
strokes; Ellipse adds width-three outlines and hollow interiors. The selected
object slots, reusable handles and destruction rules are shared with earlier modules.

The signatures/ordinals were checked against Wine 10.0's
[GDI declarations](https://github.com/wine-mirror/wine/blob/wine-10.0/dlls/gdi.exe16/gdi.exe16.spec),
[USER declarations](https://github.com/wine-mirror/wine/blob/wine-10.0/dlls/user.exe16/user.exe16.spec)
and [Win16 rectangle implementations](https://github.com/wine-mirror/wine/blob/wine-10.0/dlls/user.exe16/user.c).
These are compatibility references, not proof of original Windows 3.1 internals.
Modern contracts for [ROP2](https://learn.microsoft.com/en-us/windows/win32/api/wingdi/nf-wingdi-setrop2),
[window origin](https://learn.microsoft.com/en-us/windows/win32/api/wingdi/nf-wingdi-setwindoworgex)
and [SetPixel](https://learn.microsoft.com/en-us/windows/win32/api/wingdi/nf-wingdi-setpixel)
guide behavior; Win32 signatures are not substituted for Win16 ABIs.

## Verification and limits

The complete compiler-plus-eleven-module regression passed **388/388 cases**
(7m32s on this development host). The public suite also passed independently:
**273/273**, without proprietary input or Watcom. Seven cases opt into Stained
Glass. Its 2,000-draw run reached 11,212,972 instructions before CLOSE, 1,164
BitBlt calls and 201,112 SetPixel calls, with only one owned pen and one owned
brush live at a time. The largest observed draw used 62,089 instructions and
2,073 imports. Those measurements explain the larger service budget; they are
observations for this seed/size, not universal maxima.

- Source-authored x86 programs call all ten new imports through the real
  gateway, store return values in guest memory, and verify signed arguments,
  checked pointers, DX:AX packing, unchanged registers and Pascal stack cleanup.
- Native Windows comparisons match exact pixels for all 16 ROP2 modes with
  translated one-pixel lines/rectangles; explicit frames/pixels including
  degenerate bounds; and overlapping/clipped SRCCOPY in both directions.
- Width-three lines are round-ended geometric approximations. For 200 tested
  unclipped strokes across octants, including zero length, each differing
  color has a native/software counterpart within one pixel in both directions.
  Clipping can remove that neighbor; this is not a universal error bound.
- Ellipse widths one, two and three have the same one-pixel color-neighborhood
  comparison across circle radii 4–27. General ellipse edges remain approximations.
  Double-XOR restoration and extreme signed clipping have separate deterministic tests.
- Private execution checks 2,000 draws at 320x240, plus 600/1,200/1,200/400 draws
  at 64x64, 640x480, 321x239 and 2048x2048 with distinct deterministic seeds.
  Independent sessions stay deterministic even with different generic Speed
  values: the profile has no speed slider. CLOSE/WEP leave no pens, brushes,
  allocations or locks; WEP returns AX=1 with SP=1000 and caller DS=0068.
- Actual WPF acceptance checks RGB readback, restart, close while playing and
  switching both directions with Shapes. The original animation's cadence and
  modern RGB rasterization remain host adaptations, not historical fidelity evidence.

Static inspection of `S7:07B7` shows a size comparison choosing BitBlt for equal
source/destination extents and StretchBlt otherwise. Tested playback reaches
BitBlt, not StretchBlt. StretchBlt remains a named fail-on-use import; no scaling
success is fabricated. Other controls, gray mode, settings UI, unsupported raster
operations and general wide-pen geometry are outside this profile. More seeds
can expose paths absent from these finite runs; they should produce a symbolic
failure rather than silently wrong success.

Every invocation retains 2,000,000 instructions and 4,096 service exits as upper
bounds, one-second native slices and the shared cumulative execution-time limit.
These are safeguards, not a claim that a call consumes that work or that finite
tests establish indefinite playback.

## Run and inspect it

F5 the WPF project and choose **File > Load AD file… > Stained Glass.ad**, or:

```powershell
dotnet run --project src/AfterDarker.Wpf --no-launch-profile -- 'C:\path\Stained Glass.ad'
$env:AFTER_DARKER_STAINED_GLASS = (Resolve-Path 'ad/Stained Glass.ad').Path
dotnet test -p:TestLocalStainedGlass=true --filter 'TestCategory=LocalStainedGlass'
```

Set `AFTER_DARKER_STAINED_GLASS_CAPTURE` to an absolute local output directory
to save every hundredth image in the 2,000-draw test. Captures, traces and original
files stay ignored. The local acceptance reports live under
`artifacts/stained-glass/`; they are not public test inputs.

Start with [StainedGlassProfile](../../src/AfterDarker.Runtime/Modules/StainedGlassProfile.cs)
for the artifact contract, [Win16Drawing](../../src/AfterDarker.Core/Win16/Win16Drawing.cs)
for DC/object state, and the [runtime code map](../runtime-code-map.md) for the
separate rectangle, brush, copy and raster implementations.
