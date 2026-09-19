using System.Buffers.Binary;
using System.Diagnostics;
using AfterDarker.Core.Ne;
using AfterDarker.Core.Win16;
using AfterDarker.Core.X86;
using UnicornEngine;
using UnicornEngine.Const;

namespace AfterDarker.Runtime;

/// <summary>
/// The small execution mechanism owned by a session and exercised by synthetic CPU tests.
/// No After Dark fields or Windows handlers live here. Read RunUntil to see the
/// stop -> managed dispatch -> resume loop; neither kind of hook runs a service.
/// </summary>
public sealed class SegmentedGuest : IGuestMemory16, IDisposable
{
    public const int PageBytes = 0x1000, DescriptorBytes = 8, MaximumSegments = 32;
    private const uint GdtBase = 0x300000;
    private readonly Unicorn engine;
    private readonly Dictionary<ushort, Region> regions = [];
    private readonly byte[] gdt = new byte[(MaximumSegments + 1) * DescriptorBytes];
    private readonly TextWriter output;
    private readonly bool trace;
    private readonly int instructionLimit;
    private ushort gatewaySelector;
    private bool gatewayReached, installed;
    private int? interrupt;
    private Exception? hookError;
    private CpuState? beforeSoftwareInterrupt;
    private TimeSpan executionTime;
    private int exits, phaseInstructions;
    public long Instructions { get; private set; }
    public string Phase { get; private set; } = "setup";
    public Action? DispatchGateway { get; set; }
    public Action<int>? DispatchInterrupt { get; set; }
    private readonly DiagnosticHistory<InterruptVisit> interrupts;
    public IReadOnlyList<InterruptVisit> Interrupts => interrupts.Snapshot();
    public long InterruptCount => interrupts.TotalCount;

    private sealed record Region(uint Base, int Size, bool Code);
    public sealed record CpuState(ushort Cs, ushort Ip, ushort Ax, ushort Bx, ushort Cx, ushort Dx,
        ushort Ds, ushort Es, ushort Ss, ushort Sp, ushort Bp, ushort Si, ushort Di, uint Flags)
    {
        public FarPointer16 Pc => new(Cs, Ip);
    }
    public sealed record InterruptVisit(int Number, CpuState BeforeInstruction, CpuState AtHook, CpuState AfterHandler);

    public SegmentedGuest(TextWriter? output = null, bool trace = false, int instructionLimit = 50_000,
        DiagnosticOptions? diagnostics = null)
    {
        if (instructionLimit is < 1 or > 1_000_000) throw new ArgumentOutOfRangeException(nameof(instructionLimit));
        interrupts = new(diagnostics ?? DiagnosticOptions.Recent);
        engine = new(Common.UC_ARCH_X86, Common.UC_MODE_32);
        this.output = output ?? TextWriter.Null;
        this.trace = trace;
        this.instructionLimit = instructionLimit;
    }

    public void Map(ushort selector, uint linearBase, byte[] bytes, bool code)
    {
        if (installed) throw new InvalidOperationException("Map all segments before installing descriptors.");
        if (selector == 0 || selector % DescriptorBytes != 0 || selector / DescriptorBytes > MaximumSegments ||
            bytes.Length is < 1 or > 65536 || linearBase % PageBytes != 0)
            throw new ArgumentException("Invalid guest segment placement.");
        int mappedBytes = (bytes.Length + PageBytes - 1) & ~(PageBytes - 1);
        engine.MemMap(linearBase, mappedBytes, code ? Common.UC_PROT_READ | Common.UC_PROT_EXEC
            : Common.UC_PROT_READ | Common.UC_PROT_WRITE);
        engine.MemWrite(linearBase, bytes);
        regions.Add(selector, new(linearBase, bytes.Length, code));
        SegmentDescriptor16.Encode(linearBase, checked((ushort)(bytes.Length - 1)), code).CopyTo(gdt, selector);
    }

