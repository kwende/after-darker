namespace AfterDarker.Core.Win16;

/// <summary>A Win16 POINT value: two signed 16-bit coordinates, not a guest pointer.</summary>
/// <param name="X">Horizontal coordinate in guest device units.</param>
/// <param name="Y">Vertical coordinate in guest device units.</param>
public readonly record struct Point16(short X, short Y);
