using AfterDarker.Core.Ne;
using AfterDarker.Core.Win16;

namespace AfterDarker.Runtime;

/// <summary>Observed globals of the supported Fade Away binary; the original guest owns the fading algorithm.</summary>
/// <param name="Compatibility">Flag set by PREINITIALIZE after validating host compatibility fields.</param>
/// <param name="InstanceHandle">DLL instance saved by original startup code.</param>
/// <param name="Style">Guest's saved effect selection; this profile supports Radar (4).</param>
/// <param name="Finished">Guest flag set after the last sweep and final black fill.</param>
/// <param name="Step">Distance advanced around the edge: two pixels on the first pass, then one.</param>
/// <param name="Edge">Guest's current portion of the rectangular sweep, from 0 through 4.</param>
/// <param name="Endpoint">Moving endpoint of the line drawn from the surface center.</param>
/// <param name="System">Guest address returned by GlobalLock for the system record.</param>
/// <param name="Module">Guest address returned by GlobalLock for the module record.</param>
/// <remarks>Artifact-specific offsets and completion behavior are recorded in docs/research/fade-away-execution.md.</remarks>
public sealed record FadeAwayState(ushort Compatibility, ushort InstanceHandle, ushort Style, bool Finished,
    ushort Step, ushort Edge, Point16 Endpoint, FarPointer16 System, FarPointer16 Module);
