namespace AfterDarker.Runtime;

/// <summary>Guest values needed before calling a Win16 DLL's NE startup entry point.</summary>
/// <param name="DataSelector">Selector of the DLL's automatic data segment; also our instance token.</param>
/// <param name="HeapBytes">Local heap reservation requested by the NE header, passed in CX.</param>
/// <param name="StackSelector">Selector of the caller-owned writable stack segment.</param>
/// <param name="EmptyStackPointer">Offset immediately above the empty descending stack.</param>
/// <remarks>All addresses are guest values. See docs/runtime-code-map.md for the register contract.</remarks>
public sealed record LibraryStartupContext(
    ushort DataSelector,
    ushort HeapBytes,
    ushort StackSelector,
    ushort EmptyStackPointer);
