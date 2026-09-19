namespace AfterDarker.Runtime;

/// <summary>Our guest address-space choices, kept separate from NE metadata and Windows ABI rules.</summary>
/// <remarks>
/// Every DLL segment gets a slot first; caller, gateway, stack and host records follow it.
/// This prevents a larger DLL from colliding with the five-segment tutorial's host slots.
/// Selectors index eight-byte GDT entries; linear bases are spaced 64 KiB apart by host policy.
/// See docs/runtime-code-map.md and docs/tutorial-07-relocations.md.
/// </remarks>
internal sealed class SessionMemoryLayout
{
    /// <summary>Offset immediately above the empty 4 KiB descending stack.</summary>
    public const ushort EmptyStackPointer = 0x1000;
    /// <summary>AD_SYSTEM begins here in the host-data segment.</summary>
    public const ushort SystemOffset = 0x100;
    /// <summary>AD_MODULE begins here in the host-data segment.</summary>
    public const ushort ModuleOffset = 0x200;
    /// <summary>Two zero bytes here represent an empty DOS environment string list.</summary>
    public const ushort EnvironmentOffset = 0x300;

    /// <summary>Code selector containing our synthetic lifecycle callers.</summary>
    public ushort Caller { get; }
    /// <summary>Code selector whose entries trigger managed import dispatch.</summary>
    public ushort Gateway { get; }
    /// <summary>Writable selector used as SS throughout the session.</summary>
    public ushort Stack { get; }
    /// <summary>Writable selector holding host-supplied records and guest-written results.</summary>
    public ushort HostData { get; }

    /// <summary>Assign host slots immediately after all DLL segments.</summary>
    public SessionMemoryLayout(int moduleSegmentCount)
    {
        Caller = checked((ushort)((moduleSegmentCount + 1) * SegmentedGuest.DescriptorBytes));
        Gateway = checked((ushort)(Caller + SegmentedGuest.DescriptorBytes));
        Stack = checked((ushort)(Gateway + SegmentedGuest.DescriptorBytes));
        HostData = checked((ushort)(Stack + SegmentedGuest.DescriptorBytes));
    }

    /// <summary>Convert a selector slot to this host's chosen linear base, not a general selector translation.</summary>
    public static uint LinearBase(ushort selector) => (uint)(selector / SegmentedGuest.DescriptorBytes) << 16;
}
