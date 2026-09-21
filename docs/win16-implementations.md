# Reading the Win16 implementations

Start with [Win16Api.cs](../src/AfterDarker.Core/Win16/Win16Api.cs). This is the
class to build on as our Windows support grows. Both the Hello42 and original
Mondrian lessons call its methods.

```csharp
uint GetVersion();
bool LocalInit(ushort dataSelector, ushort start, ushort bytes);
ushort LocalAlloc(ushort dataSelector, LocalMemoryFlags flags, ushort bytes);
FarPointer16 LocalLock(ushort dataSelector, ushort handle);
ushort LocalUnlock(ushort dataSelector, ushort handle);
ushort LocalFree(ushort dataSelector, ushort handle);
FarPointer16 GlobalLock(ushort handle);
ushort GlobalUnlock(ushort handle);
FarPointer16 GetDOSEnvironment();
uint GetTickCount();
void SetRect(FarPointer16 destination, short left, short top, short right, short bottom);
bool PtInRect(FarPointer16 rectangleAddress, Point16 point);
ushort GetStockObject(short index);
short FillRect(ushort hdc, FarPointer16 rectangle, ushort brush);
void InvertRect(ushort hdc, FarPointer16 rectangle);
bool Ellipse(ushort hdc, short left, short top, short right, short bottom);
```

Read these as ordinary C# functions. Arguments have been read from the
guest stack, which still contains the active call frame. Return values have
not yet been placed into CPU registers.

| Method | What our current implementation does |
| --- | --- |
| GetVersion | Returns the version supplied by the host. |
| LocalInit | Validates the selector and zero-filled initial reservation, then publishes the shared local allocator; configured failure remains available to tutorial 06. |
| LocalAlloc | Allocates a bounded slice of guest data memory and returns a fixed offset or movable identity. Zero-init clears reused payload bytes. |
| LocalLock | Resolves a local handle under caller DS and pins a movable block; AX is the near offset, DX its selector. |
| LocalUnlock | Decreases a movable lock count; zero after the last lock is normal. |
| LocalFree | Releases the identity and coalesces its free extent; it does not unmap memory or free an OS allocation. |
| GlobalLock | Looks up a resident guest block, checks its backing memory, increments its lock count, and returns its guest address. |
| GlobalUnlock | Releases one lock and reports how many remain. |
| GetDOSEnvironment | Returns the address of a supplied empty guest environment; rejects absent or unsupported contents. |
| GetTickCount | Reads the per-guest clock: deterministic stepping for tests/captures or monotonic elapsed time for live playback. USER #15 GetCurrentTime uses this same method. |
| SetRect | Writes the four signed corners unchanged to checked guest memory. |
| GetStockObject | Returns stock white/black/null brushes (0/4/5) and white/black/null pens (6/7/8); rejects other indices. Stock objects do not consume created-object capacity. |
| IsRectEmpty | Reads a checked signed RECT; nonpositive width or height returns TRUE without mutation. |
| PtInRect | Reads a checked guest RECT and tests a signed by-value POINT; left/top inclusive, right/bottom exclusive. Empty/inverted rectangles return false. |
| FillRect / FrameRect | Use the explicit brush to fill or border a clipped rectangle, independently of the selected brush and ROP2. |
| InvertRect | Resolves the HDC; inverts the clipped rectangle's RGB bits. |
| CreatePen | Allocates a bounded, reusable guest identity retaining solid RGB color and width 0–3. Width zero becomes one. |
| CreateSolidBrush | Allocates a bounded guest brush using RGB/PALETTERGB components directly; palette-index colors fail before allocation. |
| SelectObject | Selects a supported pen/brush/bitmap into its own HDC slot and returns the previous object of that kind. A bitmap requires a memory DC and cannot be selected by two DCs. |
| DeleteObject | Releases an owned pen/brush/bitmap only when no HDC selects it; also deletes owned regions and memory DCs. Selected region geometry was copied and survives handle deletion. Stock objects stay host-owned. |
| MoveTo | Updates the HDC current point and returns its previous coordinates. |
| LineTo | Draws with a one-pixel pen or approximated width-three pen and advances the current point. A null pen only moves the point; a width-two pen fails before mutation. |
| Ellipse | Uses the selected solid brush and optional pen without changing the current point; software raster policy is documented in the Hard Rain/Shapes guides. |
| Rectangle | Uses the selected solid brush and null/one-pixel pen, preserving the current point; native-tested bounds include NULL_PEN's additional right/bottom contraction. |
| CreateCompatibleDC / DeleteDC | Allocate/release a bounded memory DC with default attributes; deleting it releases selections but retains separately owned bitmap pixels. |
| CreateCompatibleBitmap | Allocate bounded RGB storage against a color DC, returning an owned guest handle; unsupported monochrome requests fail explicitly. |
| BitBlt | Combine source/destination pixels using SRCCOPY, SRCAND, SRCPAINT or the supported brush-through-mask operation; preserve overlapping source pixels. Destination clipping applies, source clipping does not. |
| Polygon | Read checked signed vertices, fill alternate crossing pairs and close the selected cosmetic outline; preserve the current position. |
| LoadBitmap | Resolve a checked guest name/ordinal through NE directory/alias tables, decode an uncompressed DIB and allocate an owned bitmap. |
| CreateRectRgnIndirect / CreateEllipticRgnIndirect | Read a checked signed RECT and allocate bounded immutable device-coordinate geometry. Ellipse edges use the documented approximation. |
| SelectClipRgn | Copy geometry into the DC, or clear the explicit clip for handle zero; return region complexity. |
| SetTextColor / SetBkColor / SetBkMode | Retain per-DC attributes and return previous values. Text/background colors also expand zero/one bits of monochrome source bitmaps. |
| PatBlt | Fill using the selected brush and PATCOPY, independently of ROP2; reject other operation codes. |
| AD_SND named calls | Report unavailable audio and null sound resources through the separate stateless implementation; no decoding, playback or audio handles. |

