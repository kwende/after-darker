using AfterDarker.Core.Ne;
using AfterDarker.Core.Win16;
using UnicornEngine.Const;

namespace AfterDarker.Runtime;

/// <summary>Reads imported-call frames and performs the stack/control-flow part of a far Pascal return.</summary>
/// <remarks>
/// This models the supported same-privilege, non-wrapping stack, not a general x86 stack.
/// CALL FAR has already pushed IP and CS when the gateway is reached. We simulate RETF n
/// because the mocked Windows function has no guest epilogue to execute.
/// See docs/runtime-code-map.md#following-one-mocked-call for the byte layout.
/// </remarks>
/// <param name="guest">Machine whose memory and registers hold the active call.</param>
/// <param name="stackSelector">Expected SS; other stacks are outside this host's contract.</param>
/// <param name="emptyStackPointer">Upper bound of the caller's descending stack, not the current SP.</param>
public sealed class Win16Stack(SegmentedGuest guest, ushort stackSelector, ushort emptyStackPointer)
{
    /// <summary>Validate SS:SP, decode arguments, and check the saved return address before running a service.</summary>
    /// <param name="argumentBytes">Byte count from the import signature, excluding the four-byte far return.</param>
    /// <param name="functionName">Symbol included in diagnostics when the frame is invalid.</param>
    public Win16CallFrame ReadCallFrame(int argumentBytes, string functionName)
    {
        SegmentedGuest.CpuState registers = guest.Snapshot();
        const int maximumArgumentBytes = ushort.MaxValue - FarPascalWordFrame.ReturnAddressBytes;
        if (argumentBytes < 0 || argumentBytes > maximumArgumentBytes || argumentBytes % sizeof(ushort) != 0)
        {
            throw new InvalidOperationException($"Invalid far Pascal signature for {functionName}.");
        }

        int frameBytes = FarPascalWordFrame.ReturnAddressBytes + argumentBytes;
        if (registers.Ss != stackSelector || registers.Sp + frameBytes > emptyStackPointer)
        {
            throw new InvalidOperationException($"Invalid far Pascal frame for {functionName}.");
        }

        var stackAddress = new FarPointer16(stackSelector, registers.Sp);
        var decodedFrame = new FarPascalWordFrame(guest.Read(stackAddress, frameBytes));
        ushort[] arguments = new ushort[decodedFrame.ArgumentCount];
        for (int argumentIndex = 0; argumentIndex < arguments.Length; argumentIndex++)
        {
            arguments[argumentIndex] = unchecked((ushort)decodedFrame.ReadArgument(argumentIndex));
        }

        var returnAddress = new FarPointer16(decodedFrame.ReturnCs, decodedFrame.ReturnIp);
        guest.RequireCode(returnAddress, 1);
        return new Win16CallFrame(returnAddress, Array.AsReadOnly(arguments),
            decodedFrame.StackPointerAfterReturn(registers.Sp));
    }

    /// <summary>Simulate RETF plus Pascal callee cleanup; result registers are handled separately.</summary>
    /// <remarks>
    /// Moving SP removes the frame logically; it does not erase stack bytes.
    /// SS stays unchanged. EIP is the engine's offset register and receives the saved 16-bit IP.
    /// </remarks>
    public void ReturnToCaller(Win16CallFrame frame)
    {
        guest.Set(X86.UC_X86_REG_CS, frame.ReturnAddress.Selector);
        guest.Set(X86.UC_X86_REG_EIP, frame.ReturnAddress.Offset);
        guest.Set(X86.UC_X86_REG_SP, frame.StackPointerAfterReturn);
    }
}
