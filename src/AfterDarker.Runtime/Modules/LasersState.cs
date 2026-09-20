using AfterDarker.Core.Ne;

namespace AfterDarker.Runtime;

/// <summary>Globals read from the supported original Lasers artifact, not host-generated animation state.</summary>
/// <param name="Compatibility">Guest PREINITIALIZE acceptance flag.</param>
/// <param name="InstanceHandle">Data selector saved by DLL startup.</param>
/// <param name="RayCount">Number of independent rays, chosen by the guest from control 1.</param>
/// <param name="HistoryLength">Trail length chosen by the original width lookup.</param>
/// <param name="ColorInterval">Guest color-change threshold from control 3.</param>
/// <param name="RayHistoryHandle">Movable local allocation owned by the module until CLOSE.</param>
/// <param name="RayHistoryOffset">Near offset last obtained from LocalLock, relative to the module's DS.</param>
/// <param name="DrawCount">Original cycle counter; resets when the module regenerates its rays.</param>
/// <param name="System">Locked AD_SYSTEM guest address.</param>
/// <param name="Module">Locked AD_MODULE guest address.</param>
public sealed record LasersState(ushort Compatibility, ushort InstanceHandle, ushort RayCount,
    ushort HistoryLength, ushort ColorInterval, ushort RayHistoryHandle, ushort RayHistoryOffset,
    uint DrawCount, FarPointer16 System, FarPointer16 Module);
