# Reading the Win16 implementations

Start with [Win16Api.cs](../src/AfterDarker.Core/Win16/Win16Api.cs). This is the
class to build on as our Windows support grows. Both the Hello42 and original
Mondrian lessons call its methods.

```csharp
uint GetVersion();
bool LocalInit(ushort dataSelector, ushort start, ushort bytes);
FarPointer16 GlobalLock(ushort handle);
ushort GlobalUnlock(ushort handle);
FarPointer16 GetDOSEnvironment();
uint GetTickCount();
void SetRect(FarPointer16 destination, short left, short top, short right, short bottom);
bool PtInRect(FarPointer16 rectangleAddress, Point16 point);
ushort GetStockObject(short index);
short FillRect(ushort hdc, FarPointer16 rectangle, ushort brush);
void InvertRect(ushort hdc, FarPointer16 rectangle);
```

Read these as ordinary C# functions. Arguments have been read from the
guest stack, which still contains the active call frame. Return values have
not yet been placed into CPU registers.

| Method | What our current implementation does |
| --- | --- |
| GetVersion | Returns the version supplied by the host. |
| LocalInit | Validates the supplied selector and reserved zero-filled heap range; records success or an explicitly configured test failure. It does not build a Windows allocator. |
| GlobalLock | Looks up a resident guest block, checks its backing memory, increments its lock count, and returns its guest address. |
| GlobalUnlock | Releases one lock and reports how many remain. |
| GetDOSEnvironment | Returns the address of a supplied empty guest environment; rejects absent or unsupported contents. |
| GetTickCount | Returns the current virtual tick and advances it by a configured amount. |
| SetRect | Writes the four signed corners unchanged to checked guest memory. |
| GetStockObject | Returns stock black brush (index 4) or black pen (index 7); rejects other indices. Stock objects do not consume created-object capacity. |
| PtInRect | Reads a checked guest RECT and tests a signed by-value POINT; left/top inclusive, right/bottom exclusive. Empty/inverted rectangles return false. |
| FillRect | Resolves the HDC and black brush; fills the clipped rectangle on its persistent surface. |
| InvertRect | Resolves the HDC; inverts the clipped rectangle's RGB bits. |
| CreatePen | Allocates a bounded, reusable guest identity for a supported solid pen. |
| SelectObject | Selects a pen into an HDC and returns the previously selected pen. |
| DeleteObject | Releases an unselected owned pen; keeps stock objects host-owned. |
| MoveTo | Updates the HDC current point and returns its previous coordinates. |
| LineTo | Draws with the selected pen, excludes the endpoint, and advances the current point. |

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

[Win16ImportGateway](../src/AfterDarker.Runtime/Calls/Win16ImportGateway.cs)
coordinates that call. [Win16Stack](../src/AfterDarker.Runtime/Calls/Win16Stack.cs)
reads the frame and later advances SP/restores CS:IP.
[Win16RegisterConvention](../src/AfterDarker.Runtime/Calls/Win16RegisterConvention.cs)
writes word results to AX, DWORD/far-pointer results to DX:AX, and GlobalLock's
additional selector result to CX. Void signatures preserve AX/DX. See the
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
did not change the heap-model limitation. Tutorial 09 adds only the four drawing
methods listed above. `Win16ApiState.Drawing` holds each guest's HDC registry;
[Win16DeviceContext](../src/AfterDarker.Core/Win16/Win16DeviceContext.cs) holds
selected-pen/current-point state. `PixelSurface` owns pixel storage;
`CosmeticLineRasterizer` explains line stepping independently of API marshaling. See
[the drawing lesson](tutorial-09-mondrian-frames.md) for the tested rectangle
boundaries and the Win16 void-return distinction.

The direct [Win16ApiTests](../tests/AfterDarker.Tests/Unit/Win16ApiTests.cs) need
no emulator. Existing CPU/gateway tests verify the marshaling around the same
methods, and the opt-in tests exercise all three supported original modules.

## POINT by value

Rainstorm introduces `PtInRect(const RECT FAR *, POINT)`. The rectangle parameter
is a pointer; the point is two signed words passed as one value. In guest memory
POINT stores X then Y. A Pascal caller pushes Y, then X, so the source-ordered
words given to the dispatcher are `rectangle selector, rectangle offset, Y, X`.
`Win16ArgumentReader.ReadPoint` produces `Point16(X, Y)` explicitly. The API
returns BOOL in AX, preserves DX, and removes eight argument bytes plus the
four-byte far return frame. See [the execution notes](research/rainstorm-execution.md)
for ABI sources and tests that catch coordinate swaps and invalid pointers.
