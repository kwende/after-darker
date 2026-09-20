using AfterDarker.Core.Ne;

namespace AfterDarker.Runtime;

/// <summary>Detached observations of String Theory's original globals, not host-generated animation state.</summary>
/// <param name="Compatibility">Guest PREINITIALIZE acceptance flag.</param>
/// <param name="InstanceHandle">Data selector saved by DLL startup.</param>
/// <param name="GroupCount">Independent string groups selected by the guest.</param>
/// <param name="HistoryHandle">One movable allocation holding every group's line history.</param>
/// <param name="HistoryOffset">Last locked near offset, relative to the module's DS.</param>
/// <param name="HistoryLength">Active history slots per group, out of 152 allocated per group.</param>
/// <param name="HistoryIndex">Next circular slot to erase and replace in each group.</param>
/// <param name="ColorInterval">Color counter threshold; changes occur when the counter exceeds it.</param>
/// <param name="MotionCount">Original motion counter, wrapping after 701 updates.</param>
/// <param name="GroupColors">Read-only copy of the groups' current color-table indices.</param>
/// <param name="System">Guest address of AD_SYSTEM.</param>
/// <param name="Module">Guest address of AD_MODULE.</param>
public sealed record StringTheoryState(ushort Compatibility, ushort InstanceHandle, ushort GroupCount,
    ushort HistoryHandle, ushort HistoryOffset, ushort HistoryLength, ushort HistoryIndex,
    ushort ColorInterval, ushort MotionCount, IReadOnlyList<ushort> GroupColors, FarPointer16 System, FarPointer16 Module);
