using System.Buffers.Binary;
using System.Diagnostics;
using AfterDarker.Core.Ne;
using AfterDarker.Core.Win16;
using AfterDarker.Core.X86;
using UnicornEngine;
using UnicornEngine.Const;

namespace AfterDarker.Tutorials.Lessons;

/// <summary>
/// Read this lesson from Execute downward. The numbered stages follow the guide
/// docs/tutorial-06-load-library.md. Our loader prepares bytes; Unicorn executes
/// those bytes. C# never implements HelloWorld and never supplies its result.
/// </summary>
public sealed class Tutorial06LoadLibrary(string? path = null, bool trace = false) : ITutorial
{
    public string Id => "06";
    public string Title => "Load a Watcom Win16 DLL, initialize it, and call HELLOWORLD";

    // These addresses are our allocation choices, not numbers prescribed by NE.
    // A selector is a GDT byte offset here: index * 8, privilege 0, GDT not LDT.
    public const ushort DllCode = 0x08, DllData = 0x10, CallerCode = 0x18;
    public const ushort Gateway = 0x20, Stack = 0x28, CallerData = 0x30;
    public const ushort InitialSp = 0x1000;
    private const uint CallerBase = 0x30000, GatewayBase = 0x40000;
    private const uint StackBase = 0x50000, CallerDataBase = 0x60000, GdtBase = 0x70000;
    private const ushort InitResult = 0x20, HelloResult = 0x22, ExitResult = 0x24;
    private const ushort Marker = 0xCCCC;

    // This deliberately small canonical table binds real import identities to
    // synthetic addresses. Address choice is ours; parameter layout is the ABI.
    public static IReadOnlyList<NeImportBinding> Bindings { get; } = Array.AsReadOnly(new[]
    {
        new NeImportBinding(new("KERNEL", 3, null), "KERNEL!GetVersion (#3)",
            new(Gateway, 0x100), 0, Win16ReturnLayout.DwordInDxAx, "FixedWindowsVersion"),
        new NeImportBinding(new("KERNEL", 4, null), "KERNEL!LocalInit (#4)",
            new(Gateway, 0x110), 6, Win16ReturnLayout.WordInAx, "CheckedHeapTestDouble"),
        new NeImportBinding(new("USER", 1, null), "USER!MessageBox (#1)",
            new(Gateway, 0x120), 10, Win16ReturnLayout.WordInAx, "FailIfReached"),
    });

    public void Run()
    {
        string fixture = path ?? FindFixture();
        using FileStream stream = File.OpenRead(fixture);
        if (stream.Length > NeReader.MaximumFileBytes) throw new InvalidDataException("Fixture is too large.");
        byte[] bytes = new byte[(int)stream.Length];
        stream.ReadExactly(bytes);
        Result result = Execute(bytes, Console.Out, trace);
        if (!result.Initialized || result.StoredInitialization != 1 || result.Hello?.Ax != 42 || result.StoredHello != 42 ||
            result.Exit?.Ax != 1 || result.StoredExit != 1 || !result.ProtectedMode)
            throw new InvalidOperationException("The DLL did not initialize, return/store 42, and complete WEP.");
        Console.WriteLine("PASS: compiled DLL startup returned 1; HELLOWORLD returned 42; guest stored 42; WEP returned 1.");
        Console.WriteLine("Scope: Hello42 only. LocalInit is a checked test double, not a Windows heap allocator.");
    }

