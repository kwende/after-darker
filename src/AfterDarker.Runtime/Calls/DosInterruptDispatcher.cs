using AfterDarker.Core.Win16;
using UnicornEngine.Const;

namespace AfterDarker.Runtime;

/// <summary>Adapts the two supported DOS clock services to the CPU's INT 21h register interface.</summary>
/// <remarks>
/// Unlike the import gateway, this boundary has no far-call frame to pop. SegmentedGuest
/// verifies that Unicorn already advanced IP without pushing an interrupt frame.
/// See docs/research/dos-source-reference.md and docs/runtime-code-map.md.
/// </remarks>
internal sealed class DosInterruptDispatcher(SegmentedGuest guest, SessionTiming timing, TextWriter output)
{
    /// <summary>Read AH, obtain the explicit civil-time response, and update only AX/CX/DX.</summary>
    public void Dispatch(int interruptNumber)
    {
        if (interruptNumber != DosClock.InterruptNumber)
        {
            throw new NotSupportedException($"Unsupported INT {interruptNumber:X2}h during {guest.Phase}.");
        }

        ushort accumulator = (ushort)guest.Get(X86.UC_X86_REG_AX);
        byte serviceNumber = (byte)(accumulator >> 8);
        DosClock.Registers reply = DosClock.Respond(serviceNumber, accumulator, timing.CivilTime);
        guest.Set(X86.UC_X86_REG_AX, reply.Ax);
        guest.Set(X86.UC_X86_REG_CX, reply.Cx);
        guest.Set(X86.UC_X86_REG_DX, reply.Dx);

        if (output != TextWriter.Null)
        {
            output.WriteLine($"   {guest.Phase}: INT 21h AH={serviceNumber:X2} -> AX={reply.Ax:X4} " +
                $"CX={reply.Cx:X4} DX={reply.Dx:X4}; resume after INT (no RETF/IRET)");
        }
    }
}
