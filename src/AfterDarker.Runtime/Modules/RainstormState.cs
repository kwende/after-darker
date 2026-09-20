using AfterDarker.Core.Ne;
using AfterDarker.Core.Win16;

namespace AfterDarker.Runtime;

/// <summary>Globals written by the supported original Rainstorm binary, decoded for proof checks.</summary>
/// <param name="Compatibility">PREINITIALIZE's saved compatibility flag.</param>
/// <param name="InstanceHandle">Instance token saved by original DLL startup.</param>
/// <param name="DropBounds">Guest's recycling rectangle, extending beyond the drawing surface.</param>
/// <param name="DropCount">Number of raindrops processed by each DRAWFRAME.</param>
/// <param name="Strength">Guest's saved strength control value.</param>
/// <param name="Wind">Signed wind factor chosen during initialization.</param>
/// <param name="LightningCountdown">Guest frames remaining before the next paired rectangle inversions.</param>
/// <param name="System">Guest pointer obtained by locking AD_SYSTEM.</param>
/// <param name="Module">Guest pointer obtained by locking AD_MODULE.</param>
/// <remarks>Offsets and the intermediate lightning presentation policy are recorded in docs/research/rainstorm-execution.md.</remarks>
public sealed record RainstormState(ushort Compatibility, ushort InstanceHandle, Rectangle16 DropBounds,
    ushort DropCount, ushort Strength, short Wind, short LightningCountdown,
    FarPointer16 System, FarPointer16 Module);