State is in [Win16ApiState.cs](../src/AfterDarker.Core/Win16/Win16ApiState.cs).
One instance per guest keeps handles, initialized heaps, versions, and clocks
independent. Making these mutable values static would allow one emulator or
test to affect another. The reusable [GuestGlobalBlocks](../src/AfterDarker.Core/Win16/GuestMemory16.cs)
registry supplies the handle lookup and lock-count mechanics.

[Win16Imports](../src/AfterDarker.Core/Win16/Win16Imports.cs) owns import identities,
synthetic addresses, and ABI metadata. [Win16ApiDispatcher](../src/AfterDarker.Core/Win16/Win16ApiDispatcher.cs)
uses [Win16ArgumentReader](../src/AfterDarker.Core/Win16/Win16ArgumentReader.cs) to
turn source-ordered words into named, typed arguments. The API implementation
need not know any register names.

Local heap methods have an additional input: `dataSelector` comes from the
gateway's snapshot of caller DS, not a Pascal stack argument. See
[Win16CallContext](../src/AfterDarker.Core/Win16/Win16CallContext.cs).
The prominent [heap guide](win16-local-heap.md) leads to
`Win16LocalHeap.Allocate/Lock/Unlock/Free` and tutorial 10's actual x86 writes.

[Win16ImportGateway](../src/AfterDarker.Runtime/Calls/Win16ImportGateway.cs)
coordinates that call. [Win16Stack](../src/AfterDarker.Runtime/Calls/Win16Stack.cs)
reads the frame and later advances SP/restores CS:IP.
[Win16RegisterConvention](../src/AfterDarker.Runtime/Calls/Win16RegisterConvention.cs)
writes word results to AX, DWORD/far-pointer results to DX:AX, and GlobalLock's
additional selector result to CX. LocalAlloc also returns its handle in CX.
Void signatures preserve AX/DX. See the
[runtime code map](runtime-code-map.md) for a complete worked stack example.

Tutorial 06 retains its smaller teaching implementation and sends its two
supported calls to the same Win16Api methods.

```text
Guest CALL FAR -> gateway -> decode arguments
                               |
                               v
                       Win16Api.Method(...)
                               |
                               v
                       ordinary C# result
                               |
                               v
                  write registers -> far return -> resume
```

