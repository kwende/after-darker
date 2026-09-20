# The local heap: who owns the bytes?

Start in [Win16LocalHeap](../src/AfterDarker.Core/Win16/Win16LocalHeap.cs).
Its `Allocate`, `Lock`, `Unlock`, and `Free` methods contain the mechanism.
The [runnable lesson](../src/AfterDarker.Tutorials/Lessons/Tutorial10LocalHeap.cs)
lets x86 exercise those same implementations through the real import gateway.
Original [Lasers](research/lasers-execution.md), [Magic](research/magic-execution.md),
[String Theory](research/string-theory-execution.md) and [Zot!](research/zot-execution.md)
use this same allocator. Their animation code writes the payloads directly,
without module-specific logic in the heap.

```powershell
dotnet run --project src/AfterDarker.Tutorials --no-launch-profile -- 10
```

For F5, choose **Tutorial 10 - local heap and guest writes** in the existing
console app. No proprietary module or Watcom installation is required.

## Two allocations at different levels

The host is a middleman, but a guest allocation does not require a separate
Windows allocation each time. We acquire backing storage for a guest segment,
then manage smaller allocations *inside* that storage.

```text
Windows virtual memory / native allocator
    |
    | Unicorn MemMap owns backing storage for an emulated address range
    v
Guest data segment (one selector, offsets 0000..FFFF at most)
    [DLL globals][initial local heap][optional room for heap growth]
                       |
                       | LocalAlloc selects an unused contiguous slice
                       v
                [one module allocation]
```

There are three distinct kinds of object:

| Object | Owner | Meaning |
| --- | --- | --- |
| Native backing storage | Unicorn, in our process | Actual storage used to emulate mapped bytes; released with the guest engine |
| Allocation metadata | `Win16LocalHeap`, one per guest | Handle, offset, size, locks, free ranges; contains no copy of the payload |
| Payload contents | Original x86 module | Its ray history, records, etc.; read and written with normal guest instructions |

`SegmentedGuest.Map` calls `MemMap` and copies the prepared segment into that
mapping. The loader's managed byte array is a preparation buffer, not the live
heap. After mapping, guest instructions and `IGuestMemory16.Read/Write` both
access Unicorn's backing storage.

When guest code executes `MOV [DS:BX],AX`, Unicorn translates **guest** DS:BX
to guest linear memory and performs that write in its backing storage. It does
not call `LocalAlloc` or dispatch a C# handler for each ordinary memory write.
Neither the guest selector nor the near offset is a native host pointer.

## Why have a handle as well as an address?

| Allocation kind | LocalAlloc returns | LocalLock returns | Lock lifetime |
| --- | --- | --- | --- |
| Fixed (`0`) | The near offset itself | The same near offset | No lock count |
| Movable (`LMEM_MOVEABLE = 2`) | An opaque allocation identity | Its current near offset | Counts outstanding pins |

A handle identifies *which allocation*. A pointer identifies *where its bytes
currently live*. In a heap which compacts memory, an unlocked movable block
could move while its handle stays the same. A lock prevents movement while the
caller uses the pointer; this is a memory-management lock, not a thread mutex.
Our first implementation keeps all blocks resident and does not compact them,
but preserves the handle/address distinction and lock accounting.

In the lesson, the actual observations are:

```text
LocalAlloc(MOVEABLE | ZEROINIT, 32) -> handle 0002
LocalLock(0002), caller DS=0018    -> offset 0400
                                     full guest address 0018:0400
MOV BX,AX
MOV word [DS:BX],BEEF              -> payload changes in mapped guest memory
```

`0002` is our synthetic identity in a host dictionary. It is **not** a native
pointer or an address of a reconstructed Windows handle-table entry. Fixed
offsets are four-byte aligned; movable identities have low bits `10`, keeping
the two namespaces disjoint. Guest code using these APIs need not know our
metadata layout. Code inspecting private Windows heap structures is unsupported.

The original modules now exercise both allocation kinds:

| Module | Allocation | Lifetime |
| --- | --- | --- |
| Lasers | Movable, zeroed, 1,836 bytes | INITIALIZE through CLOSE |
| Magic | Movable, zeroed, 1,520 bytes | INITIALIZE through CLOSE |
| String Theory | Movable, zeroed, 4,560 bytes | INITIALIZE through CLOSE |
| Zot! | Fixed, 800 bytes plus 1,600 bytes | Allocated and freed within each strike's DRAWFRAME |

Zot!'s typical fixed handles `03E8` and `0708` are already offsets in DS `0028`.
LocalLock returns those same offsets, with no movable lock count. The earlier
modules instead receive a synthetic identity such as `0002`, then resolve it
to their payload offset. No new allocator code was needed for this distinction.

Local handles are meaningful within a particular **DS**, unlike the explicit
far pointers returned by the supplied global-record registry. The gateway
samples DS into [Win16CallContext](../src/AfterDarker.Core/Win16/Win16CallContext.cs).
The API verifies it against the heap's owner before interpreting any handle.
It does not silently substitute the DLL's selector when the caller supplies
another DS.

## What Free does

```text
LocalUnlock(handle) -> reduce pin count; allocation and contents still exist
LocalFree(handle)   -> remove identity; return its extent to the free list
next LocalAlloc    -> may reuse that extent
guest Dispose      -> release the emulator and its native mappings
```

