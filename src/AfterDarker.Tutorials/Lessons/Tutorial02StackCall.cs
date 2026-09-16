using System.Buffers.Binary;
using UnicornEngine;
using UnicornEngine.Const;

namespace AfterDarker.Tutorials.Lessons;

/// <summary>
/// Allocate a guest stack, call a guest subroutine, and verify its return.
/// Everything runs in 16-bit real mode, without hooks or host callbacks.
/// </summary>
public sealed class Tutorial02StackCall : ITutorial
{
    public string Id => "02";
    public string Title => "A guest stack and a near CALL/RET";

    public void Run()
    {
        Result observed = Execute(Console.Out);
        if (observed is not { Ax: 12, CalleeSp: 0x8FFE, FinalSp: 0x9000,
            SavedReturnIp: 0x1006, FinalIp: 0x100E, FinalCs: 0, FinalSs: 0 })
            throw new InvalidOperationException($"Near CALL/RET state did not match the lesson: {observed}.");
        Console.WriteLine("PASS: the guest CALL/RET used the stack and restored SP.");
    }

    public Result Execute(TextWriter? output = null)
    {
        output ??= TextWriter.Null;
        const long codeAddress = 0x1000;
        const long pageSize = 0x1000;
        const long stackAddress = 0x8000;
        const long initialStackPointer = stackAddress + pageSize; // 0x9000, just above the stack.
        const long expectedCalleeStackPointer = initialStackPointer - 2;

        byte[] code =
        [
            // Main:
            0xB8, 0x07, 0x00, // 0x1000: MOV AX, 7
            0xE8, 0x02, 0x00, // 0x1003: CALL AddFive -- displacement +2 from 0x1006
            0xEB, 0x06,       // 0x1006: JMP Done     -- skip the subroutine after RET

            // AddFive:
            0x89, 0xE2,       // 0x1008: MOV DX, SP   -- preserve the in-call SP for C# to inspect
            0x05, 0x05, 0x00, // 0x100A: ADD AX, 5
            0xC3,             // 0x100D: RET          -- pop the saved instruction offset
            // Done: 0x100E, the end address supplied to EmuStart.
        ];
        long endAddress = codeAddress + code.Length;

        using var emulator = new Unicorn(Common.UC_ARCH_X86, Common.UC_MODE_16);
        try
        {
            emulator.MemMap(codeAddress, pageSize, Common.UC_PROT_ALL);
            emulator.MemWrite(codeAddress, code);

            // A separate 4 KiB region of guest memory: 0x8000 through 0x8FFF.
            // Mapping it does not designate it as a stack; SS:SP does that.
            emulator.MemMap(stackAddress, pageSize, Common.UC_PROT_READ | Common.UC_PROT_WRITE);
            emulator.RegWrite(X86.UC_X86_REG_SS, 0);
            emulator.RegWrite(X86.UC_X86_REG_SP, initialStackPointer);

            // In this real-mode lesson, CS=SS=0 keeps offsets equal to guest addresses.
            // CALL decrements SP by two BEFORE writing its 16-bit return address.
            emulator.RegWrite(X86.UC_X86_REG_CS, 0);
            emulator.RegWrite(X86.UC_X86_REG_AX, 0);
            emulator.RegWrite(X86.UC_X86_REG_DX, 0);
            emulator.MemWrite(expectedCalleeStackPointer, [0xCC, 0xCC]); // Must be overwritten by CALL.

            output.WriteLine("Guest stack: 0x8000..0x8FFF; SS = 0x0000");
            output.WriteLine("Guest: MOV AX, 7; CALL AddFive; JMP Done; AddFive: MOV DX, SP; ADD AX, 5; RET");
            emulator.EmuStart(codeAddress, endAddress, timeout: 1_000_000, count: 16);

            long result = emulator.RegRead(X86.UC_X86_REG_AX);
            long calleeStackPointer = emulator.RegRead(X86.UC_X86_REG_DX);
            long finalStackPointer = emulator.RegRead(X86.UC_X86_REG_SP);
            long instructionPointer = emulator.RegRead(X86.UC_X86_REG_IP);
            long codeSegment = emulator.RegRead(X86.UC_X86_REG_CS);
            long stackSegment = emulator.RegRead(X86.UC_X86_REG_SS);

            // RET consumes the word by advancing SP; it does not erase those bytes.
            byte[] savedReturnBytes = new byte[2];
            emulator.MemRead(expectedCalleeStackPointer, savedReturnBytes);
            ushort savedReturnAddress = BinaryPrimitives.ReadUInt16LittleEndian(savedReturnBytes);

            output.WriteLine($"AX = {result} (0x{result:X4})");
            output.WriteLine($"SP: before = 0x{initialStackPointer:X4}, inside call = 0x{calleeStackPointer:X4}, after = 0x{finalStackPointer:X4}");
            output.WriteLine($"Saved return word at 0x{expectedCalleeStackPointer:X4} = 0x{savedReturnAddress:X4}");
            output.WriteLine($"Final CS:IP = {codeSegment:X4}:{instructionPointer:X4}; SS = 0x{stackSegment:X4}");
            return new(result, calleeStackPointer, finalStackPointer, savedReturnAddress,
                instructionPointer, codeSegment, stackSegment);
        }
        finally
        {
            emulator.Close(); // Binding 2.1.3 requires explicit native-engine cleanup.
        }
    }

    public sealed record Result(long Ax, long CalleeSp, long FinalSp, ushort SavedReturnIp,
        long FinalIp, long FinalCs, long FinalSs);
}
