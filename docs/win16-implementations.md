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

State is in [Win16ApiState.cs](../src/AfterDarker.Core/Win16/Win16ApiState.cs).
One instance per guest keeps handles, initialized heaps, versions, and clocks
independent. Making these mutable values static would allow one emulator or
test to affect another. The reusable [GuestGlobalBlocks](../src/AfterDarker.Core/Win16/GuestMemory16.cs)
registry supplies the handle lookup and lock-count mechanics.

The static [Win16Imports](../src/AfterDarker.Core/Win16/Win16Imports.cs) adapter
owns the current initialization import table and ABI conversion. For example,
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
does not expand the supported API surface or change the heap-model limitation.

The direct [Win16ApiTests](../tests/AfterDarker.Tests/Unit/Win16ApiTests.cs) need
no emulator. Existing CPU/gateway tests verify the marshaling around the same
methods, and the opt-in tests exercise both real DLLs.