    public Result Execute(byte[] file, TextWriter? output = null, bool traceInstructions = false,
        bool localInitSucceeds = true, uint version = 0x00000A03, int instructionLimit = 10_000)
    {
        output ??= TextWriter.Null;
        if (instructionLimit is < 1 or > 100_000) throw new ArgumentOutOfRangeException(nameof(instructionLimit));

        // 1. PARSE AND PREPARE. No Unicorn yet; plan.Bytes are ordinary C# arrays.
        NeLoadPlan plan = NeLoadPlan.Create(file,
            [new(1, DllCode, 0x10000), new(2, DllData, 0x20000)], Bindings);
        if (!plan.Image.Names.Any(n => n.Ordinal == 0 && n.Text == "HELLO42") ||
            plan.Segments.Count != 2 || plan.Image.Header.AutomaticDataSegment != 2)
            throw new NotSupportedException("Tutorial 06 accepts the project-owned HELLO42 fixture only.");
        FarPointer16 startup = plan.ResolveCode(plan.Image.Header.Startup
            ?? throw new InvalidDataException("DLL has no startup entry."));
        FarPointer16 hello = Export("HELLOWORLD");
        FarPointer16 wep = Export("WEP");
        PreparedNeSegment data = plan.Segments.Single(s => s.Source.Number == 2);
        output.WriteLine("1. FILE -> PREPARED SEGMENTS (file segment numbers become assigned selectors)");
        foreach (PreparedNeSegment segment in plan.Segments)
            output.WriteLine($"   S{segment.Source.Number}: file 0x{segment.Source.FileOffset:X} -> " +
                $"selector {segment.Placement.Selector:X4}, base 0x{segment.Placement.LinearBase:X5}, " +
                $"{segment.Source.FileBytes} stored / {segment.Bytes.Length} allocated bytes");
        foreach (NePatch patch in plan.Patches)
            output.WriteLine($"   {patch.Source}: {Convert.ToHexString(patch.Before)} -> " +
                $"{Convert.ToHexString(patch.After)} ({patch.Reason})");
        output.WriteLine($"   Startup={startup}; HELLOWORLD={hello}; WEP={wep}");

        // 2. CREATE THE GUEST MACHINE. MODE_32 selects Unicorn's protected-mode
        // API behavior; D/B=0 in EVERY descriptor selects 16-bit instructions/stacks.
        using var emulator = new Unicorn(Common.UC_ARCH_X86, Common.UC_MODE_32);
        try
        {
            byte[] gdt = new byte[7 * 8]; // slot 0 is the architecturally null descriptor
            foreach (PreparedNeSegment segment in plan.Segments)
            {
                long pageBytes = (segment.Bytes.Length + 0xFFF) & ~0xFFF;
                int protection = segment.Source.IsData
                    ? Common.UC_PROT_READ | Common.UC_PROT_WRITE
                    : Common.UC_PROT_READ | Common.UC_PROT_EXEC;
                emulator.MemMap(segment.Placement.LinearBase, pageBytes, protection);
                // THIS is where the DLL's raw bytes enter Unicorn's guest memory.
                // MemWrite is a host operation; it does not execute these bytes.
                emulator.MemWrite(segment.Placement.LinearBase, segment.Bytes);
                SegmentDescriptor16.Encode(segment.Placement.LinearBase,
                    checked((ushort)(segment.Bytes.Length - 1)), !segment.Source.IsData)
                    .CopyTo(gdt, segment.Placement.Selector);
            }
            MapHostSegment(CallerCode, CallerBase, true);
            MapHostSegment(Gateway, GatewayBase, true);
            MapHostSegment(Stack, StackBase, false);
            MapHostSegment(CallerData, CallerDataBase, false);
            emulator.MemMap(GdtBase, 0x1000, Common.UC_PROT_READ | Common.UC_PROT_WRITE);
            emulator.MemWrite(GdtBase, gdt);
            emulator.MemWrite(CallerDataBase + InitResult, [0xCC, 0xCC, 0xCC, 0xCC, 0xCC, 0xCC]);
            foreach (NeImportBinding binding in Bindings)
                emulator.MemWrite(GatewayBase + binding.Address.Offset, [0x0F, 0x0B]); // UD2 if hook fails to stop

            // Three tiny guest callers live at disjoint offsets in one code segment.
            // Each executes a real CALL FAR, then a real store through ES. We run
            // startup first and inspect its return BEFORE allowing HelloWorld.
            WriteCaller(0x000, startup, InitResult, commandLineIsNull: true);
            WriteCaller(0x100, hello, HelloResult);
            WriteCaller(0x200, wep, ExitResult, pushExitReason: true);

            // GDTR is a native Unicorn API structure, NOT an x86 descriptor.
            byte[] gdtr = new byte[24];
            BinaryPrimitives.WriteUInt64LittleEndian(gdtr.AsSpan(8), GdtBase);
            BinaryPrimitives.WriteUInt32LittleEndian(gdtr.AsSpan(16), (uint)gdt.Length - 1);
            emulator.RegWrite(X86.UC_X86_REG_GDTR, gdtr);
            emulator.RegWrite(X86.UC_X86_REG_CR0, emulator.RegRead(X86.UC_X86_REG_CR0) | 1);
            emulator.RegWrite(X86.UC_X86_REG_CS, CallerCode);
            emulator.RegWrite(X86.UC_X86_REG_DS, DllData);
            emulator.RegWrite(X86.UC_X86_REG_ES, 0); // ES:SI = NULL command line for DLL startup
            emulator.RegWrite(X86.UC_X86_REG_SS, Stack);
            emulator.RegWrite(X86.UC_X86_REG_ESP, InitialSp);
            emulator.RegWrite(X86.UC_X86_REG_EBP, 0); // a known empty frame chain; Watcom marks far frames in BP
            emulator.RegWrite(X86.UC_X86_REG_ESI, 0);
            emulator.RegWrite(X86.UC_X86_REG_EDI, DllData); // opaque fixture instance token, no host pointer
            emulator.RegWrite(X86.UC_X86_REG_ECX, plan.Image.Header.HeapBytes);
            emulator.RegWrite(X86.UC_X86_REG_EBX, 0);
            emulator.RegWrite(X86.UC_X86_REG_EDX, 0);
            emulator.RegWrite(X86.UC_X86_REG_EAX, Marker);
            emulator.RegWrite(X86.UC_X86_REG_EFLAGS, 2); // reserved bit 1; DF=0; no guest interrupt delivery
            output.WriteLine("2. MACHINE READY (all addresses belong to the guest)");
            output.WriteLine($"   GDTR base=0x{GdtBase:X5}; CR0.PE=1; 16-bit descriptor defaults");
            output.WriteLine($"   CS={CallerCode:X4} DS={DllData:X4} SS:SP={Stack:X4}:{InitialSp:X4} BP=0000");
            output.WriteLine($"   DI={DllData:X4} instance token; CX={plan.Image.Header.HeapBytes} heap bytes; ES:SI=0000:0000");

            // 3. OBSERVE EXECUTION. A hook sees instructions BEFORE execution.
            // Only gateway/budget exits stop the engine. Actual API dispatch is
            // below, after EmuStart has unwound back into managed code.
            var calls = new List<HostCall>();
            var visits = new List<EntryVisit>();
            int instructions = 0;
            long? trapped = null;
            string? hookError = null;
            string phase = "startup";
            TimeSpan executionTime = TimeSpan.Zero; // count engine time, not pauses while inspecting C# between runs
            CodeHook hook = (engine, address, size, userData) =>
            {
                try
                {
                    if (++instructions > instructionLimit)
                    {
                        hookError = $"Instruction budget ({instructionLimit}) exhausted during {phase}.";
                        engine.EmuStop();
                        return;
                    }
                    Registers registers = Snapshot();
                    var pointer = new FarPointer16(registers.Cs, registers.Ip);
                    if (pointer == startup || pointer == hello || pointer == wep)
                        visits.Add(new(phase, pointer, address, registers,
                            ReadWord(StackBase + registers.Sp), ReadWord(StackBase + registers.Sp + 2)));
                    if (traceInstructions)
                    {
                        // UD2 is deliberately invalid: Unicorn may report an
                        // unknown instruction size at that guard. It is a host
                        // exit, so do not pretend it is a decoded guest instruction.
                        string bytesText = "<host gateway>";
                        if (registers.Cs != Gateway)
                        {
                            if (size is < 1 or > 15) throw new InvalidOperationException("Invalid x86 instruction size.");
                            byte[] bytes = new byte[(int)size];
                            engine.MemRead(address, bytes);
                            bytesText = Convert.ToHexString(bytes);
                        }
                        output.WriteLine($"   trace {phase,-7} {pointer} linear={address:X5} " +
                            $"bytes={bytesText,-16} AX={registers.Ax:X4} DS={registers.Ds:X4} SP={registers.Sp:X4}");
                    }
                    if (address >= GatewayBase && address < GatewayBase + 0x1000)
                    {
                        trapped = address;
                        engine.EmuStop();
                    }
                }
                catch (Exception error)
                {
                    // Never allow a managed exception to escape a native callback.
                    hookError = $"Instruction observation failed: {error.Message}";
                    engine.EmuStop();
                }
            };
            emulator.AddCodeHook(hook, 1, 0); // all code; begin > end is Unicorn's entire-address-space convention

            // 4. CALL THE HEADER STARTUP, not LibMain's guessed file offset.
            output.WriteLine("3. EXECUTE DLL STARTUP: Unicorn fetches the caller's CALL FAR and follows it");
            Registers init = RunPhase("startup", 0x000, 0x010);
            ushort storedInit = ReadWord(CallerDataBase + InitResult);
            if (init.Ax == 0)
            {
                output.WriteLine("   Startup returned FALSE. HELLOWORLD and WEP are not called.");
                return Observations(false, init, null, null);
            }
            if (storedInit != init.Ax) throw new InvalidOperationException("Startup's guest store disagrees with AX.");

            // 5. CALL AN EXPORT FROM ANOTHER DS. This is intentional: if the
            // loader forgot the export-prologue fixup, the stack checker would
            // read our caller data rather than the DLL's runtime data.
            output.WriteLine("4. EXECUTE HELLOWORLD: caller DS differs from DLL DS; patched prologue bridges them");
            emulator.RegWrite(X86.UC_X86_REG_DS, CallerData);
            emulator.RegWrite(X86.UC_X86_REG_ES, CallerData);
            emulator.RegWrite(X86.UC_X86_REG_AX, Marker); // poison: no host write of 42 anywhere
            Registers helloReturn = RunPhase("hello", 0x100, 0x109);
            if (helloReturn.Ds != CallerData) throw new InvalidOperationException("HELLOWORLD did not restore caller DS.");

            // 6. WEP takes a word argument. Its compiled RETF 2 must remove it.
            output.WriteLine("5. EXECUTE WEP(1): compiled RETF 2 removes its argument");
            Registers exitReturn = RunPhase("wep", 0x200, 0x20C);
            return Observations(true, init, helloReturn, exitReturn);

            Result Observations(bool initialized, Registers initReturn, Registers? helloResult, Registers? exitResult) =>
                new(initialized, plan, initReturn, helloResult, exitResult,
                    ReadWord(CallerDataBase + InitResult), ReadWord(CallerDataBase + HelloResult),
                    ReadWord(CallerDataBase + ExitResult), calls.AsReadOnly(), visits.AsReadOnly(), instructions,
                    (emulator.RegRead(X86.UC_X86_REG_CR0) & 1) != 0, ReadDllData());

            byte[] ReadDllData()
            {
                byte[] observed = new byte[data.Bytes.Length];
                emulator.MemRead(data.Placement.LinearBase, observed);
                return observed;
            }

            Registers RunPhase(string name, ushort begin, ushort end)
            {
                phase = name;
                long resume = begin;
                while (true)
                {
                    if (calls.Count >= 16 || executionTime > TimeSpan.FromSeconds(5))
                        throw new InvalidOperationException("DLL execution exceeded its host-call/time budget.");
                    trapped = null;
                    // Crucial API distinction: begin is EIP OFFSET in current CS;
                    // until is a LINEAR address. Unicorn performs fetch/decode/execute.
                    long started = Stopwatch.GetTimestamp();
                    try
                    {
                        emulator.EmuStart(resume, CallerBase + end, timeout: 1_000_000, count: instructionLimit);
                    }
                    finally { executionTime += Stopwatch.GetElapsedTime(started); }
                    if (hookError is not null) throw new InvalidOperationException(hookError);
                    if (trapped is not null)
                    {
                        DispatchHostCall();
                        resume = emulator.RegRead(X86.UC_X86_REG_EIP);
                        continue;
                    }
                    Registers final = Snapshot();
                    if (instructions >= instructionLimit && (final.Cs != CallerCode || final.Ip != end))
                        throw new InvalidOperationException($"Instruction budget ({instructionLimit}) exhausted during {name}.");
                    // Reaching an instruction/time limit or HLT is NOT success.
                    if (final.Cs != CallerCode || final.Ip != end || final.Sp != InitialSp ||
                        final.Ss != Stack || final.Bp != 0)
                        throw new InvalidOperationException($"{name} stopped without a balanced return: {final}.");
                    output.WriteLine($"   Returned CS:IP={final.Cs:X4}:{final.Ip:X4} AX={final.Ax} " +
                        $"DS={final.Ds:X4} SS:SP={final.Ss:X4}:{final.Sp:X4}; guest stored AX via ES");
                    return final;
                }
            }

            void DispatchHostCall()
            {
                Registers before = Snapshot();
                NeImportBinding binding = Bindings.SingleOrDefault(b =>
                    b.Address == new FarPointer16(before.Cs, before.Ip))
                    ?? throw new NotSupportedException($"Unknown gateway {before.Cs:X4}:{before.Ip:X4}.");
                if (binding.Handler == "FailIfReached")
                    throw new NotSupportedException($"{binding.Name} reached: compiler runtime error path; no dialog is mocked.");
                if (trapped != GatewayBase + before.Ip || before.Ss != Stack ||
                    before.Sp + 4 + binding.ArgumentBytes > InitialSp)
                    throw new InvalidOperationException($"{binding.Name}: invalid stack or gateway address.");
                byte[] frameBytes = new byte[4 + binding.ArgumentBytes];
                emulator.MemRead(StackBase + before.Sp, frameBytes);
                var frame = new FarPascalWordFrame(frameBytes);
                var arguments = new ushort[frame.ArgumentCount];
                for (int i = 0; i < arguments.Length; i++)
                    arguments[i] = unchecked((ushort)frame.ReadArgument(i)); // these API words are UNSIGNED
                var returnAddress = new FarPointer16(frame.ReturnCs, frame.ReturnIp);
                PreparedNeSegment returnSegment = plan.Segments.SingleOrDefault(s =>
                    s.Placement.Selector == returnAddress.Selector && !s.Source.IsData)
                    ?? throw new InvalidOperationException("Import return CS is not DLL code.");
                if (frame.ReturnIp >= returnSegment.Source.FileBytes)
                    throw new InvalidOperationException("Import return IP is outside stored code.");
                uint returned;
                if (binding.Handler == "CheckedHeapTestDouble")
                {
                    // This fixture never calls LocalAlloc/LocalFree. Validate the
                    // exact request and reservation, then report success/failure.
                    // We DO NOT build a Windows heap, manufacture heap handles, or
                    // claim that returning TRUE implements LocalInit generally.
                    if (arguments.Length != 3 || arguments[0] != DllData || arguments[1] != 0 ||
                        arguments[2] != plan.Image.Header.HeapBytes ||
                        data.Bytes.Length - Math.Max(data.Source.FileBytes, data.Source.MinimumAllocationBytes) != arguments[2])
                        throw new InvalidOperationException("LocalInit test double received an unsupported heap request.");
                    returned = localInitSucceeds ? 1u : 0u;
                }
                else if (binding.Handler == "FixedWindowsVersion") returned = version;
                else throw new NotSupportedException($"No handler for {binding.Name}.");

                output.WriteLine($"   Host exit: {binding.Name}({string.Join(", ", arguments.Select(a => $"0x{a:X4}"))}) " +
                    $"-> 0x{returned:X8}; resume {returnAddress}; Pascal cleanup {binding.ArgumentBytes} bytes");
                emulator.RegWrite(X86.UC_X86_REG_AX, (ushort)returned);
                if (binding.ReturnLayout == Win16ReturnLayout.DwordInDxAx)
                    emulator.RegWrite(X86.UC_X86_REG_DX, (ushort)(returned >> 16));
                // The hook did not return to the guest. This explicitly simulates
                // RETF + argument cleanup, exactly as explained in tutorial 04.
                emulator.RegWrite(X86.UC_X86_REG_CS, frame.ReturnCs);
                emulator.RegWrite(X86.UC_X86_REG_EIP, frame.ReturnIp);
                emulator.RegWrite(X86.UC_X86_REG_SP, frame.StackPointerAfterReturn(before.Sp));
                calls.Add(new(binding, Array.AsReadOnly(arguments), returned, before, Snapshot(), returnAddress));
            }

            Registers Snapshot() => new(Word(X86.UC_X86_REG_CS), Word(X86.UC_X86_REG_EIP),
                Word(X86.UC_X86_REG_AX), Word(X86.UC_X86_REG_DX), Word(X86.UC_X86_REG_DS),
                Word(X86.UC_X86_REG_ES), Word(X86.UC_X86_REG_SS), Word(X86.UC_X86_REG_SP),
                Word(X86.UC_X86_REG_BP), Word(X86.UC_X86_REG_CX), Word(X86.UC_X86_REG_DI), Word(X86.UC_X86_REG_SI));
            ushort Word(int register) => checked((ushort)emulator.RegRead(register));
            ushort ReadWord(long address)
            {
                byte[] bytes = new byte[2];
                emulator.MemRead(address, bytes);
                return BinaryPrimitives.ReadUInt16LittleEndian(bytes);
            }
            void MapHostSegment(ushort selector, uint address, bool code)
            {
                emulator.MemMap(address, 0x1000, code ? Common.UC_PROT_READ | Common.UC_PROT_EXEC
                    : Common.UC_PROT_READ | Common.UC_PROT_WRITE);
                SegmentDescriptor16.Encode(address, 0x0FFF, code).CopyTo(gdt, selector);
            }
            void WriteCaller(ushort offset, FarPointer16 target, ushort resultOffset,
                bool commandLineIsNull = false, bool pushExitReason = false)
            {
                var code = new List<byte>();
                if (pushExitReason) code.AddRange([0x68, 0x01, 0x00]); // PUSH word 1
                code.AddRange([0x9A, (byte)target.Offset, (byte)(target.Offset >> 8),
                    (byte)target.Selector, (byte)(target.Selector >> 8)]); // CALL FAR ptr16:16
                if (commandLineIsNull)
                {
                    // ES was NULL for startup's command-line pointer. Change ES
                    // only AFTER returning; preserve the function result in AX.
                    code.AddRange([0x50, 0xB8, (byte)CallerData, 0x00, 0x8E, 0xC0, 0x58]);
                    // PUSH AX; MOV AX,CallerData; MOV ES,AX; POP AX (7 bytes)
                }
                code.AddRange([0x26, 0xA3, (byte)resultOffset, (byte)(resultOffset >> 8)]); // MOV ES:[result],AX
                emulator.MemWrite(CallerBase + offset, code.ToArray());
            }
        }
        finally { emulator.Close(); }

        FarPointer16 Export(string name) => plan.ResolveCode(plan.Image.FindExport(name)?.Address
            ?? throw new InvalidDataException($"Missing code export {name}."));
    }

