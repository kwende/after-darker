namespace AfterDarker.Core.Win16;

/// <summary>The supported Win16 LocalAlloc flags; other policies fail explicitly.</summary>
[Flags]
public enum LocalMemoryFlags : ushort
{
    /// <summary>The handle is itself the allocation's near offset in the owning DS.</summary>
    Fixed = 0,
    /// <summary>The handle is an identity; LocalLock resolves it to a near offset.</summary>
    Moveable = 0x0002,
    /// <summary>Clear the requested bytes, including when reusing a previously freed range.</summary>
    ZeroInit = 0x0040
}
