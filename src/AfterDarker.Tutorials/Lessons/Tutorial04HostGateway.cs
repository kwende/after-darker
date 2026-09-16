using System.Buffers.Binary;
using UnicornEngine;
using UnicornEngine.Const;

namespace AfterDarker.Tutorials.Lessons;

/// <summary>
/// Guest CALL FAR -> stop at a synthetic gateway -> C# service -> guest store.
/// Descriptor setup repeats tutorial 03 so this lesson can be read on its own.
/// </summary>
public sealed class Tutorial04HostGateway : ITutorial
{
    public string Id => "04";
    public string Title => "A far call into a C# host gateway";

    public void Run()
    {
        const long pageSize = 0x1000;
        const uint callerBase = 0x10000, gatewayBase = 0x20000;
        const uint dataBase = 0x30000, stackBase = 0x40000, gdtBase = 0x50000;
        const ushort callerSelector = 0x0008, gatewaySelector = 0x0010;
        const ushort dataSelector = 0x0018, stackSelector = 0x0020;
        const ushort segmentLimit = 0x0FFF, initialSp = 0x1000, gatewayOffset = 0x0200;
        const ushort marker = 0xCCCC;

        // Far Pascal convention: push arguments LEFT TO RIGHT; the callee cleans up.
        // There is no guest ADD or RETF here. C# supplies AX and simulates RETF 4.
        byte[] callerCode =
        [
            0x68, 0x07, 0x00,             // 0008:0000  PUSH word 7   (left)
            0x68, 0x05, 0x00,             // 0008:0003  PUSH word 5   (right)
            0x9A, 0x00, 0x02, 0x10, 0x00, // 0008:0006  CALL FAR 0010:0200
            0xA3, 0x20, 0x00,             // 0008:000B  MOV [DS:0020], AX

            // Repeat on the SAME engine, this time with a signed argument/result.
            0x68, 0xF9, 0xFF,             // 0008:000E  PUSH word -7  (left)
            0x68, 0x05, 0x00,             // 0008:0011  PUSH word 5   (right)
            0x9A, 0x00, 0x02, 0x10, 0x00, // 0008:0014  CALL FAR 0010:0200
            0xA3, 0x22, 0x00,             // 0008:0019  MOV [DS:0022], AX
            // Completion: 0008:001C.
        ];

        // This is a tiny synthetic service table, not an NE import resolver.
        var bindings = new Dictionary<GuestAddress, HostBinding>
        {
            [new(gatewaySelector, gatewayOffset)] = new("Tutorial!HostAdd", 4, HostAdd),
        };
        (short Left, short Right, short Result, ushort ReturnIp, ushort ResultOffset)[] expectedCalls =
        [
            (7, 5, 12, 0x000B, 0x0020),
            (-7, 5, -2, 0x0019, 0x0022),
        ];

        byte[] gdt = new byte[5 * 8];
        Create16BitDescriptor(callerBase, segmentLimit, true).CopyTo(gdt, callerSelector);
        Create16BitDescriptor(gatewayBase, segmentLimit, true).CopyTo(gdt, gatewaySelector);
        Create16BitDescriptor(dataBase, segmentLimit, false).CopyTo(gdt, dataSelector);
        Create16BitDescriptor(stackBase, segmentLimit, false).CopyTo(gdt, stackSelector);

        // As in tutorial 03: protected-mode API setup, with 16-bit descriptors.
        using var emulator = new Unicorn(Common.UC_ARCH_X86, Common.UC_MODE_32);
        try
        {
            emulator.MemMap(callerBase, pageSize, Common.UC_PROT_ALL);
            emulator.MemMap(gatewayBase, pageSize, Common.UC_PROT_ALL);
            emulator.MemMap(dataBase, pageSize, Common.UC_PROT_READ | Common.UC_PROT_WRITE);
            emulator.MemMap(stackBase, pageSize, Common.UC_PROT_READ | Common.UC_PROT_WRITE);
            emulator.MemMap(gdtBase, pageSize, Common.UC_PROT_READ | Common.UC_PROT_WRITE);
            emulator.MemWrite(callerBase, callerCode);
            emulator.MemWrite(gdtBase, gdt);
            emulator.MemWrite(dataBase + 0x20, [0xCC, 0xCC, 0xCC, 0xCC]);

            // A guard instruction, NOT the implementation of HostAdd. Its changed AX
            // would expose a hook that failed to stop BEFORE the gateway executed.
            emulator.MemWrite(gatewayBase + gatewayOffset, [0xB8, 0xAD, 0xDE, 0xF4]); // MOV AX, DEAD; HLT
            WriteDescriptorTableRegister(emulator, gdtBase, (uint)gdt.Length - 1);
            emulator.RegWrite(X86.UC_X86_REG_CR0, emulator.RegRead(X86.UC_X86_REG_CR0) | 1);
            emulator.RegWrite(X86.UC_X86_REG_CS, callerSelector);
            emulator.RegWrite(X86.UC_X86_REG_DS, dataSelector);
            emulator.RegWrite(X86.UC_X86_REG_SS, stackSelector);
            emulator.RegWrite(X86.UC_X86_REG_ESP, initialSp);
            emulator.RegWrite(X86.UC_X86_REG_EAX, 0);

            // The hook does only two things: record the exit and request a stop.
            // No argument decoding, service execution, or return simulation in here.
            long? trappedLinearAddress = null;
            int trapCount = 0;
            CodeHook gatewayHook = (engine, address, size, userData) =>
            {
                trappedLinearAddress = address;
                trapCount++;
                engine.EmuStop();
            };
            // Watch the whole reserved segment so an unknown entry gets a symbolic error.
            emulator.AddCodeHook(gatewayHook, gatewayBase, gatewayBase + segmentLimit);

            long resumeIp = 0;
            long previousAx = 0;
            for (int callIndex = 0; callIndex < expectedCalls.Length; callIndex++)
            {
                var expected = expectedCalls[callIndex];
                trappedLinearAddress = null;
                // Start takes an IP offset; stop takes a linear address (tutorial 03).
                emulator.EmuStart(resumeIp, callerBase + callerCode.Length, timeout: 1_000_000, count: 32);

                // EmuStart has returned: we are OUTSIDE the native hook now.
                if (trappedLinearAddress is not long reportedAddress || trapCount != callIndex + 1)
                    throw new InvalidOperationException($"Expected gateway stop #{callIndex + 1}; got {trapCount} stops.");

                var entry = new GuestAddress(
                    checked((ushort)emulator.RegRead(X86.UC_X86_REG_CS)),
                    checked((ushort)emulator.RegRead(X86.UC_X86_REG_EIP)));
                if (!bindings.TryGetValue(entry, out HostBinding? binding))
                    throw new InvalidOperationException($"Unsupported host gateway {entry}; no service binding exists.");
                if (reportedAddress != gatewayBase + entry.Offset)
                    throw new InvalidOperationException($"Gateway {entry}: expected a linear hook address; got 0x{reportedAddress:X}.");
                if (emulator.RegRead(X86.UC_X86_REG_AX) != previousAx)
                    throw new InvalidOperationException("AX changed before host dispatch; the gateway guard may have executed.");

                // A near pointer into our known stack segment is checked before MemRead.
                long sp = emulator.RegRead(X86.UC_X86_REG_SP);
                int frameSize = 4 + binding.ArgumentByteCount; // IP + CS + arguments
                if (emulator.RegRead(X86.UC_X86_REG_SS) != stackSelector ||
                    sp < 0 || sp + frameSize > pageSize || sp != initialSp - frameSize)
                    throw new InvalidOperationException($"{binding.Name}: invalid guest stack frame at SP=0x{sp:X4}.");

                byte[] frame = new byte[frameSize];
                emulator.MemRead(stackBase + sp, frame);
                // At the gateway: [SP+0]=IP, [+2]=CS, [+4]=right, [+6]=left.
                ushort returnIp = BinaryPrimitives.ReadUInt16LittleEndian(frame.AsSpan(0, 2));
                ushort returnCs = BinaryPrimitives.ReadUInt16LittleEndian(frame.AsSpan(2, 2));
                short right = BinaryPrimitives.ReadInt16LittleEndian(frame.AsSpan(4, 2));
                short left = BinaryPrimitives.ReadInt16LittleEndian(frame.AsSpan(6, 2));
                if (returnCs != callerSelector || returnIp != expected.ReturnIp ||
                    left != expected.Left || right != expected.Right)
                    throw new InvalidOperationException($"{binding.Name}: unexpected return {returnCs:X4}:{returnIp:X4} or arguments ({left}, {right}).");

                // This call's result must still be untouched when control reaches C#.
                if (ReadWord(emulator, dataBase + expected.ResultOffset) != marker)
                    throw new InvalidOperationException("The guest result changed before its host call returned.");
                if (callIndex > 0 && ReadWord(emulator, dataBase + expectedCalls[callIndex - 1].ResultOffset) !=
                    unchecked((ushort)expectedCalls[callIndex - 1].Result))
                    throw new InvalidOperationException("The previous guest store did not complete before the next call.");

                Console.WriteLine($"Trap {trapCount}: {entry}, hook linear address 0x{reportedAddress:X5}, SP=0x{sp:X4}");
                short result = binding.Handler(left, right); // The addition happens in C#.
                Console.WriteLine($"Host: {binding.Name}({left}, {right}) = {result}; return {returnCs:X4}:{returnIp:X4}");

                // Simulate same-privilege RETF 4: restore CS:IP, pop four return bytes,
                // and remove four argument bytes. The guest performs no RETF itself.
                emulator.RegWrite(X86.UC_X86_REG_AX, unchecked((ushort)result));
                emulator.RegWrite(X86.UC_X86_REG_CS, returnCs);
                emulator.RegWrite(X86.UC_X86_REG_EIP, returnIp);
                emulator.RegWrite(X86.UC_X86_REG_SP, sp + frameSize);
                if (ReadWord(emulator, dataBase + expected.ResultOffset) != marker)
                    throw new InvalidOperationException("Host dispatch must not write the guest result location.");

                resumeIp = returnIp;
                previousAx = unchecked((ushort)result);
            }

            // Resume after the second host return so the GUEST executes its final MOV.
            trappedLinearAddress = null;
            emulator.EmuStart(resumeIp, callerBase + callerCode.Length, timeout: 1_000_000, count: 32);
            if (trappedLinearAddress is not null || trapCount != expectedCalls.Length)
                throw new InvalidOperationException("Unexpected extra gateway entry while completing the guest.");

            foreach (var expected in expectedCalls)
            {
                ushort actualBits = ReadWord(emulator, dataBase + expected.ResultOffset);
                Console.WriteLine($"Guest stored {unchecked((short)actualBits)} (0x{actualBits:X4}) at {dataSelector:X4}:{expected.ResultOffset:X4}");
                if (actualBits != unchecked((ushort)expected.Result))
                    throw new InvalidOperationException($"Expected guest result {expected.Result} at offset 0x{expected.ResultOffset:X4}.");
            }

            long finalSp = emulator.RegRead(X86.UC_X86_REG_SP);
            long finalCs = emulator.RegRead(X86.UC_X86_REG_CS);
            long finalIp = emulator.RegRead(X86.UC_X86_REG_EIP);
            Console.WriteLine($"Final CS:IP = {finalCs:X4}:{finalIp:X4}; SP = 0x{finalSp:X4}; host calls = {trapCount}");
            if (finalSp != initialSp || finalCs != callerSelector || finalIp != callerCode.Length ||
                emulator.RegRead(X86.UC_X86_REG_AX) != unchecked((ushort)expectedCalls[^1].Result) ||
                emulator.RegRead(X86.UC_X86_REG_DS) != dataSelector ||
                emulator.RegRead(X86.UC_X86_REG_SS) != stackSelector ||
                (emulator.RegRead(X86.UC_X86_REG_CR0) & 1) == 0)
                throw new InvalidOperationException("Guest completion registers or restored stack do not match the expected state.");

            Console.WriteLine("PASS: two host calls returned to x86; the guest stored both results and restored its stack.");
        }
        finally
        {
            // Closing the engine also removes its hooks; the binding roots the delegate.
            emulator.Close();
        }
    }