    public sealed record Registers(ushort Cs, ushort Ip, ushort Ax, ushort Dx, ushort Ds, ushort Es,
        ushort Ss, ushort Sp, ushort Bp, ushort Cx, ushort Di, ushort Si);
    public sealed record EntryVisit(string Phase, FarPointer16 Address, long LinearAddress,
        Registers Registers, ushort ReturnIp, ushort ReturnCs);
    public sealed record HostCall(NeImportBinding Binding, IReadOnlyList<ushort> Arguments,
        uint Returned, Registers Before, Registers After, FarPointer16 ReturnAddress);
    public sealed record Result(bool Initialized, NeLoadPlan Plan, Registers Initialization,
        Registers? Hello, Registers? Exit, ushort StoredInitialization, ushort StoredHello, ushort StoredExit,
        IReadOnlyList<HostCall> Calls, IReadOnlyList<EntryVisit> Entries, int InstructionCount, bool ProtectedMode,
        byte[] DllDataAfterExecution);

    private static string FindFixture()
    {
        // F5 works from the project directory or bin directory, without a personal
        // absolute path in launchSettings. Build the fixture once with the script.
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            string candidate = Path.Combine(directory.FullName, "artifacts", "win16", "hello42", "hello42.dll");
            if (File.Exists(candidate)) return candidate;
        }
        throw new FileNotFoundException("Build Hello42 first: ./tools/build-win16-fixture.ps1 (see docs/tutorial-06-load-library.md).");
    }
}
