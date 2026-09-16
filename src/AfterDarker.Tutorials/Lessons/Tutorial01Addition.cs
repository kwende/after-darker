using UnicornEngine;
using UnicornEngine.Const;

namespace AfterDarker.Tutorials.Lessons;

/// <summary>
/// C# supplies instruction bytes; Unicorn executes them; C# reads the result.
/// This lesson uses 16-bit real mode, not Win16 protected-mode selectors.
/// </summary>
public sealed class Tutorial01Addition : ITutorial
{
    public string Id => "01";
    public string Title => "Add two constants in 16-bit x86";

    public void Run()
    {
        Result observed = Execute(output: Console.Out);
        if (observed is not { Ax: 12, Ip: 0x1006 })
            throw new InvalidOperationException($"Expected AX=12 and IP=0x1006; got {observed}.");
        Console.WriteLine("PASS: the guest computed 7 + 5 and reached the end of its code.");
    }

    // The console and tests execute the same instructions. Tests inspect values,
    // not formatted console output, and can vary the two word-sized operands.
    public Result Execute(ushort left = 7, ushort right = 5, TextWriter? output = null)
    {
        output ??= TextWriter.Null;
        // These are guest addresses, not pointers into our C# process.
        const long codeAddress = 0x1000;
        const long mappedSize = 0x1000; // Unicorn maps memory in aligned 4 KiB pages.

        // Hand-encoded instructions: opcode followed by a little-endian 16-bit value.
        // Keeping assembly beside its bytes avoids needing an assembler for lesson 01.
        byte[] code =
        [
            0xB8, (byte)left, (byte)(left >> 8),   // MOV AX, left  (7 in the lesson)
            0x05, (byte)right, (byte)(right >> 8), // ADD AX, right (5 in the lesson)
        ];
        long endAddress = codeAddress + code.Length;

        using var emulator = new Unicorn(Common.UC_ARCH_X86, Common.UC_MODE_16);
        try
        {
            emulator.MemMap(codeAddress, mappedSize, Common.UC_PROT_ALL);
            emulator.MemWrite(codeAddress, code);
            emulator.RegWrite(X86.UC_X86_REG_CS, 0); // Real-mode CS=0 makes IP match our address.
            emulator.RegWrite(X86.UC_X86_REG_AX, 0);

            output.WriteLine($"Guest: MOV AX, {left}; ADD AX, {right}");

            // Run synchronously until the first byte AFTER our code (0x1006).
            // No hooks, callbacks, or guest exit syscall are required.
            // Timeout is in microseconds; count is a fallback instruction budget.
            emulator.EmuStart(codeAddress, endAddress, timeout: 1_000_000, count: 16);

            long result = emulator.RegRead(X86.UC_X86_REG_AX);
            long instructionPointer = emulator.RegRead(X86.UC_X86_REG_IP);
            output.WriteLine($"AX = {result} (0x{result:X4})");
            return new(result, instructionPointer);
        }
        finally
        {
            // In binding 2.1.3, Dispose frees binding allocations; Close releases the native engine.
            emulator.Close();
        }
    }

    public sealed record Result(long Ax, long Ip);
}
