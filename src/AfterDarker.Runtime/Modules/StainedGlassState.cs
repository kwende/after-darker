using AfterDarker.Core.Ne;

namespace AfterDarker.Runtime;

/// <summary>Detached observations of the analyzed Stained Glass globals; these are guest-owned values.</summary>
/// <param name="Compatibility">PREINITIALIZE's recorded host compatibility result.</param>
/// <param name="InstanceHandle">Data-selector token saved by DLL startup.</param>
/// <param name="DeviceContext">HDC supplied with the current module invocation.</param>
/// <param name="System">Last locked AD_SYSTEM address.</param>
/// <param name="Module">Last locked AD_MODULE address.</param>
/// <param name="ColorDisplay">Whether original code recognizes a color-capable destination.</param>
/// <param name="Complexity">Cached first control, copied by the drawing dispatcher.</param>
/// <param name="Duplication">Cached second control.</param>
/// <param name="Color">Cached third control.</param>
public sealed record StainedGlassState(ushort Compatibility, ushort InstanceHandle, ushort DeviceContext,
    FarPointer16 System, FarPointer16 Module, ushort ColorDisplay, ushort Complexity, ushort Duplication, ushort Color);
