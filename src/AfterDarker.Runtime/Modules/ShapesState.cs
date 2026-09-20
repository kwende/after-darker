using AfterDarker.Core.Ne;

namespace AfterDarker.Runtime;

/// <summary>A detached observation of Shapes' original globals; shape generation stays in the guest.</summary>
/// <param name="Compatibility">PREINITIALIZE's acceptance flag.</param>
/// <param name="InstanceHandle">Instance saved by the original DLL startup.</param>
/// <param name="DeviceContext">Guest HDC saved by the module dispatcher.</param>
/// <param name="RandomSeed">The original 32-bit pseudorandom generator's current state.</param>
/// <param name="System">Saved AD_SYSTEM far pointer.</param>
/// <param name="Module">Saved AD_MODULE far pointer.</param>
public sealed record ShapesState(ushort Compatibility, ushort InstanceHandle, ushort DeviceContext,
    uint RandomSeed, FarPointer16 System, FarPointer16 Module);
