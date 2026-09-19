using System.Buffers.Binary;
using System.Security.Cryptography;
using AfterDarker.Core.AfterDark;
using AfterDarker.Core.Ne;
using AfterDarker.Core.Win16;
using AfterDarker.Tutorials.Runtime;
using UnicornEngine.Const;

namespace AfterDarker.Tutorials.Lessons;

/// <summary>
/// Read Execute from top to bottom: prepare bytes, map them, supply real guest
/// records, set registers, run startup, then call MODULE twice. The companion
/// guide explains the boundaries; supporting code never implements Mondrian's
/// initialization algorithm. Its own x86 instructions read our inputs and write
/// the state that we observe at the end.
/// </summary>
public sealed class Tutorial08MondrianInitialize(string? path = null, bool trace = false,
    TextReader? input = null, TextWriter? output = null) : ITutorial
{
    public string Id => "08";
    public string Title => "Load original Mondrian: DLL startup -> PREINITIALIZE -> INITIALIZE";

    // S1..S5 occupy selectors 0008..0028 and linear slots 10000..50000.
    // These host-owned slots follow them. The stack does not come from the DLL.
    public const ushort Caller = 0x30, Gateway = 0x38, Stack = 0x40, HostData = 0x48;
    public const uint CallerBase = 0x60000, GatewayBase = 0x70000, StackBase = 0x80000, HostDataBase = 0x90000;
    public const ushort InitialSp = 0x1000, SystemOffset = 0x100, ModuleOffset = 0x200, EnvironmentOffset = 0x300;
    private const ushort StartupResult = 0x20, PreinitializeResult = 0x22, InitializeResult = 0x24;
    private const ushort StartupCaller = 0, PreinitializeCaller = 0x100, InitializeCaller = 0x200;
    private const int WordBytes = sizeof(ushort), FarReturnBytes = 2 * WordBytes;
    public static readonly DateTime CivilTime = new(1993, 6, 15, 12, 34, 56, DateTimeKind.Unspecified);

    public void Run()
    {
        TextWriter writer = output ?? Console.Out;
        string? filePath = path;
        if (filePath is null)
        {
            writer.Write("Path to your local Mondrian.ad (the analyzed version): ");
            filePath = (input ?? Console.In).ReadLine()?.Trim().Trim('"');
        }
        if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("A local Mondrian path is required.");
        using FileStream stream = File.OpenRead(filePath);
        if (stream.Length > NeReader.MaximumFileBytes) throw new InvalidDataException("Module exceeds the NE input limit.");
        byte[] file = new byte[(int)stream.Length];
        stream.ReadExactly(file);
        Execute(file, writer, trace);
        writer.WriteLine("PASS: original Mondrian startup, PREINITIALIZE, and INITIALIZE returned with verified guest state.");
        writer.WriteLine("Stops before BLANK/DRAWFRAME. No pixels, settings dialogs, or general Windows heap implementation.");
    }

    public sealed record HostCall(string Phase, Win16Imports.ImportEntry Binding, IReadOnlyList<ushort> Arguments,
        uint Returned, SegmentedGuest.CpuState Before, SegmentedGuest.CpuState After);
    public sealed record PhaseResult(string Name, SegmentedGuest.CpuState Registers, ushort StoredAx,
        MondrianInitialization.State State);
    public sealed record Result(NeLoadPlan Plan, MondrianInitialization.State BeforeExecution,
        IReadOnlyList<PhaseResult> Phases, IReadOnlyList<HostCall> Calls,
        IReadOnlyList<SegmentedGuest.InterruptVisit> Interrupts, LocalHeapReservation Heap,
        int OutstandingLocks, int Instructions, bool ProtectedMode);

    public static Result Execute(byte[] file, TextWriter? output = null, bool trace = false,
        MondrianInitialization.Options? options = null, int instructionLimit = 50_000)
    {
        output ??= TextWriter.Null;
        options ??= new();
        options.Validate();
        // Host-record offsets and state assertions below are specific to this
        // analyzed binary. Refuse other revisions before executing anything.
        string hash = Convert.ToHexString(SHA256.HashData(file));
        if (hash != MondrianInitialization.Sha256)
            throw new NotSupportedException("Tutorial 08 requires the analyzed Mondrian SHA-256 " + MondrianInitialization.Sha256 + ".");
        NeImage image = NeReader.Read(file);
        var bindings = Win16Imports.BindImports(image, Gateway);
        var byImport = bindings.ToDictionary(b => b.Import);
        NeLoadPlan plan = NeLoadPlan.CreateWithImportResolver(file, NeLoadPlan.PlaceSegments(image),
            import => byImport[import].Address);
        PreparedNeSegment data = plan.Segments.Single(s => s.Source.Number == image.Header.AutomaticDataSegment);
        ushort dllData = data.Placement.Selector;
        FarPointer16 startup = plan.ResolveCode(image.Header.Startup!.Value);
        FarPointer16 module = plan.ResolveCode(image.FindExport("MODULE")!.Address!.Value);
        var beforeExecution = MondrianInitialization.Observe(data.Bytes);
        output.WriteLine($"1. PREPARE {hash}: {plan.Segments.Count} segments; {plan.Patches.Count} patches; startup {startup}; MODULE {module}");

        using var guest = new SegmentedGuest(output, trace, instructionLimit);
        foreach (PreparedNeSegment segment in plan.Segments)
        {
            // MemMap creates storage; MemWrite copies ALREADY relocated machine
            // code/data. Unicorn later fetches those bytes without any C# opcode enum.
            guest.Map(segment.Placement.Selector, segment.Placement.LinearBase, segment.Bytes, !segment.Source.IsData);
            output.WriteLine($"   S{segment.Source.Number} -> {segment.Placement.Selector:X4}:0000 at linear {segment.Placement.LinearBase:X5}, {segment.Bytes.Length} bytes");
        }

        // Allocate records before publishing handles. Neither 0101 nor 0102 is
        // a usable address: GlobalLock translates them into HostData:offset.
        var records = MondrianInitialization.CreateRecords(options);
        byte[] hostBytes = new byte[SegmentedGuest.PageBytes];
        records.System.CopyTo(hostBytes, SystemOffset);
        records.Module.CopyTo(hostBytes, ModuleOffset);
        // EnvironmentOffset already holds two NUL bytes: a valid empty string list.
        // No TZ selects the guest runtime's built-in timezone, not the host's.
        for (int i = StartupResult; i <= InitializeResult + 1; i++) hostBytes[i] = 0xCC;
        guest.Map(HostData, HostDataBase, hostBytes, code: false);
        guest.Map(Stack, StackBase, new byte[SegmentedGuest.PageBytes], code: false);

        byte[] gatewayBytes = new byte[SegmentedGuest.PageBytes];
        foreach (var binding in bindings)
        {
            gatewayBytes[binding.Address.Offset] = 0x0F;
            gatewayBytes[binding.Address.Offset + 1] = 0x0B; // UD2; never execute the synthetic address.
            output.WriteLine($"   {binding.Name} -> {binding.Address}: {binding.Implementation}");
        }
        guest.Map(Gateway, GatewayBase, gatewayBytes, code: true);
        int heapStart = Math.Max(data.Source.FileBytes, data.Source.MinimumAllocationBytes);
        if (data.Bytes.Length != heapStart + image.Header.HeapBytes)
            throw new InvalidOperationException("Prepared data does not include the declared heap tail.");
        var services = new Win16Api(new Win16ApiState(guest,
            new(new(dllData, checked((ushort)heapStart)), image.Header.HeapBytes), new(HostData, EnvironmentOffset)));
        services.State.Blocks.Register(MondrianInitialization.SystemHandle, new(HostData, SystemOffset), records.System.Length);
        services.State.Blocks.Register(MondrianInitialization.ModuleHandle, new(HostData, ModuleOffset), records.Module.Length);
        output.WriteLine($"2. HOST RECORDS: handle 0101 -> {HostData:X4}:{SystemOffset:X4}; handle 0102 -> {HostData:X4}:{ModuleOffset:X4}");
        output.WriteLine($"   Options {options.Width}x{options.Height}, speed={options.Speed}, clear={options.Clear}; empty environment {HostData:X4}:{EnvironmentOffset:X4}");

        // Each caller executes a real CALL FAR and stores the returned AX itself.
        // MODULE's Pascal arguments are pushed left-to-right; RETF 6 removes them.
        byte[] callerBytes = new byte[SegmentedGuest.PageBytes];
        ushort startupEnd = WriteCaller(StartupCaller, startup, StartupResult, null);
        ushort preinitializeEnd = WriteCaller(PreinitializeCaller, module, PreinitializeResult, MondrianInitialization.PreinitializeMessage);
        ushort initializeEnd = WriteCaller(InitializeCaller, module, InitializeResult, MondrianInitialization.InitializeMessage);
        guest.Map(Caller, CallerBase, callerBytes, code: true);
        guest.Install(Gateway);

        // DLL startup's compiler ABI: DS = automatic data, DI = instance token,
        // CX = heap request, ES:SI = optional command line (null for this DLL).
        // SS:SP belongs to our caller; BP=0 terminates the initial frame chain.
        guest.Set(X86.UC_X86_REG_DS, dllData);
        guest.Set(X86.UC_X86_REG_ES, 0);
        guest.Set(X86.UC_X86_REG_SI, 0);
        guest.Set(X86.UC_X86_REG_DI, dllData);
        guest.Set(X86.UC_X86_REG_CX, image.Header.HeapBytes);
        guest.Set(X86.UC_X86_REG_SS, Stack);
        guest.Set(X86.UC_X86_REG_SP, InitialSp);
        guest.Set(X86.UC_X86_REG_BP, 0);
        output.WriteLine($"3. STARTUP INPUTS: DS={dllData:X4}, DI={dllData:X4}, CX={image.Header.HeapBytes}, ES:SI=0000:0000, SS:SP={Stack:X4}:{InitialSp:X4}, BP=0");

        var calls = new List<HostCall>();
        var phases = new List<PhaseResult>();
        guest.DispatchGateway = DispatchHostCall;
        guest.DispatchInterrupt = number =>
        {
            if (number != DosClock.InterruptNumber) throw new NotSupportedException($"Unsupported INT {number:X2}h during {guest.Phase}.");
            ushort ax = (ushort)guest.Get(X86.UC_X86_REG_AX);
            var reply = DosClock.Respond((byte)(ax >> 8), ax, CivilTime);
            guest.Set(X86.UC_X86_REG_AX, reply.Ax);
            guest.Set(X86.UC_X86_REG_CX, reply.Cx);
            guest.Set(X86.UC_X86_REG_DX, reply.Dx);
            output.WriteLine($"   {guest.Phase}: INT 21h AH={ax >> 8:X2} -> AX={reply.Ax:X4} CX={reply.Cx:X4} DX={reply.Dx:X4}; resume after INT (no RETF/IRET)");
        };

        PhaseResult initializedDll = RunPhase("DLL startup", StartupCaller, startupEnd, StartupResult, dllData);
        if (initializedDll.StoredAx == 0 || initializedDll.State.InstanceHandle != dllData || services.State.InitializedHeap is null)
            throw new InvalidOperationException("DLL startup did not succeed and save its instance handle; MODULE will not run.");

        // Call MODULE from a different DS, exercising the export-prologue patch.
        // HDC is a reserved nonzero identity only: initialization does not draw.
        // Any attempt to use it reaches an unsupported drawing gateway and stops.
        guest.Set(X86.UC_X86_REG_DS, HostData);
        PhaseResult preinitialized = RunPhase("PREINITIALIZE", PreinitializeCaller, preinitializeEnd, PreinitializeResult, HostData);
        if (preinitialized.State.Compatibility != 1)
            throw new InvalidOperationException("Mondrian rejected the supplied compatibility fields; INITIALIZE will not run.");
        // PREINITIALIZE's AX is incidental in this binary; the compatibility
        // variable above is the meaningful proof, not an invented success code.
        PhaseResult initialized = RunPhase("INITIALIZE", InitializeCaller, initializeEnd, InitializeResult, HostData);
        MondrianInitialization.State state = initialized.State;
        if (state.Clear != (options.Clear ? 1 : 0) || state.Threshold != options.ExpectedThreshold || state.Counter != 0 ||
            state.Rectangles != 0 || state.Tick != Win16ApiState.DefaultInitialTick || state.Seed != (ushort)state.Time ||
            state.System != new FarPointer16(HostData, SystemOffset) || state.Module != new FarPointer16(HostData, ModuleOffset) ||
            guest.Interrupts.Count != 3 || services.State.Blocks.OutstandingLocks != 0)
            throw new InvalidOperationException("Original initialization returned but guest state disagrees with the supplied host contract.");
        output.WriteLine($"4. GUEST STATE: compatible={state.Compatibility}, threshold={state.Threshold}, clear={state.Clear}, rectangles={state.Rectangles}");
        output.WriteLine($"   Guest time={state.Time:X8}, RNG seed={state.Seed:X8}, tick={state.Tick:X8}; all global locks released");
        return new(plan, beforeExecution, phases.AsReadOnly(), calls.AsReadOnly(), guest.Interrupts.AsReadOnly(),
            services.State.InitializedHeap, services.State.Blocks.OutstandingLocks, guest.Instructions, (guest.Get(X86.UC_X86_REG_CR0) & 1) != 0);

        PhaseResult RunPhase(string name, ushort begin, ushort end, ushort resultOffset, ushort expectedDs)
        {
            var returned = guest.RunUntil(name, new(Caller, begin), new(Caller, end));
            ushort stored = BinaryPrimitives.ReadUInt16LittleEndian(guest.Read(new(HostData, resultOffset), 2));
            if (returned.Ss != Stack || returned.Sp != InitialSp || returned.Bp != 0 || returned.Ds != expectedDs || stored != returned.Ax)
                throw new InvalidOperationException($"{name}: unbalanced stack, changed caller DS, or mismatched guest store: {returned}.");
            if (services.State.Blocks.OutstandingLocks != 0) throw new InvalidOperationException($"{name}: leaked global locks.");
            var observation = new PhaseResult(name, returned, stored,
                MondrianInitialization.Observe(guest.Read(new(dllData, 0), data.Bytes.Length)));
            phases.Add(observation);
            output.WriteLine($"   {name} returned to {returned.Pc}; AX={stored:X4} (guest stored); DS={returned.Ds:X4}; SS:SP={returned.Ss:X4}:{returned.Sp:X4}");
            return observation;
        }

        void DispatchHostCall()
        {
            HostCall call = DispatchImport(guest, bindings, services);
            calls.Add(call);
            output.WriteLine($"   {call.Phase}: {call.Binding.Name}({string.Join(",", call.Arguments.Select(a => a.ToString("X4")))}) -> {call.Returned:X8}; RETF {call.Binding.ArgumentBytes}, resume {call.After.Pc}");
        }

        ushort WriteCaller(ushort begin, FarPointer16 target, ushort resultOffset, ushort? message)
        {
            var code = new List<byte>();
            if (message is ushort value)
            {
                Push(value); Push(MondrianInitialization.ReservedHdc); Push(MondrianInitialization.SystemHandle);
            }
            code.Add(0x9A); Word(target.Offset); Word(target.Selector); // CALL FAR immediate
            code.AddRange([0x50, 0xB8]); Word(HostData); // PUSH AX; MOV AX,HostData
            code.AddRange([0x8E, 0xC0, 0x58]); // MOV ES,AX; POP AX (preserve returned value)
            code.AddRange([0x26, 0xA3]); Word(resultOffset); // MOV ES:[resultOffset],AX
            code.CopyTo(callerBytes, begin);
            return checked((ushort)(begin + code.Count));
            void Push(ushort value) { code.Add(0x68); Word(value); }
            void Word(ushort value) { code.Add((byte)value); code.Add((byte)(value >> 8)); }
        }
    }

    // Public so original tiny guest programs can test the SAME argument/return
    // path without requiring anyone else's copyrighted DLL. Kept in the lesson
    // so the learner can step directly from a trapped call into this mechanism.
    public static HostCall DispatchImport(SegmentedGuest guest,
        IReadOnlyList<Win16Imports.ImportEntry> bindings, Win16Api services)
    {
        var before = guest.Snapshot();
        var binding = bindings.SingleOrDefault(b => b.Address == before.Pc)
            ?? throw new NotSupportedException($"Unknown import gateway {before.Pc} during {guest.Phase}.");
        if (binding.Implementation == Win16Imports.Handler.Unsupported)
            throw new NotSupportedException($"{guest.Phase}: {binding.Name} reached at {before.Pc}; initialization-only lesson stops here.");
        if (before.Ss != Stack || before.Sp + FarReturnBytes + binding.ArgumentBytes!.Value > InitialSp)
            throw new InvalidOperationException($"Invalid far Pascal frame for {binding.Name}.");
        var frame = new FarPascalWordFrame(guest.Read(new(Stack, before.Sp), FarReturnBytes + binding.ArgumentBytes.Value));
        var arguments = new ushort[frame.ArgumentCount];
        for (int i = 0; i < arguments.Length; i++) arguments[i] = unchecked((ushort)frame.ReadArgument(i));
        var returnAddress = new FarPointer16(frame.ReturnCs, frame.ReturnIp);
        guest.RequireCode(returnAddress, 1);
        Win16Imports.Reply reply;
        try { reply = Win16Imports.Invoke(services, binding, arguments); }
        catch (Exception error) { throw new InvalidOperationException($"{guest.Phase}: {binding.Name} at {before.Pc}: {error.Message}", error); }
        guest.Set(X86.UC_X86_REG_AX, (ushort)reply.Value);
        if (binding.ReturnLayout == Win16ReturnLayout.DwordInDxAx) guest.Set(X86.UC_X86_REG_DX, (ushort)(reply.Value >> 16));
        if (reply.Cx is ushort cx) guest.Set(X86.UC_X86_REG_CX, cx);
        // Unlike INT, CALL FAR pushed IP/CS. Simulate RETF + Pascal cleanup
        // here, outside the hook, before the execution loop starts again.
        guest.Set(X86.UC_X86_REG_CS, frame.ReturnCs);
        guest.Set(X86.UC_X86_REG_EIP, frame.ReturnIp);
        guest.Set(X86.UC_X86_REG_SP, frame.StackPointerAfterReturn(before.Sp));
        return new(guest.Phase, binding, Array.AsReadOnly(arguments), reply.Value, before, guest.Snapshot());
    }
}
