using AfterDarker.Core.Ne;

namespace AfterDarker.Runtime;

/// <summary>One original drop record decoded from Hard Rain's static data-segment array.</summary>
/// <param name="Color">Guest-selected RGB color in COLORREF order.</param>
/// <param name="CenterX">Horizontal center chosen by the guest's random-number routine.</param>
/// <param name="CenterY">Vertical center chosen by the guest's random-number routine.</param>
/// <param name="Radius">Current ring radius; the original code increases it by three per visit.</param>
/// <param name="MaximumRadius">Guest-selected threshold which triggers erasure and regeneration.</param>
public sealed record HardRainDrop(uint Color, short CenterX, short CenterY, uint Radius, uint MaximumRadius);

/// <summary>A detached observation of Hard Rain's original globals, not host-generated animation state.</summary>
/// <param name="Compatibility">PREINITIALIZE acceptance flag.</param>
/// <param name="InstanceHandle">Instance saved by original DLL initialization.</param>
/// <param name="DropCount">Number of active records copied from control 1.</param>
/// <param name="DropSize">Random-radius range copied from control 2.</param>
/// <param name="NextDrop">Index which the next DRAWFRAME will update.</param>
/// <param name="Drops">Owned snapshot of the guest's active drop records.</param>
/// <param name="System">AD_SYSTEM pointer obtained by GlobalLock.</param>
/// <param name="Module">AD_MODULE pointer obtained by GlobalLock.</param>
public sealed record HardRainState(ushort Compatibility, ushort InstanceHandle, ushort DropCount,
    ushort DropSize, ushort NextDrop, IReadOnlyList<HardRainDrop> Drops, FarPointer16 System, FarPointer16 Module);
