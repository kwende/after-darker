using AfterDarker.Core.Win16;

namespace AfterDarker.Runtime;

/// <summary>Detached evidence of one guest-to-host import call and its completed return.</summary>
/// <param name="Phase">Lifecycle invocation in which the call occurred.</param>
/// <param name="Binding">Import identity, synthetic address and ABI metadata.</param>
/// <param name="Arguments">Decoded word values in source order.</param>
/// <param name="Returned">Handler result; void bindings do not write this value to AX.</param>
/// <param name="Before">Registers at the gateway, with CALL FAR's frame still on the stack.</param>
/// <param name="After">Registers after writing the result and simulating RETF plus argument cleanup.</param>
public sealed record Win16CallTrace(string Phase, Win16Imports.ImportEntry Binding,
    IReadOnlyList<ushort> Arguments, uint Returned,
    SegmentedGuest.CpuState Before, SegmentedGuest.CpuState After);
