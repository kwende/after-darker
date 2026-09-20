using AfterDarker.Core.Ne;

namespace AfterDarker.Core.Win16;

/// <summary>Detached allocation evidence; this is bookkeeping, not a copy of guest payload bytes.</summary>
/// <param name="Handle">Win16 identity passed to Lock, Unlock and Free.</param>
/// <param name="Address">Guest data address; offset zero denotes a zero-size discarded movable block.</param>
/// <param name="RequestedBytes">Payload bytes requested by the guest.</param>
/// <param name="ReservedBytes">Aligned extent taken from the free list.</param>
/// <param name="Moveable">Whether the handle needs an identity-to-address lookup.</param>
/// <param name="LockCount">Outstanding movable locks; fixed blocks have no lock count.</param>
public sealed record LocalAllocationSnapshot(ushort Handle, FarPointer16 Address, int RequestedBytes,
    int ReservedBytes, bool Moveable, byte LockCount);

/// <summary>Bounded per-guest heap accounting, independent of the history of allocation calls.</summary>
/// <param name="Selector">DS identifying the owning data segment.</param>
/// <param name="InitialBytes">NE startup reservation.</param>
/// <param name="CapacityBytes">Already mapped backing available to this heap, including growth room.</param>
/// <param name="EnabledBytes">Bytes currently admitted to the allocator; not an OS commit measurement.</param>
/// <param name="FreeBytes">Unused enabled bytes; adjacent holes coalesce on Free.</param>
/// <param name="LargestFreeBlock">Largest contiguous enabled hole; fragmentation can prevent allocation.</param>
/// <param name="Allocations">Live allocations only, sorted by handle.</param>
public sealed record LocalHeapSnapshot(ushort Selector, int InitialBytes, int CapacityBytes, int EnabledBytes,
    int FreeBytes, int LargestFreeBlock, IReadOnlyList<LocalAllocationSnapshot> Allocations)
{
    /// <summary>Locks on movable allocations; unrelated to thread synchronization.</summary>
    public int OutstandingLocks => Allocations.Sum(allocation => allocation.LockCount);
}
