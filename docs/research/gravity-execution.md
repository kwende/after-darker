# Gravity: original animation with off-screen bitmaps and unavailable audio

Gravity is the twelfth supported original module. Its own x86 code computes the
ball motion, chooses colors, constructs masks and calls GDI. The host now supplies
the missing memory DC/bitmap services and two mask operations. Audio is explicitly
unavailable, as requested by the owner; no sound device, resource decoder or
playback worker is created.

## Artifact and supported configuration

The supported SHA-256 is
`ADDF0B0A5A9F2341C2EE094398F02AC98BC7306BB2938A41905AE66F734E0482`.
It has five NE segments, automatic data segment 5, a 1,024-byte initial local
heap, DLL startup at `S4:0000`, `MODULE` at `S1:003E` and `WEP` at `S1:0027`.
Only that analyzed version is accepted. The private module, disassembly and
captures stay in ignored local directories.

[GravityProfile](../../src/AfterDarker.Runtime/Modules/GravityProfile.cs) supplies
four balls, size 20, Clear Screen enabled and Sound disabled. These are fixed,
tested host controls, not a claim about the original default settings. Dimensions
are restricted to 64–2048 pixels on each axis. The generic speed control is not
used. `AD_SYSTEM.ptAspect` is 1:1 for our square-pixel surface: initialization
divides by its horizontal component, so leaving that SDK field zero causes an
actual guest divide error. This reuses Hard Rain's host-record interpretation.

[GravityState](../../src/AfterDarker.Runtime/Modules/GravityState.cs) exposes the
guest's saved host pointers, bitmap and sound handles, and each ball's color and
position. The named offsets in the profile describe this artifact's globals;
they are observations, not host-written animation state. No machine-code patches
or C# motion simulation are involved.

## The bitmap owns pixels; the DC owns drawing state

Gravity revealed a lifetime distinction which a single display HDC had hidden:

```text
INITIALIZE: create memory DC -> create/select bitmap -> draw the masks
            delete DC -> bitmap remains alive

DRAWFRAME:  create another memory DC -> select that same bitmap
            BitBlt through it -> delete DC -> bitmap remains alive

CLOSE:      delete bitmap -> no owned GDI storage remains
```

The bitmap is a 60x20 RGB strip with three 20x20 tiles, occupying 3,600 bytes.
The original helper at `S2:0012` builds it with two PATCOPY fills and three
Ellipse calls. A DC retains selected pen, brush, bitmap, origin, current point
and ROP2. Deleting it releases its selections, not the separately owned bitmap.
A bitmap may be selected into only one memory DC at a time and cannot be
deleted while selected. Host display DCs cannot be destroyed by guest calls.

