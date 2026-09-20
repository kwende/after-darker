using AfterDarker.Core.Ne;

namespace AfterDarker.Core.Win16;

/// <summary>Allocate slices of one already-mapped guest data segment. Start here to understand local heaps.</summary>
/// <remarks>
/// This class owns allocation identities and free ranges, NOT the underlying native memory.
/// The runtime maps that memory once; original x86 loads/stores access it without calling this class.
/// Free returns a slice to this allocator. Disposing the guest releases Unicorn's backing storage.
/// See docs/win16-local-heap.md for the complete ownership and handle-to-address walkthrough.
/// </remarks>
public sealed class Win16LocalHeap
{
    private const int Alignment = 4;
    private const int FirstMoveableHandle = 2;
    private const byte MaximumLockCount = 254;
    private readonly IGuestMemory16 memory;
    private readonly LocalHeapReservation reservation;
    private readonly int capacityBytes;
    private readonly Dictionary<ushort, Allocation> allocations = [];
    private readonly List<FreeRange> freeRanges = [];
    private int enabledEnd;

    // Payload bytes live in guest memory. These small host records only describe ownership.
    private sealed record FreeRange(int Start, int Length);
    private sealed class Allocation(ushort handle, ushort offset, int requestedBytes, int reservedBytes, bool moveable)
    {
        public ushort Handle { get; } = handle;
        public ushort Offset { get; } = offset;
        public int RequestedBytes { get; } = requestedBytes;
        public int ReservedBytes { get; } = reservedBytes;
        public bool Moveable { get; } = moveable;
        public byte LockCount { get; set; }
    }

    /// <summary>Describe a zero-filled mapped reservation, optionally with bounded, already-mapped growth room.</summary>
    public Win16LocalHeap(IGuestMemory16 memory, LocalHeapReservation reservation, int? capacityBytes = null)
    {
        this.memory = memory;
        this.reservation = reservation;
        this.capacityBytes = capacityBytes ?? reservation.Length;
        if (reservation.Start.Selector == 0 || reservation.Length <= 0 || this.capacityBytes < reservation.Length ||
            reservation.Start.Offset + (long)this.capacityBytes > 65536)
            throw new ArgumentException("Invalid local heap backing range.");
        _ = memory.Read(reservation.Start, this.capacityBytes); // No allocation may escape mapped guest memory.
        enabledEnd = reservation.Start.Offset + reservation.Length;
        // A null near pointer must never be an allocation; fixed handles are four-byte aligned.
        int firstOffset = Align(Math.Max(Alignment, (int)reservation.Start.Offset));
        if (firstOffset < enabledEnd) freeRanges.Add(new(firstOffset, enabledEnd - firstOffset));
    }

    /// <summary>Choose a free guest range and return a Win16 handle, or zero when capacity is exhausted.</summary>
    /// <remarks>
    /// Fixed: handle == near offset. Movable: handle -> host Allocation -> near offset.
    /// We keep movable blocks resident in this first implementation; no compaction, discard-on-pressure,
    /// callbacks or historical Windows arena/handle-table bytes are fabricated in guest memory.
    /// </remarks>
    public ushort Allocate(LocalMemoryFlags flags, ushort requestedBytes)
    {
        const LocalMemoryFlags supported = LocalMemoryFlags.Moveable | LocalMemoryFlags.ZeroInit;
        if ((flags & ~supported) != 0) throw new NotSupportedException($"Unsupported LocalAlloc flags 0x{(ushort)flags:X4}.");
        bool moveable = flags.HasFlag(LocalMemoryFlags.Moveable);
        if (!moveable && requestedBytes == 0) return 0;
        ushort handle = moveable ? FindUnusedMoveableHandle() : (ushort)0;
        if (moveable && handle == 0) return 0;

        int reservedBytes = Align(requestedBytes);
        int rangeIndex = -1;
        ushort offset = 0;
        if (reservedBytes != 0)
        {
            rangeIndex = FindFreeRange(reservedBytes);
            if (rangeIndex < 0)
            {
                EnableGrowthRoom();
                rangeIndex = FindFreeRange(reservedBytes);
            }
            if (rangeIndex < 0) return 0; // Real guest allocation failure; never a fake usable handle.
            offset = checked((ushort)freeRanges[rangeIndex].Start);
            if (flags.HasFlag(LocalMemoryFlags.ZeroInit))
                memory.Write(new(reservation.Start.Selector, offset), new byte[requestedBytes]);
        }
        if (!moveable) handle = offset;
        allocations.Add(handle, new(handle, offset, requestedBytes, reservedBytes, moveable));
        if (rangeIndex >= 0) TakeFreeRange(rangeIndex, reservedBytes);
        return handle;
    }

    /// <summary>Resolve a handle to a guest near offset and acquire a movable lock.</summary>
    /// <remarks>
    /// "Lock" pins an allocation's location conceptually; it is not a mutex. No memory is copied.
    /// Zero-size movable handles are discarded and return offset zero. Fixed handles are already offsets.
    /// </remarks>
    public ushort Lock(ushort handle)
    {
        if (handle == 0) return 0;
        Allocation allocation = FindAllocation(handle);
        if (allocation.Offset == 0) return 0;
        if (allocation.Moveable && allocation.LockCount < MaximumLockCount) allocation.LockCount++;
        return allocation.Offset;
    }