    public void Install(ushort gateway)
    {
        if (installed) throw new InvalidOperationException("Descriptors already installed.");
        gatewaySelector = gateway;
        engine.MemMap(GdtBase, PageBytes, Common.UC_PROT_READ | Common.UC_PROT_WRITE);
        engine.MemWrite(GdtBase, gdt);
        // GDTR describes the TABLE, while each eight-byte descriptor describes
        // one segment. D/B=0 in those descriptors selects 16-bit code/stack use.
        byte[] gdtr = new byte[24];
        BinaryPrimitives.WriteUInt64LittleEndian(gdtr.AsSpan(8), GdtBase);
        BinaryPrimitives.WriteUInt32LittleEndian(gdtr.AsSpan(16), (uint)gdt.Length - 1);
        engine.RegWrite(X86.UC_X86_REG_GDTR, gdtr);
        Set(X86.UC_X86_REG_CR0, Get(X86.UC_X86_REG_CR0) | 1);
        Set(X86.UC_X86_REG_EFLAGS, 2); // Reserved bit set; direction flag clear.

        CodeHook codeHook = (_, address, size, _) =>
        {
            try
            {
                if (Instructions < long.MaxValue) Instructions++;
                if (++phaseInstructions > instructionLimit) throw new InvalidOperationException("Instruction budget exhausted.");
                CpuState state = Snapshot();
                if (state.Cs == gatewaySelector)
                {
                    gatewayReached = true;
                    engine.EmuStop(); // Stop BEFORE fetching a gateway's UD2 guard.
                    return;
                }
                RequireCode(state.Pc, size);
                if (Translate(state.Pc, size) != address) throw new InvalidOperationException("CS:IP disagrees with hook address.");
                byte[] instruction = Read(state.Pc, size);
                // For this boundary, only an explicit INT imm8 can be serviced.
                // A CPU fault reporting interrupt 13 is not a DOS API request.
                beforeSoftwareInterrupt = instruction.Length == 2 && instruction[0] == 0xCD ? state : null;
                if (trace) this.output.WriteLine($"  {Phase} {state.Pc} {Convert.ToHexString(instruction),-14} " +
                    $"AX={state.Ax:X4} DS={state.Ds:X4} SS:SP={state.Ss:X4}:{state.Sp:X4}");
            }
            catch (Exception error) { hookError = error; engine.EmuStop(); }
        };
        InterruptHook interruptHook = (_, number, _) =>
        {
            // No exceptions or application logic may escape a native callback.
            interrupt = number;
            engine.EmuStop();
        };
        engine.AddCodeHook(codeHook, 1, 0);
        engine.AddInterruptHook(interruptHook);
        installed = true;
    }

    public const int DefaultServiceExitLimit = 128;

