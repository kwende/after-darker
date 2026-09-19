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
ushort GetStockObject(short index);
short FillRect(ushort hdc, FarPointer16 rectangle, ushort brush);
void InvertRect(ushort hdc, FarPointer16 rectangle);
```

Read these as ordinary C# functions. Arguments have already been taken off the
guest stack; return values have not yet been placed into CPU registers.

| Method | What our current implementation does |
| --- | --- |
| GetVersion | Returns the version supplied by the host. |
| LocalInit | Validates the supplied selector and reserved zero-filled heap range; records success or an explicitly configured test failure. It does not build a Windows allocator. |
| GlobalLock | Looks up a resident guest block, checks its backing memory, increments its lock count, and returns its guest address. |
| GlobalUnlock | Releases one lock and reports how many remain. |
| GetDOSEnvironment | Returns the address of a supplied empty guest environment; rejects absent or unsupported contents. |
| GetTickCount | Returns the current virtual tick and advances it by a configured amount. |
| SetRect | Writes the four signed corners unchanged to checked guest memory. |
| GetStockObject | Returns the guest stock-black-brush identity for index 4; rejects others. |
| FillRect | Resolves the HDC and black brush; fills the clipped rectangle on its persistent surface. |
| InvertRect | Resolves the HDC; inverts the clipped rectangle's RGB bits. |

State is in [Win16ApiState.cs](../src/AfterDarker.Core/Win16/Win16ApiState.cs).
One instance per guest keeps handles, initialized heaps, versions, and clocks
independent. Making these mutable values static would allow one emulator or
test to affect another. The reusable [GuestGlobalBlocks](../src/AfterDarker.Core/Win16/GuestMemory16.cs)
registry supplies the handle lookup and lock-count mechanics.

The static [Win16Imports](../src/AfterDarker.Core/Win16/Win16Imports.cs) adapter
owns the current initialization/drawing import table and ABI conversion. For example,
`GlobalLock` returns a `FarPointer16`; the adapter arranges that value for DX:AX
and the additional Win16 CX result. The API implementation need not know any
register names. Tutorial 06 retains its small earlier binding table and converts
its two calls to the same API methods.

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
`PixelSurface` owns raster operations separately from API marshaling. See
[the drawing lesson](tutorial-09-mondrian-frames.md) for the tested rectangle
boundaries and the Win16 void-return distinction.

The direct [Win16ApiTests](../tests/AfterDarker.Tests/Unit/Win16ApiTests.cs) need
no emulator. Existing CPU/gateway tests verify the marshaling around the same
methods, and the opt-in tests exercise both real DLLs.
