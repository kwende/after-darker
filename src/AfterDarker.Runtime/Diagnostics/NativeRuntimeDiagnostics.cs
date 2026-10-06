using UnicornEngine;
using UnicornEngine.Const;

namespace AfterDarker.Runtime;

/// <summary>Small source-owned execution probe for a consumer's installed native engine and apphost.</summary>
/// <remarks>This proves native loading and hook stop/resume, not NE loading or a supported AD module.
/// Run inside the real consuming executable: checking from a different host misses its CFG setting.</remarks>
public static class NativeRuntimeDiagnostics
{
    /// <summary>Execute MOV AX,7 and ADD AX,5, stopping before ADD and resuming from the host.</summary>
    /// <returns>The computed AX value, 12. Unexpected execution state throws.</returns>
    public static ushort VerifyExecution()
    {
        const long codeAddress = 0x1000;
        const long addAddress = codeAddress + 3;
        const long endAddress = codeAddress + 6;
        using var emulator = new Unicorn(Common.UC_ARCH_X86, Common.UC_MODE_16);
        try
        {
            emulator.MemMap(codeAddress, 0x1000, Common.UC_PROT_ALL);
            emulator.MemWrite(codeAddress, [0xB8, 7, 0, 0x05, 5, 0]);
            emulator.RegWrite(X86.UC_X86_REG_CS, 0);
            bool stopped = false;
            CodeHook hook = (engine, address, size, userData) =>
            {
                if (stopped) return;
                stopped = true;
                engine.EmuStop();
            };
            emulator.AddCodeHook(hook, addAddress, addAddress);
            emulator.EmuStart(codeAddress, endAddress, timeout: 1_000_000, count: 10);
            if (!stopped || emulator.RegRead(X86.UC_X86_REG_AX) != 7 || emulator.RegRead(X86.UC_X86_REG_IP) != addAddress)
                throw new InvalidOperationException("Native execution did not stop before the addition.");
            emulator.EmuStart(addAddress, endAddress, timeout: 1_000_000, count: 10);
            GC.KeepAlive(hook);
            ushort result = (ushort)emulator.RegRead(X86.UC_X86_REG_AX);
            if (result != 12 || emulator.RegRead(X86.UC_X86_REG_IP) != endAddress)
                throw new InvalidOperationException("Native execution did not resume and complete 7 + 5.");
            return result;
        }
        finally
        {
            // The 2.1.3 binding separates native engine Close from managed Dispose.
            emulator.Close();
        }
    }
}