The initialization helper uses `DeleteDC`. At `S2:071F`, drawing instead uses
`DeleteObject` on the temporary HDC. Supporting that documented alternative
matters: otherwise the old DC retains the bitmap selection, and the next draw
cannot select it. Public tests compare this lifetime sequence with native GDI,
including reselecting and reading the retained pixels. See Microsoft's
[CreateCompatibleDC remarks](https://learn.microsoft.com/en-us/windows/win32/api/wingdi/nf-wingdi-createcompatibledc)
and [SelectObject contract](https://learn.microsoft.com/en-us/windows/win32/api/wingdi/nf-wingdi-selectobject).

[Win16Drawing.Bitmaps](../../src/AfterDarker.Core/Win16/Win16Drawing.Bitmaps.cs)
owns this shared behavior. Guest handles are small integers in separate bounded
pools, never host pointers. Limits are 256 bitmaps, 64 memory DCs, 32 MiB of bitmap
RGB storage per guest and 2048x2048 per bitmap. Pool/size exhaustion returns a
null allocation handle. New RGB bitmap bytes are deterministically black, an
explicit host policy because native GDI does not promise initial contents.

A new memory DC has default attributes and a stock monochrome bitmap. We model
that stock identity so selection/restoration works, but drawing on it is guarded.
The supported color path creates a compatible bitmap against the display DC,
then selects it into the memory DC. Creating a bitmap against the default
monochrome DC, or requesting zero dimensions, remains unsupported. Supporting
those paths is unnecessary for Gravity's color configuration.

## Masks combine three inputs

Stained Glass needed ordinary source copying. Gravity also uses the source
bitmap as a mask, with the selected brush providing the color. `S2:04F5` and
`S2:052A` apply the two operations at the ball's current coordinates:

| Operation | Per-bit result | Use |
| --- | --- | --- |
| SRCCOPY `00CC0020` | `source` | Existing ordinary image copy |
| DSPDxax `00E20746` | `(source & brush) \| (~source & destination)` | Paint selected brush where the source mask is white; retain destination where black |
| SRCAND `008800C6` | `source & destination` | Apply the second mask's black outline while retaining other pixels |
| PATCOPY `00F00021` | `brush` | Fill the off-screen mask backgrounds; no source bitmap |

These explicit bitmap operations are independent of the DC's ROP2 pen/shape
setting. Gravity's tested configuration leaves trails as successive positions
are drawn. The second mask outlines the current ball; it does not erase a prior
position. Initialization and BLANK clear the display separately.

[BitmapRasterOperation](../../src/AfterDarker.Core/Rendering/BitmapRasterOperation.cs)
names the encodings. [PixelSurface.Blit](../../src/AfterDarker.Core/Rendering/PixelSurface.Blit.cs)
clips both surfaces and snapshots the source before writing, preserving overlaps.
PATCOPY uses [PixelSurface.Pattern](../../src/AfterDarker.Core/Rendering/PixelSurface.Pattern.cs)
to normalize signed extents and copy the brush without applying ROP2. Native
tests cover all three supported BitBlt operations, clipping, independent origins,
self-copy and signed PATCOPY bounds. Their pixels match modern Windows exactly
for those cases. The existing Ellipse raster remains a software approximation;
the bitmap tests do not turn its mask boundaries into historical fidelity proof.

## Audio follows a consistent unavailable-device path

Sound off does not eliminate the helper imports. INITIALIZE unconditionally
opens sound, loads a resource, queries asynchronous capability and sets the
mode. The 1,000-draw run also reaches PlaySound 100 times. CLOSE calls both
CloseSound and FreeSound with the unavailable device/null handle.

The recovered `AD_SND.H` supplies the named `FAR PASCAL` declarations; see
[SDK provenance](after-dark-sdk.md). All seven results are WORD-sized BOOL or
HSOUND values in AX, preserving DX:

| AD_SND import | Pascal argument bytes | Unavailable response |
| --- | ---: | --- |
| ADWOPENSOUND | 0 | FALSE |
| ADWCLOSESOUND | 2 | FALSE; nothing is open or queued |
| ADWSOUNDASYNCCAP | 0 | FALSE |
| ADWLOADSOUNDRESOURCE | 6 | Null HSOUND; instance WORD plus resource-name far pointer |
| ADWSETSOUNDMODE | 4 | FALSE; no sound object exists |
| ADWPLAYSOUND | 2 | FALSE; no playback starts |
| ADWFREESOUND | 2 | FALSE; no sound object exists |

[UnavailableAfterDarkSound](../../src/AfterDarker.Core/AfterDark/UnavailableAfterDarkSound.cs)
holds these stateless implementations separately from CPU hooks and profile
logic. The shared registry now accepts these exact named AD_SND imports; unknown
names still fail symbolically. Parameters are marshaled normally and the ordinary
gateway performs the far return/stack cleanup. Resource pointers are not
dereferenced on this failure path. Public x86 tests exercise every signature,
including an unreadable resource pointer whose data is unnecessary to failure.

**Observed:** Gravity tolerates these failures through drawing and shutdown.
**Boundary:** this implements an unavailable helper, not functional audio or a
guarantee that every other module tolerates the same failures.

## Shared Win16 additions

The new GDI signatures are from Wine 10.0's
[Win16 GDI declarations](https://raw.githubusercontent.com/wine-mirror/wine/wine-10.0/dlls/gdi.exe16/gdi.exe16.spec).
The Microsoft [BitBlt](https://learn.microsoft.com/en-us/windows/win32/api/wingdi/nf-wingdi-bitblt)
and [PatBlt](https://learn.microsoft.com/en-us/windows/win32/api/wingdi/nf-wingdi-patblt)
contracts guide raster behavior; they are not substitutes for Win16 ABI widths.

| Import | Argument bytes | Result in AX |
| --- | ---: | --- |
| GDI #52 CreateCompatibleDC | 2 | HDC |
| GDI #51 CreateCompatibleBitmap | 6 | HBITMAP; width/height are unsigned Win16 WORDs |
| GDI #68 DeleteDC | 2 | BOOL |
| GDI #29 PatBlt | 14 | BOOL; coordinates/extents are signed words, operation is a DWORD |

SelectObject/DeleteObject gain bitmap/DC ownership semantics. Existing BitBlt
retains its 20-byte Pascal frame and gains two operation codes. Unknown bitmap
operations and StretchBlt remain guarded; bitmap resource decoding is not added.

## Verification and reproduction

All **407/407** combined cases pass with Watcom and the twelve private modules
enabled (7m 11s). The default public suite passes **285/285**, with no private
module or compiler required. The full report is local at
`artifacts/gravity/full-regression/AfterDarker.Tests_net10.0_x64.trx`.

Twelve new public cases cover ownership/defaults/capacity, actual x86 arguments,
far returns and failure guards, native raster comparisons and the DeleteObject
DC lifetime. Seven private cases cover 1,000 draws, three further size/seed
combinations, two independent guests, advancing live time and input rejection.
They check resource counts and complete BLANK/CLOSE/WEP with no locks,
allocations, brushes, pens, bitmaps or memory DCs left. WEP returns 1 with the
caller stack and DS restored.

At 320x240 with deterministic timing, the 1,000-draw run executes 1,145,876
instructions before the final BLANK/CLOSE. Its largest draw uses 1,982
instructions. It makes 4,090 BitBlts, 2,045 temporary brushes and 1,001 memory DCs
over its lifetime, while peak ownership is just one brush, one bitmap and one
memory DC. Between draws only the 3,600-byte bitmap remains. The profile allows
200,000 instructions and 512 service exits per invocation, retaining the existing
native-time bounds. These are finite tests, not an indefinite-playback guarantee.

```powershell
$env:AFTER_DARKER_GRAVITY = (Resolve-Path ad/Gravity.ad).Path
# Optional: save every tenth draw, using an ignored output directory.
$env:AFTER_DARKER_GRAVITY_CAPTURE = Join-Path $PWD artifacts/gravity/frames
dotnet test -p:TestLocalGravity=true --filter TestCategory=LocalGravity --report-trx

dotnet run --project src/AfterDarker.Wpf -- 'ad/Gravity.ad'
```

The file can also be selected through **File > Load AD file…**. Actual WPF smoke
runs passed in both directions between Gravity and Stained Glass: 30
presentations, exact WriteableBitmap readback, invalid-file rejection, restart,
close while playing and clean shutdown. Local reports are under
`artifacts/gravity/wpf-to-stained` and `artifacts/gravity/wpf-from-stained`.
PNG captures are local observations, not redistributed artwork.

The reusable mechanism is **a separately owned bitmap, temporarily selected
into a drawing context, with explicit mask operations and checked lifetimes**.
The module still owns every animation decision.