    // A typed synthetic API: signed 16-bit arguments/result, wrapping like 16-bit ADD.
    private static short HostAdd(short left, short right) => unchecked((short)(left + right));

    private readonly record struct GuestAddress(ushort Selector, ushort Offset)
    {
        public override string ToString() => $"{Selector:X4}:{Offset:X4}";
    }

    // All bindings in this lesson use far Pascal, two signed words, and AX return.
    private sealed record HostBinding(string Name, int ArgumentByteCount, Func<short, short, short> Handler);

    private static ushort ReadWord(Unicorn emulator, long linearAddress)
    {
        byte[] bytes = new byte[2];
        emulator.MemRead(linearAddress, bytes);
        return BinaryPrimitives.ReadUInt16LittleEndian(bytes);
    }

    // Identical descriptor encoding to tutorial 03: byte limits, D/B=0, privilege 0.
    private static byte[] Create16BitDescriptor(uint baseAddress, ushort inclusiveLimit, bool executable)
    {
        byte[] descriptor = new byte[8];
        BinaryPrimitives.WriteUInt16LittleEndian(descriptor.AsSpan(0, 2), inclusiveLimit);
        BinaryPrimitives.WriteUInt16LittleEndian(descriptor.AsSpan(2, 2), (ushort)baseAddress);
        descriptor[4] = (byte)(baseAddress >> 16);
        descriptor[5] = executable ? (byte)0x9B : (byte)0x93;
        descriptor[6] = 0;
        descriptor[7] = (byte)(baseAddress >> 24);
        return descriptor;
    }

    private static void WriteDescriptorTableRegister(Unicorn emulator, uint baseAddress, uint inclusiveLimit)
    {
        // Native x64 uc_x86_mmr API layout (not a guest segment descriptor).
        byte[] gdtr = new byte[24];
        BinaryPrimitives.WriteUInt64LittleEndian(gdtr.AsSpan(8, 8), baseAddress);
        BinaryPrimitives.WriteUInt32LittleEndian(gdtr.AsSpan(16, 4), inclusiveLimit);
        emulator.RegWrite(X86.UC_X86_REG_GDTR, gdtr);
    }
}