Free does not clear payload bytes or call Windows to release one allocation.
Adjacent free extents merge so they can serve a larger request. A normal
reallocation may contain previous data. `LMEM_ZEROINIT` explicitly clears the
requested payload bytes before the new handle is returned. The lesson observes
both behaviors, then leaves no live allocations.

The underlying native allocator and Windows control when released backing
becomes reusable or changes the process's working set. No per-LocalFree OS
deallocation or immediate physical-RAM reduction is claimed.

As in the Win16 reference implementation, a local block can be freed while
locked. That invalidates its pointers and removes its lock accounting. A stale
raw guest pointer may still address mapped bytes; this heap is not a
use-after-free detector. Unknown or double-freed handles are diagnosed at API
boundaries. Reused 16-bit identities cannot detect every stale-handle error.

## Initial size and growth capacity are different

Lasers' NE header asks for an **initial 1,024-byte heap**, but its fixed three-ray
configuration requests **1,836 bytes**: three 612-byte history records. Treating
the header value as a permanent allocation ceiling would fail initialization.

For profiles which opt in to `AllowLocalHeapGrowth`, `AfterDarkSession.MapLibrarySegments`
maps the automatic data segment to 65,536 bytes before execution. The initial
heap reservation and startup CX value remain those from the NE header. The
allocator admits the remaining mapped tail only when no existing free range
fits. All offsets remain within that same selector.

For the tested Lasers artifact, static allocation ends at `04B8`, leaving
64,328 bytes of bounded heap capacity. Its first allocation triggers growth
from 1,024 enabled bytes to that capacity. This eager backing / on-demand
allocator growth policy is ours; it does not reproduce historical Windows
arena headers, handle-table overhead, segment resizing or compaction choices.
Magic, String Theory and Zot! opt into this same growth policy. The other
profiles keep their original reservation-sized mappings.

`LocalHeapSnapshot` exposes the initial size, capacity, enabled bytes, free
bytes, largest free block and live allocations. **Enabled bytes are allocator
accounting, not a measurement of Windows committed memory.** Fragmentation or
exhaustion returns zero even when a caller would prefer a larger block.

## API contract and explicit limits

| Import | Pascal argument bytes | Return implemented here |
| --- | ---: | --- |
| KERNEL #4 LocalInit | 6: selector, zero start, initial bytes | AX boolean; establishes the checked reservation |
| KERNEL #5 LocalAlloc | 4: flags, unsigned byte count | AX handle or zero; CX also receives the handle |
| KERNEL #7 LocalFree | 2: handle | AX zero on success; unknown handles fail diagnostically |
| KERNEL #8 LocalLock | 2: handle | AX near offset; DX carries caller DS as in the reference implementation |
| KERNEL #9 LocalUnlock | 2: handle | AX remaining movable locks; zero is normal after the final unlock |

All use the existing far Pascal frame decoder/return path. LocalLock's caller
may use only AX with its existing DS; Lasers does exactly that. These details
come from Wine 10.0's [export declarations](https://github.com/wine-mirror/wine/blob/wine-10.0/dlls/krnl386.exe16/krnl386.exe16.spec)
and [LocalAlloc16, LocalFree16, LocalLock16, LOCAL_InternalLock and LocalUnlock16](https://github.com/wine-mirror/wine/blob/wine-10.0/dlls/krnl386.exe16/local.c),
paired with observed original-module calls. Wine is reference evidence, not
vendored code, a new dependency, or a Windows 3.1 conformance oracle.

Supported flags are fixed/movable and zero-init. Fixed zero-size allocation
returns zero; movable zero-size allocation returns a discarded identity whose
lock offset is zero. Movable lock counts saturate at 254 as in the reference.
Unknown flags fail explicitly. LocalReAlloc, LocalSize, LocalHandle, callbacks,
discard-on-pressure, compaction, general global allocation, and task scheduling
remain outside this increment. Heap bookkeeping is host-owned, bounded by the
16-bit address/handle space, and never shared between guest sessions.

## Where to step and what to verify

1. [Tutorial10LocalHeap.Execute](../src/AfterDarker.Tutorials/Lessons/Tutorial10LocalHeap.cs): source-authored x86, one mapping and two bounded execution phases.
2. [Win16Api.LocalAlloc / LocalLock / LocalFree](../src/AfterDarker.Core/Win16/Win16Api.cs): API names, implicit DS and ordinary C# values.
3. [Win16LocalHeap](../src/AfterDarker.Core/Win16/Win16LocalHeap.cs): range choice, handle lookup, zeroing, growth and merging free ranges.
4. [Win16ImportGateway](../src/AfterDarker.Runtime/Calls/Win16ImportGateway.cs): capture DS, decode stack, invoke the service, return to x86.
5. [LocalHeapTests](../tests/AfterDarker.Tests/Unit/LocalHeapTests.cs) and [LocalHeapExecutionTests](../tests/AfterDarker.Tests/Conformance/LocalHeapExecutionTests.cs): allocator boundaries versus actual CPU/ABI behavior.
6. [Lasers execution evidence](research/lasers-execution.md): the same mechanism serving an original module.

The durable distinction: **mapping provides bytes; the heap provides ownership;
locking resolves identity to location; guest instructions use that location.**
