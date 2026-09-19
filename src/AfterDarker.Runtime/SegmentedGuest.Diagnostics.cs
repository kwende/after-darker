using AfterDarker.Core.Ne;

namespace AfterDarker.Runtime;

// Kept nested for source compatibility with the lessons and their trace structures.
public sealed partial class SegmentedGuest
{
    /// <summary>Detached 16-bit register values at an instruction or service boundary.</summary>
    /// <param name="Cs">Code selector; combines with IP to identify the next instruction.</param>
    /// <param name="Ip">Instruction offset within CS, not a linear address.</param>
    /// <param name="Ax">Accumulator; word results and low halves of DWORD results use this register.</param>
    /// <param name="Bx">General-purpose base register, owned by executing guest code.</param>
    /// <param name="Cx">Count register; startup heap size or GlobalLock's additional selector result.</param>
    /// <param name="Dx">Data register; high half of a DWORD or far-pointer result.</param>
    /// <param name="Ds">Default data selector, established by the caller or DLL prologue.</param>
    /// <param name="Es">Extra data selector; used by guest pointer accesses and our result store.</param>
    /// <param name="Ss">Stack selector; combines with SP to address the top stack word.</param>
    /// <param name="Sp">Descending-stack offset: pushes decrease it; returns increase it.</param>
    /// <param name="Bp">Stack-frame base pointer, preserved across supported calls.</param>
    /// <param name="Si">Source index; part of the ES:SI command-line pointer at DLL startup.</param>
    /// <param name="Di">Destination index; the DLL instance token at startup.</param>
    /// <param name="Flags">Full EFLAGS snapshot, including arithmetic and control bits.</param>
    public sealed record CpuState(ushort Cs, ushort Ip, ushort Ax, ushort Bx, ushort Cx, ushort Dx,
        ushort Ds, ushort Es, ushort Ss, ushort Sp, ushort Bp, ushort Si, ushort Di, uint Flags)
    {
        /// <summary>The segmented program counter, expressed as CS:IP.</summary>
        public FarPointer16 Pc => new(Cs, Ip);
    }
    /// <summary>Evidence that a software interrupt resumed without a synthetic far return.</summary>
    /// <param name="Number">INT immediate, such as 0x21 for DOS.</param>
    /// <param name="BeforeInstruction">Registers before the guest INT instruction.</param>
    /// <param name="AtHook">Registers after Unicorn advanced IP and stopped.</param>
    /// <param name="AfterHandler">Registers after the managed service response.</param>
    public sealed record InterruptVisit(int Number, CpuState BeforeInstruction, CpuState AtHook, CpuState AfterHandler);

}
