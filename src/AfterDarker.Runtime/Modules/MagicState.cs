using AfterDarker.Core.Ne;

namespace AfterDarker.Runtime;

/// <summary>Globals read from the supported original Magic module, never host-generated animation state.</summary>
/// <param name="Compatibility">Guest PREINITIALIZE acceptance flag.</param>
/// <param name="InstanceHandle">Data selector saved by DLL startup.</param>
/// <param name="HistoryHandle">Movable local allocation owned until CLOSE, holding 152 ten-byte line records.</param>
/// <param name="HistoryOffset">Near offset obtained from LocalLock, relative to the module's DS.</param>
/// <param name="HistoryLength">Number of records used for the visible trail, selected by the guest's lookup.</param>
/// <param name="HistoryIndex">Circular-buffer slot to erase and replace on the next drawing update.</param>
/// <param name="LineDelay">Guest DRAWFRAME counter threshold; zero permits every call to advance the animation.</param>
/// <param name="ColorInterval">Guest color counter threshold; the color changes when the counter exceeds it.</param>
/// <param name="Mirror">Original combo choice: 0 none, 1 horizontal, 2 vertical, 3 both.</param>
/// <param name="MotionCount">Updates since the last velocity change; wraps after 701 updates.</param>
/// <param name="ColorIndex">Current index into the guest's own color table.</param>
/// <param name="System">Locked AD_SYSTEM guest address.</param>
/// <param name="Module">Locked AD_MODULE guest address.</param>
public sealed record MagicState(ushort Compatibility, ushort InstanceHandle,
    ushort HistoryHandle, ushort HistoryOffset, ushort HistoryLength, ushort HistoryIndex,
    ushort LineDelay, ushort ColorInterval, ushort Mirror, ushort MotionCount, ushort ColorIndex,
    FarPointer16 System, FarPointer16 Module);
