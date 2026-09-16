using System.Buffers.Binary;
using UnicornEngine;
using UnicornEngine.Const;

namespace AfterDarker.Tutorials.Lessons;

/// <summary>
/// A 16-bit protected-mode CALL FAR / RETF entirely inside the guest.
/// The caller stores the returned AX in guest data memory for C# to verify.
/// </summary>
public sealed class Tutorial03FarCall : ITutorial
{
    public string Id => "03";
    public string Title => "Protected-mode CALL FAR and RETF";

    public void Run()
    {
        const long pageSize = 0x1000;
        const uint callerBase = 0x10000;
        const uint calleeBase = 0x20000;
        const uint dataBase = 0x30000;
        const uint stackBase = 0x40000;
        const uint descriptorTableBase = 0x50000;

        // Selector = descriptor-table index * 8 (GDT entries, privilege level 0).
        const ushort callerSelector = 0x0008;
        const ushort calleeSelector = 0x0010;
        const ushort dataSelector = 0x0018;
        const ushort stackSelector = 0x0020;
        const ushort segmentLimit = 0x0FFF; // Inclusive, byte-granularity limit.
        const ushort calleeOffset = 0x0200;
        const ushort resultOffset = 0x0020;
        const ushort returnOffset = 0x0008;
        const ushort initialStackPointer = 0x1000;
        const ushort expectedCalleeStackPointer = initialStackPointer - 4;

        byte[] callerCode =
        [
            0xB8, 0x07, 0x00,             // 0008:0000  MOV AX, 7
            0x9A, 0x00, 0x02, 0x10, 0x00, // 0008:0003  CALL FAR 0010:0200
            0xA3, 0x20, 0x00,             // 0008:0008  MOV [DS:0020], AX
            // 0008:000B is the completion address; only the guest writes the result.
        ];
        byte[] calleeCode =
        [
            0x8C, 0xCB,       // 0010:0200  MOV BX, CS -- capture the callee selector
            0x89, 0xE2,       // 0010:0202  MOV DX, SP -- capture the in-call stack pointer
            0x05, 0x05, 0x00, // 0010:0204  ADD AX, 5
            0xCB,             // 0010:0207  RETF       -- pop IP, then CS
        ];

        // Entry zero is the required null descriptor. Each following entry is 8 bytes.
        byte[] descriptorTable = new byte[5 * 8];
        Create16BitDescriptor(callerBase, segmentLimit, executable: true).CopyTo(descriptorTable, callerSelector);
        Create16BitDescriptor(calleeBase, segmentLimit, executable: true).CopyTo(descriptorTable, calleeSelector);
        Create16BitDescriptor(dataBase, segmentLimit, executable: false).CopyTo(descriptorTable, dataSelector);
        Create16BitDescriptor(stackBase, segmentLimit, executable: false).CopyTo(descriptorTable, stackSelector);

        // Unicorn 2.1.3's UC_MODE_16 register/start APIs assume real-mode bases.
        // UC_MODE_32 initializes protected mode. Our descriptors' D/B=0 bits then
        // select 16-bit instructions and SP. No 32-bit guest instructions are used.
        using var emulator = new Unicorn(Common.UC_ARCH_X86, Common.UC_MODE_32);
        try
        {
            emulator.MemMap(callerBase, pageSize, Common.UC_PROT_ALL);
            emulator.MemMap(calleeBase, pageSize, Common.UC_PROT_ALL);
            emulator.MemMap(dataBase, pageSize, Common.UC_PROT_READ | Common.UC_PROT_WRITE);
            emulator.MemMap(stackBase, pageSize, Common.UC_PROT_READ | Common.UC_PROT_WRITE);
            emulator.MemMap(descriptorTableBase, pageSize, Common.UC_PROT_READ | Common.UC_PROT_WRITE);
            emulator.MemWrite(callerBase, callerCode);
            emulator.MemWrite(calleeBase + calleeOffset, calleeCode);
            emulator.MemWrite(descriptorTableBase, descriptorTable);
            emulator.MemWrite(dataBase + resultOffset, [0xCC, 0xCC]);
            emulator.MemWrite(stackBase + expectedCalleeStackPointer, [0xCC, 0xCC, 0xCC, 0xCC]);

            // GDTR tells the CPU where to look up selectors. Install it BEFORE CS/DS/SS.
            WriteDescriptorTableRegister(emulator, descriptorTableBase, (uint)descriptorTable.Length - 1);
            emulator.RegWrite(X86.UC_X86_REG_CR0, emulator.RegRead(X86.UC_X86_REG_CR0) | 1); // PE=1
            emulator.RegWrite(X86.UC_X86_REG_CS, callerSelector);
            emulator.RegWrite(X86.UC_X86_REG_DS, dataSelector);
            emulator.RegWrite(X86.UC_X86_REG_SS, stackSelector);
            emulator.RegWrite(X86.UC_X86_REG_ESP, initialStackPointer);
            emulator.RegWrite(X86.UC_X86_REG_EAX, 0);
            emulator.RegWrite(X86.UC_X86_REG_EBX, 0);
            emulator.RegWrite(X86.UC_X86_REG_EDX, 0);

            Console.WriteLine("Caller 0008:0000 -> callee 0010:0200 -> caller 0008:0008");
            Console.WriteLine("Segment bases: caller=0x10000, callee=0x20000, data=0x30000, stack=0x40000");

            // In this engine mode, beginAddr is written to EIP (an offset), while
            // untilAddr is compared with the linear execution address (CS.base + IP).
            emulator.EmuStart(beginAddr: 0, untilAddr: callerBase + callerCode.Length,
                timeout: 1_000_000, count: 32);

            long resultInAx = emulator.RegRead(X86.UC_X86_REG_AX);
            long observedCalleeSelector = emulator.RegRead(X86.UC_X86_REG_BX);
            long observedCalleeStackPointer = emulator.RegRead(X86.UC_X86_REG_DX);
            long finalStackPointer = emulator.RegRead(X86.UC_X86_REG_SP);
            long finalCodeSelector = emulator.RegRead(X86.UC_X86_REG_CS);
            long finalInstructionOffset = emulator.RegRead(X86.UC_X86_REG_EIP);
            long finalDataSelector = emulator.RegRead(X86.UC_X86_REG_DS);
            long finalStackSelector = emulator.RegRead(X86.UC_X86_REG_SS);
            bool protectedMode = (emulator.RegRead(X86.UC_X86_REG_CR0) & 1) != 0;

            byte[] resultBytes = new byte[2];
            emulator.MemRead(dataBase + resultOffset, resultBytes);
            ushort resultInMemory = BinaryPrimitives.ReadUInt16LittleEndian(resultBytes);

            // A 16-bit far CALL pushes CS, then IP: IP is at the lower address/top.
            byte[] savedReturnBytes = new byte[4];
            emulator.MemRead(stackBase + expectedCalleeStackPointer, savedReturnBytes);
            ushort savedIp = BinaryPrimitives.ReadUInt16LittleEndian(savedReturnBytes.AsSpan(0, 2));
            ushort savedCs = BinaryPrimitives.ReadUInt16LittleEndian(savedReturnBytes.AsSpan(2, 2));

            Console.WriteLine($"Protected mode: {protectedMode}; callee CS captured in BX = 0x{observedCalleeSelector:X4}");
            Console.WriteLine($"AX = {resultInAx}; guest result at 0018:0020 (linear 0x{dataBase + resultOffset:X5}) = {resultInMemory}");
            Console.WriteLine($"SP: before = 0x{initialStackPointer:X4}, inside call = 0x{observedCalleeStackPointer:X4}, after = 0x{finalStackPointer:X4}");
            Console.WriteLine($"Saved return CS:IP = {savedCs:X4}:{savedIp:X4}");
            Console.WriteLine($"Final CS:IP = {finalCodeSelector:X4}:{finalInstructionOffset:X4}; DS = 0x{finalDataSelector:X4}; SS = 0x{finalStackSelector:X4}");

            if (!protectedMode || resultInAx != 12 || resultInMemory != 12 ||
                observedCalleeSelector != calleeSelector || observedCalleeStackPointer != expectedCalleeStackPointer ||
                finalStackPointer != initialStackPointer || savedIp != returnOffset || savedCs != callerSelector ||
                finalCodeSelector != callerSelector || finalInstructionOffset != callerCode.Length ||
                finalDataSelector != dataSelector || finalStackSelector != stackSelector)
            {
                throw new InvalidOperationException(
                    "Expected protected mode, AX=memory=12, callee CS=0010, in-call SP=0FFC, restored SP=1000, " +
                    "saved return=0008:0008, final CS:IP=0008:000B, DS=0018, and SS=0020.");
            }

            Console.WriteLine("PASS: CALL FAR / RETF restored CS:IP and SP; the guest stored the returned value.");
        }
        finally
        {
            emulator.Close();
        }
    }

