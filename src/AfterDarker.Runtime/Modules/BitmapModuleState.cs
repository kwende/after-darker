using AfterDarker.Core.Ne;

namespace AfterDarker.Runtime;

/// <summary>Detached observations of the shared host-facing globals in the bitmap module family.</summary>
/// <param name="Compatibility">PREINITIALIZE's guest-written compatibility flag.</param>
/// <param name="InstanceHandle">DLL instance saved by original startup.</param>
/// <param name="DeviceContext">HDC passed to the original MODULE dispatcher.</param>
/// <param name="System">Guest's last locked AD_SYSTEM pointer.</param>
/// <param name="Module">Guest's last locked AD_MODULE pointer.</param>
public sealed record BitmapModuleState(ushort Compatibility, ushort InstanceHandle, ushort DeviceContext,
    FarPointer16 System, FarPointer16 Module);