    /// <summary>Release a movable lock and return the remaining count; zero after the last lock is normal.</summary>
    public ushort Unlock(ushort handle)
    {
        if (handle == 0) return 0;
        Allocation allocation = FindAllocation(handle);
        if (!allocation.Moveable || allocation.LockCount == 0) return 0;
        return --allocation.LockCount;
    }

    /// <summary>Release the identity and return its extent to the free list; zero means success.</summary>
    /// <remarks>
    /// This does NOT free an OS allocation, clear payload bytes, or unmap guest memory.
    /// As in the Win16 reference, a local block can be freed while locked; all its pointers then expire.
    /// Unknown/double-freed handles fail diagnostically instead of corrupting allocator metadata.
    /// </remarks>
    public ushort Free(ushort handle)
    {
        if (handle == 0) return 0;
        Allocation allocation = FindAllocation(handle);
        allocations.Remove(handle);
        if (allocation.ReservedBytes != 0) ReturnFreeRange(allocation.Offset, allocation.ReservedBytes);
        return 0;
    }

    /// <summary>Return detached, inspectable ownership and capacity data without reading guest payloads.</summary>
    public LocalHeapSnapshot Snapshot() => new(reservation.Start.Selector, reservation.Length, capacityBytes,
        enabledEnd - reservation.Start.Offset, freeRanges.Sum(range => range.Length),
        freeRanges.Count == 0 ? 0 : freeRanges.Max(range => range.Length),
        Array.AsReadOnly(allocations.Values.OrderBy(allocation => allocation.Handle).Select(allocation =>
            new LocalAllocationSnapshot(allocation.Handle, new(reservation.Start.Selector, allocation.Offset),
                allocation.RequestedBytes, allocation.ReservedBytes, allocation.Moveable, allocation.LockCount)).ToArray()));

    /// <summary>Enforce the implicit Win16 heap selector before interpreting a handle from the caller.</summary>
    public void RequireOwner(ushort dataSelector)
    {
        if (dataSelector != reservation.Start.Selector)
            throw new NotSupportedException($"Local heap belongs to DS={reservation.Start.Selector:X4}, not caller DS={dataSelector:X4}.");
    }

    private Allocation FindAllocation(ushort handle) => allocations.TryGetValue(handle, out Allocation? allocation)
        ? allocation : throw new InvalidOperationException($"Unknown local handle {handle:X4}; no guest address returned.");

    // Keep fixed offsets (low bits 00) distinct from movable identities (low bits 10).
    // These are our bounded opaque tokens, NOT offsets into a Windows handle table.
    private ushort FindUnusedMoveableHandle()
    {
        for (int candidate = FirstMoveableHandle; candidate <= ushort.MaxValue; candidate += Alignment)
            if (!allocations.ContainsKey((ushort)candidate)) return (ushort)candidate;
        return 0;
    }

    private int FindFreeRange(int length) => freeRanges.FindIndex(range => range.Length >= length);
    private static int Align(int bytes) => (bytes + Alignment - 1) & ~(Alignment - 1);

    /// <summary>Admit the pre-mapped tail on demand, never growing beyond this data segment.</summary>
    private void EnableGrowthRoom()
    {
        int maximumEnd = reservation.Start.Offset + capacityBytes;
        if (maximumEnd == enabledEnd) return;
        // Existing ranges end at enabledEnd, which need not be aligned in every NE file.
        // Coalesce first so a trailing free range can cross the initial boundary.
        ReturnFreeRange(enabledEnd, maximumEnd - enabledEnd);
        enabledEnd = maximumEnd;
        for (int index = freeRanges.Count - 1; index >= 0; index--)
        {
            FreeRange range = freeRanges[index];
            int alignedStart = Align(Math.Max(Alignment, range.Start));
            int length = range.Start + range.Length - alignedStart;
            if (length <= 0) freeRanges.RemoveAt(index);
            else freeRanges[index] = new(alignedStart, length);
        }
    }

    private void TakeFreeRange(int index, int length)
    {
        FreeRange range = freeRanges[index];
        if (range.Length == length) freeRanges.RemoveAt(index);
        else freeRanges[index] = new(range.Start + length, range.Length - length);
    }

    /// <summary>Merge adjacent holes so freed neighboring allocations can serve a larger request.</summary>
    private void ReturnFreeRange(int start, int length)
    {
        freeRanges.Add(new(start, length));
        freeRanges.Sort((left, right) => left.Start.CompareTo(right.Start));
        for (int index = freeRanges.Count - 2; index >= 0; index--)
        {
            FreeRange left = freeRanges[index], right = freeRanges[index + 1];
            if (left.Start + left.Length != right.Start) continue;
            freeRanges[index] = new(left.Start, left.Length + right.Length);
            freeRanges.RemoveAt(index + 1);
        }
    }
}