    public CpuState RunUntil(string phase, FarPointer16 start, FarPointer16 end,
        int serviceExitLimit = DefaultServiceExitLimit)
    {
        if (serviceExitLimit is < 1 or > 1024) throw new ArgumentOutOfRangeException(nameof(serviceExitLimit));
        // Budgets apply to each bounded host invocation. Keeping a lifetime
        // counter is useful diagnostics, but must not kill a healthy animation
        // merely because many completed DRAWFRAME calls preceded this one.
        // Independent of the lifetime total: even a saturated diagnostic counter
        // must never disable or shorten the next invocation's safety budget.
        phaseInstructions = 0;
        exits = 0;
        executionTime = TimeSpan.Zero;
        Phase = phase;
        RequireCode(start, 1);
        RequireCode(end, 1);
        Set(X86.UC_X86_REG_CS, start.Selector);
        long resume = start.Offset;
        while (true)
        {
            if (executionTime > TimeSpan.FromSeconds(5))
                throw new InvalidOperationException($"{Phase}: service/time budget exhausted.");
            gatewayReached = false;
            interrupt = null;
            long began = Stopwatch.GetTimestamp();
            try
            {
                // Unicorn's begin is an EIP OFFSET; until is a LINEAR address.
                // MemWrite supplied bytes earlier; this call fetches/decodes them.
                engine.EmuStart(resume, Translate(end, 1), timeout: 1_000_000, count: instructionLimit);
            }
            catch (Exception error)
            {
                throw new InvalidOperationException($"{Phase}: guest execution failed at {Snapshot()}: {error.Message}", error);
            }
            finally { executionTime += Stopwatch.GetElapsedTime(began); }
            if (hookError is not null) throw new InvalidOperationException($"{Phase} at {Snapshot().Pc}: {hookError.Message}", hookError);
            if ((interrupt is not null || gatewayReached) && ++exits > serviceExitLimit)
                throw new InvalidOperationException($"{Phase}: service budget ({serviceExitLimit}) exhausted.");

            if (interrupt is int number)
            {
                CpuState atHook = Snapshot();
                CpuState before = beforeSoftwareInterrupt ?? throw new InvalidOperationException(
                    $"{Phase}: CPU exception/unsupported interrupt {number:X2} at {atHook.Pc}.");
                byte[] opcode = Read(before.Pc, 2);
                if (opcode[1] != number || atHook.Cs != before.Cs || atHook.Ip != before.Ip + 2 ||
                    atHook.Sp != before.Sp || atHook.Ss != before.Ss || atHook.Flags != before.Flags)
                    throw new InvalidOperationException("Unicorn INT boundary differs from the tested no-frame, advanced-IP contract.");
                // Unicorn intercepted INT itself: it has already advanced IP and
                // has NOT pushed an interrupt frame. Do not simulate RETF/IRET or
                // advance IP again. The conformance test verifies these facts.
                (DispatchInterrupt ?? throw new NotSupportedException("No interrupt handler installed."))(number);
                CpuState after = Snapshot();
                if (after.Pc != atHook.Pc || after.Sp != atHook.Sp || after.Ss != atHook.Ss || after.Flags != atHook.Flags)
                    throw new InvalidOperationException("DOS handler changed control flow, stack, or flags.");
                interrupts.Add(new(number, before, atHook, after));
            }
            else if (gatewayReached)
            {
                (DispatchGateway ?? throw new NotSupportedException("No import handler installed."))();
            }
            else
            {
                CpuState final = Snapshot();
                if (final.Pc != end) throw new InvalidOperationException($"{Phase}: stopped at {final.Pc}, expected {end}; budget/HLT is not success.");
                return final;
            }
            resume = Get(X86.UC_X86_REG_EIP);
        }
    }

    public long Get(int register) => engine.RegRead(register);
    public void Set(int register, long value) => engine.RegWrite(register, value);
    public CpuState Snapshot() => new(W(X86.UC_X86_REG_CS), W(X86.UC_X86_REG_EIP), W(X86.UC_X86_REG_AX),
        W(X86.UC_X86_REG_BX), W(X86.UC_X86_REG_CX), W(X86.UC_X86_REG_DX), W(X86.UC_X86_REG_DS),
        W(X86.UC_X86_REG_ES), W(X86.UC_X86_REG_SS), W(X86.UC_X86_REG_SP), W(X86.UC_X86_REG_BP),
        W(X86.UC_X86_REG_SI), W(X86.UC_X86_REG_DI), (uint)Get(X86.UC_X86_REG_EFLAGS));
    private ushort W(int register) => checked((ushort)Get(register));

    public byte[] Read(FarPointer16 address, int count)
    {
        long linear = Translate(address, count);
        byte[] bytes = new byte[count];
        engine.MemRead(linear, bytes);
        return bytes;
    }
    public void Write(FarPointer16 address, byte[] bytes) => engine.MemWrite(Translate(address, bytes.Length, write: true), bytes);
    public void RequireCode(FarPointer16 address, int count)
    {
        if (count is < 1 or > 15 || !regions.TryGetValue(address.Selector, out Region? region) || !region.Code)
            throw new InvalidOperationException($"Invalid code address/instruction size at {address}.");
        _ = Translate(address, count);
    }
    private long Translate(FarPointer16 address, int count, bool write = false)
    {
        if (!regions.TryGetValue(address.Selector, out Region? region) || count < 0 ||
            address.Offset > region.Size - count || (write && region.Code))
            throw new InvalidOperationException($"Invalid guest {(write ? "write" : "read")} at {address}, length {count}.");
        return region.Base + address.Offset;
    }
    public void Dispose() { engine.Close(); engine.Dispose(); }
}
