namespace AfterDarker.Core.Win16;

/// <summary>Implicit API inputs sampled at the gateway, kept separate from Pascal stack arguments.</summary>
/// <param name="DataSelector">The caller's DS, which selects the Win16 local heap.</param>
public readonly record struct Win16CallContext(ushort DataSelector);
