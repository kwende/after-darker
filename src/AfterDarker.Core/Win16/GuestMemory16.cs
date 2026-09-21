using AfterDarker.Core.Ne;

namespace AfterDarker.Core.Win16;

/// <summary>Guest addresses only. Implementations must check selector, range, and write access.</summary>
public interface IGuestMemory16
{
    byte[] Read(FarPointer16 address, int count);
    void Write(FarPointer16 address, byte[] bytes);
    /// <summary>Check output storage without modifying it, before an external operation with side effects.</summary>
    /// <remarks>Memory providers must opt in to this check before hosting ScrollDC. The default fails closed.</remarks>
    void ValidateWrite(FarPointer16 address, int count) =>
        throw new NotSupportedException("This guest memory provider does not support preflight write validation.");
}

/// <summary>
/// A small registry of host-provided, resident global blocks. A HANDLE is a lookup
/// key, not a pointer. Lock returns the checked guest address behind that key.
/// This models lock lifetime; it does not implement GlobalAlloc, moving, or discard.
/// Invalid handles fail before guest execution resumes with an unusable pointer.
/// </summary>
public sealed class GuestGlobalBlocks(IGuestMemory16 memory)
{
    private sealed class Block(FarPointer16 address, int length)
    {
        public FarPointer16 Address { get; } = address;
        public int Length { get; } = length;
        public ushort Locks { get; set; }
    }
    private readonly Dictionary<ushort, Block> blocks = [];

    public void Register(ushort handle, FarPointer16 address, int length)
    {
        if (handle is 0 or ushort.MaxValue || address.Selector == 0 || length <= 0)
            throw new ArgumentException("Resident block needs a non-null handle, selector, and positive size.");
        _ = memory.Read(address, length); // Ensure backing memory exists before publishing its handle.
        blocks.Add(handle, new(address, length));
    }

    public FarPointer16 Lock(ushort handle)
    {
        Block block = Find(handle);
        _ = memory.Read(block.Address, block.Length);
        block.Locks = checked((ushort)(block.Locks + 1));
        return block.Address;
    }

    // Win16 GlobalUnlock reports remaining locks; zero after the last unlock is
    // expected success, not an allocation failure. Reject unbalanced use here.
    public ushort Unlock(ushort handle)
    {
        Block block = Find(handle);
        if (block.Locks == 0) throw new InvalidOperationException($"Unbalanced GlobalUnlock({handle:X4}).");
        return --block.Locks;
    }

    public int OutstandingLocks => blocks.Values.Sum(b => b.Locks);
    private Block Find(ushort handle) => blocks.TryGetValue(handle, out Block? block) ? block
        : throw new InvalidOperationException($"Unknown global handle {handle:X4}; no guest pointer returned.");
}
