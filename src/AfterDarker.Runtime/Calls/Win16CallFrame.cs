using AfterDarker.Core.Ne;

namespace AfterDarker.Runtime;

/// <summary>A checked snapshot of the guest stack at an intercepted far Pascal call.</summary>
/// <param name="ReturnAddress">CS:IP pushed by the actual guest CALL FAR instruction.</param>
/// <param name="Arguments">Word values in source order, reversing their order in stack memory.</param>
/// <param name="StackPointerAfterReturn">SP after removing saved IP, saved CS, and all argument words.</param>
/// <remarks>Far pointers and DWORDs still occupy two words; Win16Imports interprets those pairs.</remarks>
public sealed record Win16CallFrame(
    FarPointer16 ReturnAddress,
    IReadOnlyList<ushort> Arguments,
    ushort StackPointerAfterReturn);