    // This lesson's descriptors use byte limits, 16-bit defaults, and privilege 0.
    // Keeping the hardware byte layout here avoids hiding it behind a general loader.
    private static byte[] Create16BitDescriptor(uint baseAddress, ushort inclusiveLimit, bool executable)
    {
        byte[] descriptor = new byte[8];
        BinaryPrimitives.WriteUInt16LittleEndian(descriptor.AsSpan(0, 2), inclusiveLimit);
        BinaryPrimitives.WriteUInt16LittleEndian(descriptor.AsSpan(2, 2), (ushort)baseAddress);
        descriptor[4] = (byte)(baseAddress >> 16);
        // Present, privilege 0, code/data, readable code or writable data, accessed.
        descriptor[5] = executable ? (byte)0x9B : (byte)0x93;
        descriptor[6] = 0; // G=0: byte limit; D/B=0: 16-bit code/stack; upper limit bits=0.
        descriptor[7] = (byte)(baseAddress >> 24);
        return descriptor;
    }

    private static void WriteDescriptorTableRegister(Unicorn emulator, uint baseAddress, uint inclusiveLimit)
    {
        // Native uc_x86_mmr layout on our x64 host: selector at 0, base at 8,
        // limit at 16, flags at 20; total 24 bytes including alignment padding.
        // This is the Unicorn API buffer, not an x86 descriptor in guest memory.
        byte[] gdtr = new byte[24];
        BinaryPrimitives.WriteUInt64LittleEndian(gdtr.AsSpan(8, 8), baseAddress);
        BinaryPrimitives.WriteUInt32LittleEndian(gdtr.AsSpan(16, 4), inclusiveLimit);
        emulator.RegWrite(X86.UC_X86_REG_GDTR, gdtr);
    }
}
