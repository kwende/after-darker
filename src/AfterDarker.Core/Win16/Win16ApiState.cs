using AfterDarker.Core.Ne;

namespace AfterDarker.Core.Win16;

/// <summary>Per-guest state and explicit host policy, separate from the API implementations.</summary>
public sealed class Win16ApiState
{
    public const uint DefaultWindowsVersion = 0x00000A03;
    public const uint DefaultInitialTick = 0x12345678, DefaultTickStep = 16;
    public IGuestMemory16 Memory { get; }
    public GuestGlobalBlocks Blocks { get; }
    public LocalHeapReservation ReservedHeap { get; }
    public LocalHeapReservation? InitializedHeap { get; internal set; }
    /// <summary>Mapped bytes available after the heap start, including explicitly supplied growth room.</summary>
    public int LocalHeapCapacityBytes { get; }
    /// <summary>Allocator published only by successful LocalInit; allocation state belongs to this guest alone.</summary>
    public Win16LocalHeap? LocalHeap { get; internal set; }
    public FarPointer16? Environment { get; }
    public uint WindowsVersion { get; }
    public bool LocalInitSucceeds { get; }
    public IWin16Clock Clock { get; }
    public uint? LastReturnedTick { get; internal set; }
    public Win16Drawing? Drawing { get; init; }

    public Win16ApiState(IGuestMemory16 memory, LocalHeapReservation reservedHeap,
        FarPointer16? environment = null, uint windowsVersion = DefaultWindowsVersion,
        bool localInitSucceeds = true, uint initialTick = DefaultInitialTick, uint tickStep = DefaultTickStep,
        IWin16Clock? clock = null, int? localHeapCapacityBytes = null)
    {
        if (reservedHeap.Start.Selector == 0 || reservedHeap.Length <= 0 ||
            reservedHeap.Start.Offset + (long)reservedHeap.Length > 65536)
            throw new ArgumentException("Invalid local heap reservation.");
        _ = memory.Read(reservedHeap.Start, reservedHeap.Length);
        if (environment is FarPointer16 address &&
            (address.Selector == 0 || !memory.Read(address, 2).SequenceEqual(new byte[2])))
            throw new ArgumentException("Supply a mapped double-NUL empty DOS environment.");
        Memory = memory;
        Blocks = new(memory);
        ReservedHeap = reservedHeap;
        LocalHeapCapacityBytes = localHeapCapacityBytes ?? reservedHeap.Length;
        if (LocalHeapCapacityBytes < reservedHeap.Length || reservedHeap.Start.Offset + (long)LocalHeapCapacityBytes > 65536)
            throw new ArgumentException("Local heap capacity must contain its initial reservation within one segment.");
        _ = memory.Read(reservedHeap.Start, LocalHeapCapacityBytes);
        Environment = environment;
        WindowsVersion = windowsVersion;
        LocalInitSucceeds = localInitSucceeds;
        Clock = clock ?? new SteppingWin16Clock(initialTick, tickStep);
    }
}
