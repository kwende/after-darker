using AfterDarker.Core.Ne;

namespace AfterDarker.Core.Win16;

/// <summary>The loader's initial local-heap tail after the DLL's static data, in guest memory.</summary>
/// <param name="Start">First reserved byte, including the selector which owns this local heap.</param>
/// <param name="Length">Initial byte count from the NE header; distinct from any backing capacity for growth.</param>
public sealed record LocalHeapReservation(FarPointer16 Start, int Length);
