using AfterDarker.Core.Ne;

namespace AfterDarker.Core.Win16;

public sealed record LocalHeapReservation(FarPointer16 Start, int Length);

/// <summary>Per-guest state and explicit host policy, separate from the API implementations.</summary>
public sealed class Win16ApiState
{
    public const uint DefaultWindowsVersion = 0x00000A03;
    public const uint DefaultInitialTick = 0x12345678, DefaultTickStep = 16;
    public IGuestMemory16 Memory { get; }
    public GuestGlobalBlocks Blocks { get; }
    public LocalHeapReservation ReservedHeap { get; }
    public LocalHeapReservation? InitializedHeap { get; internal set; }
    public FarPointer16? Environment { get; }
    public uint WindowsVersion { get; }
    public bool LocalInitSucceeds { get; }
    public uint TickStep { get; }
    public uint NextTick { get; internal set; }

    public Win16ApiState(IGuestMemory16 memory, LocalHeapReservation reservedHeap,
        FarPointer16? environment = null, uint windowsVersion = DefaultWindowsVersion,
        bool localInitSucceeds = true, uint initialTick = DefaultInitialTick, uint tickStep = DefaultTickStep)
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
        Environment = environment;
        WindowsVersion = windowsVersion;
        LocalInitSucceeds = localInitSucceeds;
        NextTick = initialTick;
        TickStep = tickStep;
    }
}
