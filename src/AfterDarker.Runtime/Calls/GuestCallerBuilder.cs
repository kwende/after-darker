using AfterDarker.Core.Ne;

namespace AfterDarker.Runtime;

/// <summary>Builds the tiny x86 caller which pushes arguments, calls a DLL, and stores the returned AX.</summary>
/// <remarks>
/// This is deliberately a small, inspectable encoder, not a general assembler.
/// The guest performs the CALL FAR and result store itself. Build all callers before execution
/// so Unicorn never sees edited instructions in its decoded-code cache.
/// See docs/runtime-code-map.md and docs/tutorial-06-load-library.md.
/// </remarks>
internal sealed class GuestCallerBuilder
{
    private readonly List<byte> instructions = [];

    /// <summary>Write one caller and return the offset immediately after its final store.</summary>
    /// <param name="callerSegment">Code segment receiving the encoded instructions.</param>
    /// <param name="startOffset">First instruction's offset in that segment.</param>
    /// <param name="target">Resolved DLL startup or export address.</param>
    /// <param name="resultAddress">Writable guest location at which the caller records AX.</param>
    /// <param name="argumentWords">Pascal arguments in left-to-right push order.</param>
    public static ushort WriteCaller(byte[] callerSegment, ushort startOffset,
        FarPointer16 target, FarPointer16 resultAddress, params ushort[] argumentWords)
    {
        var builder = new GuestCallerBuilder();
        foreach (ushort argument in argumentWords)
        {
            builder.PushImmediateWord(argument);
        }
        builder.CallFar(target);
        builder.StoreReturnedAx(resultAddress);
        builder.instructions.CopyTo(callerSegment, startOffset);
        return checked((ushort)(startOffset + builder.instructions.Count));
    }

    /// <summary>PUSH imm16 decrements SP and writes one argument word to SS:SP.</summary>
    private void PushImmediateWord(ushort value)
    {
        instructions.Add(0x68);
        AppendWord(value);
    }

    /// <summary>CALL FAR imm16:16 saves CS:IP; the encoded operand stores offset before selector.</summary>
    private void CallFar(FarPointer16 target)
    {
        instructions.Add(0x9A);
        AppendWord(target.Offset);
        AppendWord(target.Selector);
    }

    /// <summary>Preserve AX while loading ES, then let the guest store the result in host-owned guest memory.</summary>
    private void StoreReturnedAx(FarPointer16 destination)
    {
        instructions.Add(0x50);                  // PUSH AX: save returned value on the guest stack.
        instructions.Add(0xB8);                  // MOV AX,imm16: segment registers cannot load immediates.
        AppendWord(destination.Selector);
        instructions.AddRange([0x8E, 0xC0]);     // MOV ES,AX: address the result storage.
        instructions.Add(0x58);                  // POP AX: recover the return value, balancing our PUSH.
        instructions.AddRange([0x26, 0xA3]);     // MOV ES:[offset],AX: ES override plus direct word store.
        AppendWord(destination.Offset);
    }

    /// <summary>Encode a 16-bit immediate in x86 little-endian byte order.</summary>
    private void AppendWord(ushort value)
    {
        instructions.Add((byte)value);
        instructions.Add((byte)(value >> 8));
    }
}