`DosClock` remains separate: those are DOS interrupt services, not Windows
imports. Unsupported Windows calls still fail by name. Extracting these methods
originally retained a reservation-only heap; the Lasers increment now adds the
bounded allocator described above. Tutorial 09 adds only the four drawing
methods listed above. `Win16ApiState.Drawing` holds each guest's HDC registry;
[Win16DeviceContext](../src/AfterDarker.Core/Win16/Win16DeviceContext.cs) holds
the selected pen, selected brush and current point. `PixelSurface` owns pixel
storage; `CosmeticLineRasterizer` and `EllipseRasterizer` explain their respective
boundary stepping independently of API marshaling. See
[the drawing lesson](tutorial-09-mondrian-frames.md) for the tested rectangle
boundaries and the Win16 void-return distinction.

The direct [Win16ApiTests](../tests/AfterDarker.Tests/Unit/Win16ApiTests.cs) need
no emulator. Existing CPU/gateway tests verify the marshaling around the same
methods, and the opt-in tests exercise all sixteen supported original modules.

Zot! adds the `USER!GetCurrentTime` identity with zero Pascal argument bytes
and a DWORD return in DX:AX. Both it and `GetTickCount` bind to `Handler.Ticks`;
there is no second clock or additional timer service. Wine 10.0's
[Win16 USER declarations](https://raw.githubusercontent.com/wine-mirror/wine/wine-10.0/dlls/user.exe16/user.exe16.spec)
establish this alias. Public gateway tests check clock sharing, unsigned wrap,
register results and stack cleanup. See [Zot!'s execution notes](research/zot-execution.md).

Fade Away's Radar path adds no Windows API behavior. Its other styles import
Ellipse, Rectangle and PatBlt. Hard Rain implements Ellipse and Shapes adds
Rectangle; Gravity adds the PATCOPY subset of PatBlt. This does not
enable other Fade Away styles. Initial white pixels are supplied
by the host through `PixelSurface.LoadRgb`, not by a fake Windows call. See the
[Fade Away notes](research/fade-away-execution.md).

Hard Rain's `Ellipse` is GDI ordinal 24, with ten argument bytes and a BOOL in
AX. The default white brush and explicitly selected black brush have host-owned
identities; neither consumes the pen pool. `SelectObject` restores the correct
object kind by looking up the handle, not by guessing from the last drawing call.

Shapes adds GDI #66 CreateSolidBrush (four argument bytes, HBRUSH in AX) and
GDI #27 Rectangle (ten argument bytes, BOOL in AX). Both preserve DX and use the
existing far-return cleanup. `Win16Color` holds the RGB/PALETTERGB policy;
`Win16Drawing` owns separate bounded brush and pen handle pools. Playback results
and shutdown checks include both kinds. Stained Glass subsequently extends
FillRect to other explicit brushes. See [Shapes' evidence and limits](research/shapes-execution.md).
See [the pen/brush, aspect-ratio and raster walkthrough](research/hard-rain-execution.md).

## Bitmaps and silent sound helpers

[Win16Api.Bitmaps](../src/AfterDarker.Core/Win16/Win16Api.Bitmaps.cs) exposes the
typed API methods; [Win16Drawing.Bitmaps](../src/AfterDarker.Core/Win16/Win16Drawing.Bitmaps.cs)
owns storage, handle ranges and selection checks. The
[Gravity execution guide](research/gravity-execution.md) follows its retained
bitmap across short-lived DCs and explains the native-tested ROP3 mask formulas.

[UnavailableAfterDarkSound](../src/AfterDarker.Core/AfterDark/UnavailableAfterDarkSound.cs)
is static because it owns no devices, handles or queues. It implements seven
named SDK functions with consistent failure/null results. The same registry,
argument reader and far-return mechanism used for ordinal Windows imports serve
these named helper imports. It is a tested unavailable-audio path, not a working
audio implementation or a generic success fallback.

## POINT by value

Rainstorm introduces `PtInRect(const RECT FAR *, POINT)`. The rectangle parameter
is a pointer; the point is two signed words passed as one value. In guest memory
POINT stores X then Y. A Pascal caller pushes Y, then X, so the source-ordered
words given to the dispatcher are `rectangle selector, rectangle offset, Y, X`.
`Win16ArgumentReader.ReadPoint` produces `Point16(X, Y)` explicitly. The API
returns BOOL in AX, preserves DX, and removes eight argument bytes plus the
four-byte far return frame. See [the execution notes](research/rainstorm-execution.md)
for ABI sources and tests that catch coordinate swaps and invalid pointers.
