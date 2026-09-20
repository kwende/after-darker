using AfterDarker.Core.Ne;

namespace AfterDarker.Runtime;

/// <summary>Zot!'s original scheduling and temporary bolt-storage state at a completed lifecycle boundary.</summary>
/// <param name="Compatibility">Guest PREINITIALIZE acceptance flag.</param>
/// <param name="InstanceHandle">Data selector saved by DLL startup.</param>
/// <param name="ForkDivisor">Guest fork-choice divisor derived from the Forkiness control.</param>
/// <param name="Frequency">Original How Often control value.</param>
/// <param name="NextStrike">Guest DWORD deadline compared against GetCurrentTime.</param>
/// <param name="MainHandle">Fixed local allocation for main-bolt segments; zero between completed calls.</param>
/// <param name="ForkHandle">Fixed local allocation for fork segments; zero between completed calls.</param>
/// <param name="MainSegmentCount">Main-bolt segments generated during the last call; reset by an idle draw.</param>
/// <param name="ForkSegmentCount">Fork segments generated during the last call; reset by an idle draw.</param>
/// <param name="System">Guest address of AD_SYSTEM.</param>
/// <param name="Module">Guest address of AD_MODULE.</param>
public sealed record ZotState(ushort Compatibility, ushort InstanceHandle, ushort ForkDivisor, ushort Frequency,
    uint NextStrike, ushort MainHandle, ushort ForkHandle, ushort MainSegmentCount, ushort ForkSegmentCount,
    FarPointer16 System, FarPointer16 Module);
