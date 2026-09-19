using AfterDarker.Core.Ne;

namespace AfterDarker.Runtime;

/// <summary>Observed globals in the supported Spiral Gyra binary; these are not host-computed animation state.</summary>
/// <param name="Compatibility">Flag set by PREINITIALIZE when host compatibility checks pass.</param>
/// <param name="InstanceHandle">Instance token saved by DLL startup.</param>
/// <param name="Width">Guest's saved drawing width.</param>
/// <param name="Height">Guest's saved drawing height.</param>
/// <param name="Tick">Most recently saved GetTickCount value.</param>
/// <param name="Seed">Original guest random-number state.</param>
/// <param name="Time">Time value converted by the guest C runtime.</param>
/// <param name="System">Guest pointer obtained by locking the host system record.</param>
/// <param name="Module">Guest pointer obtained by locking the host module record.</param>
/// <param name="BlackPen">Owned black pen used by the original module.</param>
/// <param name="DrawingPen">Owned color pen used by the original module.</param>
/// <remarks>See docs/research/spiral-gyra-execution.md for artifact identity and offsets.</remarks>
public sealed record SpiralGyraState(ushort Compatibility, ushort InstanceHandle, ushort Width, ushort Height,
    uint Tick, uint Seed, uint Time, FarPointer16 System, FarPointer16 Module, ushort BlackPen, ushort DrawingPen);
