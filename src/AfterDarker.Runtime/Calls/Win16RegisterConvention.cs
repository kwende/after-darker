using AfterDarker.Core.Ne;
using AfterDarker.Core.Win16;
using UnicornEngine.Const;

namespace AfterDarker.Runtime;

/// <summary>Explains and applies register assignments required by our Win16 calling conventions.</summary>
/// <remarks>
/// Startup inputs and API return values are different contracts. Neither belongs in a GDI
/// implementation. Stack cleanup lives in <see cref="Win16Stack"/>; CPU execution lives in
/// <see cref="SegmentedGuest"/>. See docs/runtime-code-map.md and docs/win16-implementations.md.
/// </remarks>
public static class Win16RegisterConvention
{
    /// <summary>Prepare the caller's stack and the compiler runtime's DLL startup inputs.</summary>
    /// <remarks>
    /// This is host preparation before executing the NE startup routine, not an emulated
    /// PUSH BP/MOV BP,SP function prologue. AX is deliberately not initialized: the DLL
    /// writes its success result there. CS:IP is selected later by RunUntil.
    /// </remarks>
    public static void SetUpPrologRegisters(SegmentedGuest guest, LibraryStartupContext context)
    {
        // DS addresses the DLL's globals. Our narrow loader also uses that selector
        // as the instance handle which the startup runtime receives in DI.
        guest.Set(X86.UC_X86_REG_DS, context.DataSelector);
        guest.Set(X86.UC_X86_REG_DI, context.DataSelector);

        // CX describes the already reserved heap tail. ES:SI is a null command line.
        guest.Set(X86.UC_X86_REG_CX, context.HeapBytes);
        guest.Set(X86.UC_X86_REG_ES, 0);
        guest.Set(X86.UC_X86_REG_SI, 0);

        // PUSH decrements SP before writing. BP=0 terminates the initial frame chain.
        guest.Set(X86.UC_X86_REG_SS, context.StackSelector);
        guest.Set(X86.UC_X86_REG_SP, context.EmptyStackPointer);
        guest.Set(X86.UC_X86_REG_BP, 0);
    }

    /// <summary>Call MODULE with the host's data selector, exercising the DLL's export prologue.</summary>
    /// <remarks>The guest export must establish its own DS and restore this caller DS on return.</remarks>
    public static void SetUpModuleCallerDataSegment(SegmentedGuest guest, ushort callerDataSelector)
    {
        guest.Set(X86.UC_X86_REG_DS, callerDataSelector);
    }

    /// <summary>Place a mocked API's result in the registers specified by its import contract.</summary>
    /// <remarks>
    /// A word uses AX; a DWORD or far pointer uses DX:AX (high:low).
    /// Void APIs preserve both. GlobalLock additionally supplies its selector in CX.
    /// Register writes communicate results; they do not pop the return address.
    /// </remarks>
    public static void WriteReturnRegisters(
        SegmentedGuest guest, Win16ReturnLayout returnLayout, Win16Imports.Reply reply)
    {
        if (returnLayout != Win16ReturnLayout.Void)
        {
            ushort lowWord = unchecked((ushort)reply.Value);
            guest.Set(X86.UC_X86_REG_AX, lowWord);
        }

        if (returnLayout == Win16ReturnLayout.DwordInDxAx)
        {
            ushort highWord = (ushort)(reply.Value >> 16);
            guest.Set(X86.UC_X86_REG_DX, highWord);
        }

        if (reply.Cx is ushort additionalSelector)
        {
            guest.Set(X86.UC_X86_REG_CX, additionalSelector);
        }
    }
}
