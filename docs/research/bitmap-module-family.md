# Can of Worms, GeoBounce, Nocturnes and Punch Out

Implemented together on `codex/bitmap-module-family`, starting at reviewed
`main` commit `299fab6` after Gravity. The scope is original-code execution of
these four exact files with fixed color controls and no audio. This brings the
supported collection to **16 of the 29 analyzed modules**.

## Artifact identities and chosen controls

| Module | SHA-256 | Four AD_MODULE control words |
| --- | --- | --- |
| Can of Worms | `1FD824FCDA5ED65D3903E5158B8F20A2F2C2614E5E64EA48E0EB14B81D27D72E` | `50,11,10,0`: Weavy motion, 11 segments, 10 worms, sound off |
| GeoBounce | `19B6058BB0EF1D63E5A081894837FC5E17224F11B907EF4C08697C948FED32A5` | `0,55,50,2`: tetrahedron, size 55, speed 50, shaded/color faces |
| Nocturnes | `525AC95FAAB662835D497D3D491512BC2F24A7AA752C1CE014B413288E3036BF` | `0,0,1,50`: unused template fields zero, color enabled, density 50 |
| Punch Out | `FABAFCEBF8638660C00B3E4314F0D72E5F4B796907A0B1A4E8E4AF3B582B6FC2` | `0,40,5,0`: circular punches, size 40, speed 5, sound off |

All supply matching AD_SYSTEM/AD_MODULE dimensions and square-pixel aspect.
Can of Worms uses the owner's requested all-white initial image. Punch Out
also receives white as a deterministic desktop-image substitute. These are
one-time host inputs; the original modules perform subsequent drawing.
No desktop capture is implemented. Nocturnes and GeoBounce start black.

The generic WPF speed control is disabled for these fixed profiles. Supported
dimensions start at 128×128. Punch Out is capped at 2028 in either dimension:
its original code adds a 10-pixel border on each side of a full-screen bitmap,
which must remain within the shared 2048-pixel bitmap limit. Other profiles
retain the 2048 limit. Ordinary lifecycle calls allow 500,000 instructions and
4,096 host-service exits; existing elapsed-time bounds still apply.

## What each module taught us

**Can of Worms:** the existing masks/blits were sufficient after adding the
stock WHITE_PEN and `IsRectEmpty`. The latter reads a signed RECT and tests
nonpositive extents without sorting the edges or changing guest memory. The
module owns one 14×6 mask bitmap and repeatedly creates/deletes temporary DCs.
It still calls the sound helper, even with its sound control disabled; those
calls receive the existing unavailable-device/null-handle responses.

**GeoBounce:** its selected polyhedron requires `Polygon`. The shared software
rasterizer fills alternate crossing pairs and closes the one-pixel outline.
It collects fill/outline coverage before writing pixels so XOR cannot erase
pixels accidentally touched twice. It does not change the current drawing
position. POINT coordinates are signed and the guest array is bounds-checked;
at most 256 vertices are supported. The module's startup constructs 112 colored
brushes for its shading table, which it retains and eventually deletes.

**Nocturnes:** `LoadBitmap(instance,"eyes")` requires a resource alias lookup,
a standard one-bit DIB decoder, and owned bitmap creation. Its resource labels
include leftover "Flying Objects" and "Toast" text; the observed rendering
code reads Color and Density instead. Resource labels alone therefore do not
establish a control's active meaning. The image is a sprite sheet of eyes,
102×80 pixels, used as a mask to paint different colors. Normal DRAWFRAME also
sets/restores text/background colors and background mode. Those DC attributes
participate in monochrome-source blits despite their text-related names.
See the [resource walkthrough and open-source references](../ne-bitmap-resources.md).

**Punch Out:** rectangular and elliptic clipping regions restrict destination
blits, including the newly reached `SRCPAINT` (source OR destination). Selecting
a region copies geometry into the DC, so deleting the region handle does not
clear that clip. Coordinates are device pixels, independent of window origin;
source-DC clipping does not limit BitBlt reads. The module also draws a Rectangle
before selecting its first owned bitmap. That request really targets the memory
DC's default one-pixel bitmap; we execute only its observed black/white subset.
Its original animation repeatedly closes/reinitializes its temporary drawing
state as punches finish. In 600 calls it allocates 54 bitmaps cumulatively,
but owns at most three simultaneously. Peak regions are two, not 36.

## Reusable implementation

- `NeResourceCatalog` and `NeResourceAlias`: snapshotted file data, directory and
  RT_NAMETABLE lookup; independent of CPU, GDI, and WPF.
- `DibBitmapDecoder` / `DecodedBitmap`: validated payload to typed RGB data.
- `Win16Api.Resources`: checked guest names/instance and LoadBitmap ownership.
- `Win16Drawing.Regions` / `RasterClipRegion`: bounded handles and copied geometry.
- `Win16DeviceContext.Draw` / `PixelSurface.WithClip`: synchronous scoped clip;
  restored in `finally`, so shared pixels never inherit another DC's clip.
- `Win16Drawing.Colors`: text/background attributes and previous-value returns.
- `BitmapModuleProfile` and four purpose-named profiles: shared SDK inputs,
  artifact-specific offsets/controls and observed-global validation only.

Nine additional imports use the existing canonical gateway and Pascal ABI:
`IsRectEmpty`, `Polygon`, `LoadBitmap`, `CreateRectRgnIndirect`,
`CreateEllipticRgnIndirect`, `SelectClipRgn`, `SetTextColor`, `SetBkColor`,
and `SetBkMode`. No API behavior is hidden inside a module profile or CPU hook.
The region namespace is bounded to 256 handles; bitmap/DC limits remain
256 bitmaps, 64 memory DCs and 32 MiB of RGB bitmap storage per guest.

## Proof and visual limits

Observed deterministic 320×240 runs complete 600 drawing calls each, save six
PNGs each, and execute CLOSE/WEP with balanced global/local locks and no owned
pens, brushes, bitmaps, DCs, regions or local allocations remaining.

| Module | Instructions before final shutdown | Peak bitmaps | Peak memory DCs | Bitmap bytes during run |
| --- | ---: | ---: | ---: | ---: |
| Can of Worms | 1,172,245 | 1 | 1 | 252 |
| GeoBounce | 39,583,084 | 1 | 1 | 46,128 |
| Nocturnes | 564,188 | 1 | 1 | 24,480 |
| Punch Out | 115,356 | 3 | 3 | 280,800 |

These totals describe one deterministic seed/size, not API limits. Additional
integration cases cover size/seed variation, live clocks at WPF dimensions,
independent guests, invalid dimensions, changed hashes and complete shutdown.
Actual WPF smoke runs cover each module, switching to the next one, pixel
readback, invalid-file rejection, restart and close while playing. Reports
and images are local under `artifacts/bitmap-family/`.

Native Windows tests establish exact rectangular clipping/SRCPAINT pixels for
the tested case. Both polygon samples (convex and concave) also match exactly.
Elliptic clipping is an explicit pixel-center approximation: the 40-pixel circle
case differs from modern GDI in 119 boundary pixels, all within 1.5 pixels of
the ideal ellipse boundary. Existing ellipse strokes retain their earlier
documented approximation. These tests and recognizable captures do not prove
historical Win3.1 pixel fidelity or every original settings combination.

This work does not implement audio, settings dialogs, general monochrome
drawing, scaled blits, RLE images, icons, or the proprietary AD_RSRC helper ABI.
The public fixture images and synthetic guest programs are source-owned;
all original modules, extracted resources and captures remain ignored local inputs.
