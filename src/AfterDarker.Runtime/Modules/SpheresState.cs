using AfterDarker.Core.Ne;

namespace AfterDarker.Runtime;

/// <summary>Detached observations of Spheres' globals; the original guest owns all animation decisions.</summary>
/// <param name="Compatibility">PREINITIALIZE's acceptance flag.</param>
/// <param name="InstanceHandle">Instance retained by DLL startup.</param>
/// <param name="DeviceContext">HDC retained by the module dispatcher.</param>
/// <param name="RandomSeed">Current state of the guest's pseudorandom generator.</param>
/// <param name="System">Saved AD_SYSTEM far pointer.</param>
/// <param name="Module">Saved AD_MODULE far pointer.</param>
/// <param name="SphereNumber">One-based sphere number since the last screen clear.</param>
/// <param name="NextBand">Next shading band to draw, from zero through 33.</param>
/// <param name="Radius">Original radius of the current sphere, before its shading bands shrink.</param>
/// <param name="CenterX">Current sphere's horizontal center.</param>
/// <param name="CenterY">Current sphere's vertical center.</param>
public sealed record SpheresState(ushort Compatibility, ushort InstanceHandle, ushort DeviceContext,
    uint RandomSeed, FarPointer16 System, FarPointer16 Module, ushort SphereNumber,
    ushort NextBand, ushort Radius, ushort CenterX, ushort CenterY);
