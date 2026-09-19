using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using AfterDarker.Core.AfterDark;
using AfterDarker.Core.Ne;
using AfterDarker.Core.Win16;
using AfterDarker.Core.Rendering;
using UnicornEngine.Const;

namespace AfterDarker.Runtime;

/// <summary>
/// One loaded After Dark guest and its optional drawing surface. Hosts explicitly
/// initialize, blank, draw, inspect, shut down and dispose it. No callback keeps it alive.
/// Use from one owner sequentially; this class does not schedule or synchronize
/// callers. The same execution path serves tutorials and the WPF host.
/// </summary>
public class AfterDarkSession<TState> : IAnimationSession
{
    // Host slots follow ALL loaded NE segments. A sixth DLL segment must not
    // collide with the caller slot used by the five-segment Mondrian tutorial.
    public ushort Caller { get; }
    public ushort Gateway { get; }
    public ushort Stack { get; }
    public ushort HostData { get; }
    public uint CallerBase => (uint)(Caller / 8) << 16;
    public uint GatewayBase => (uint)(Gateway / 8) << 16;
    public uint StackBase => (uint)(Stack / 8) << 16;
    public uint HostDataBase => (uint)(HostData / 8) << 16;
    public const ushort InitialSp = 0x1000, SystemOffset = 0x100, ModuleOffset = 0x200, EnvironmentOffset = 0x300;
    private const ushort StartupResult = 0x20, PreinitializeResult = 0x22, InitializeResult = 0x24;
    private const ushort StartupCaller = 0, PreinitializeCaller = 0x100, InitializeCaller = 0x200;
    private const int WordBytes = sizeof(ushort), FarReturnBytes = 2 * WordBytes;
    public static readonly DateTime CivilTime = new(1993, 6, 15, 12, 34, 56, DateTimeKind.Unspecified);

    public const ushort BlankMessage = 1, DrawFrameMessage = 2, CloseMessage = 3;
    private const ushort BlankCaller = 0x300, DrawCaller = 0x400, DrawingResult = 0x26;
    private const ushort CloseCaller = 0x500, WepCaller = 0x600, CloseResult = 0x28, WepResult = 0x2A;
    private const ushort WepSystemExit = 1;

    public sealed record HostCall(string Phase, Win16Imports.ImportEntry Binding, IReadOnlyList<ushort> Arguments,
        uint Returned, SegmentedGuest.CpuState Before, SegmentedGuest.CpuState After);
    public sealed record PhaseResult(string Name, SegmentedGuest.CpuState Registers, ushort StoredAx,
        TState State);
    public sealed record Result(NeLoadPlan Plan, TState BeforeExecution,
        IReadOnlyList<PhaseResult> Phases, IReadOnlyList<HostCall> Calls,
        IReadOnlyList<SegmentedGuest.InterruptVisit> Interrupts, LocalHeapReservation Heap,
        int OutstandingLocks, long Instructions, bool ProtectedMode, DiagnosticSummary Diagnostics);
    public sealed record DiagnosticSummary(int? HistoryCapacity, long TotalCalls, long TotalPhases,
        long TotalInterrupts, IReadOnlyDictionary<string, long> ImportCalls);

    private readonly SegmentedGuest guest;
    private readonly TextWriter output;
    private readonly PlaybackOptions options;
    private readonly ModuleProfile<TState> profile;
    private readonly NeLoadPlan plan;
    private readonly PreparedNeSegment data;
    private readonly ushort dllData, startupEnd, preinitializeEnd, initializeEnd, blankEnd, drawEnd, closeEnd, wepEnd;
    private readonly IReadOnlyList<Win16Imports.ImportEntry> bindings;
    private readonly TState beforeExecution;
    private readonly Win16Api services;
    private readonly SessionTiming timing;
    private readonly PixelSurface? surface;
    private readonly Win16Drawing? drawing;
    private readonly DiagnosticOptions diagnostics;
    private readonly DiagnosticHistory<HostCall> calls;
    private readonly DiagnosticHistory<PhaseResult> phases;
    // Keys come only from the fixed import registry; this cannot grow per frame.
    private readonly Dictionary<string, long> importCalls = [];

    public string ModuleName => profile.Name;
    public int LivePenCount => drawing?.LivePenCount ?? 0;
    public int PeakPenCount => drawing?.PeakPenCount ?? 0;
    void IAnimationSession.Initialize() => Initialize();
    void IAnimationSession.Blank() => Blank();
    void IAnimationSession.DrawFrame() => DrawFrame();
    public PlaybackResult GetPlaybackResult()
    {
        var result = GetResult();
        return new(ModuleName, result.Phases.Select(p => new PlaybackPhase(p.Name, p.StoredAx, p.Registers)).ToArray(),
            result.Instructions, result.OutstandingLocks, result.Calls.Count, LivePenCount, PeakPenCount, result.Diagnostics.ImportCalls);
    }

    public enum SessionState { Loaded, Initialized, Ready, Closed, Faulted, Disposed }
    public SessionState State { get; private set; } = SessionState.Loaded;

    /// <summary>Prepare/map a module without executing it. The caller owns this session with using.</summary>
    public AfterDarkSession(byte[] file, ModuleProfile<TState> profile, PlaybackOptions? options = null,
        bool enableDrawing = true, TextWriter? output = null, bool trace = false, int instructionLimit = 50_000,
        DiagnosticOptions? diagnostics = null, SessionTiming? timing = null)
    {
        this.profile = profile;
        this.diagnostics = diagnostics ?? DiagnosticOptions.Recent;
        this.timing = timing ?? SessionTiming.Deterministic();
        calls = new(this.diagnostics);
        phases = new(this.diagnostics);
        this.output = output ?? TextWriter.Null;
        output = this.output;
        this.options = options ?? new();
        options = this.options;
        profile.ValidateOptions(options);
        // Host-record offsets and state assertions below are specific to this
        // analyzed binary. Refuse other revisions before executing anything.
        string hash = Convert.ToHexString(SHA256.HashData(file));
        if (hash != profile.Sha256)
            throw new NotSupportedException($"{profile.Name} execution requires SHA-256 {profile.Sha256}" + ".");
        if (enableDrawing)
        {
            surface = new PixelSurface(options.Width, options.Height);
            drawing = new Win16Drawing();
            drawing.Register(AfterDarkHostContract.ReservedHdc, surface);
        }
        NeImage image = NeReader.Read(file);
        Caller = checked((ushort)((image.Segments.Count + 1) * 8));
        Gateway = (ushort)(Caller + 8); Stack = (ushort)(Caller + 16); HostData = (ushort)(Caller + 24);
        bindings = Win16Imports.BindImports(image, Gateway, enableDrawing: drawing is not null);
        var byImport = bindings.ToDictionary(b => b.Import);
        plan = NeLoadPlan.CreateWithImportResolver(file, NeLoadPlan.PlaceSegments(image),
            import => byImport[import].Address);
        data = plan.Segments.Single(s => s.Source.Number == image.Header.AutomaticDataSegment);
        dllData = data.Placement.Selector;
        FarPointer16 startup = plan.ResolveCode(image.Header.Startup!.Value);
        FarPointer16 module = plan.ResolveCode(image.FindExport("MODULE")!.Address!.Value);
        beforeExecution = profile.Observe(data.Bytes);
        output.WriteLine($"1. PREPARE {hash}: {plan.Segments.Count} segments; {plan.Patches.Count} patches; startup {startup}; MODULE {module}");

        guest = new SegmentedGuest(output, trace, instructionLimit, this.diagnostics);
        try
        {
            foreach (PreparedNeSegment segment in plan.Segments)
            {
                // MemMap creates storage; MemWrite copies ALREADY relocated machine
                // code/data. Unicorn later fetches those bytes without any C# opcode enum.
                guest.Map(segment.Placement.Selector, segment.Placement.LinearBase, segment.Bytes, !segment.Source.IsData);
                output.WriteLine($"   S{segment.Source.Number} -> {segment.Placement.Selector:X4}:0000 at linear {segment.Placement.LinearBase:X5}, {segment.Bytes.Length} bytes");
            }

            // Allocate records before publishing handles. Neither 0101 nor 0102 is
            // a usable address: GlobalLock translates them into HostData:offset.
            var records = profile.CreateRecords(options);
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
            services = new Win16Api(new Win16ApiState(guest,
                new(new(dllData, checked((ushort)heapStart)), image.Header.HeapBytes), new(HostData, EnvironmentOffset),
                clock: this.timing.Clock) { Drawing = drawing });
            services.State.Blocks.Register(AfterDarkHostContract.SystemHandle, new(HostData, SystemOffset), records.System.Length);
            services.State.Blocks.Register(AfterDarkHostContract.ModuleHandle, new(HostData, ModuleOffset), records.Module.Length);
            output.WriteLine($"2. HOST RECORDS: handle 0101 -> {HostData:X4}:{SystemOffset:X4}; handle 0102 -> {HostData:X4}:{ModuleOffset:X4}");
            output.WriteLine($"   Options {options.Width}x{options.Height}, speed={options.Speed}, clear={options.Clear}; empty environment {HostData:X4}:{EnvironmentOffset:X4}");

            // Each caller executes a real CALL FAR and stores the returned AX itself.
            // MODULE's Pascal arguments are pushed left-to-right; RETF 6 removes them.
            byte[] callerBytes = new byte[SegmentedGuest.PageBytes];
            startupEnd = WriteCaller(callerBytes, StartupCaller, startup, StartupResult, null);
            preinitializeEnd = WriteCaller(callerBytes, PreinitializeCaller, module, PreinitializeResult, AfterDarkHostContract.PreinitializeMessage);
            initializeEnd = WriteCaller(callerBytes, InitializeCaller, module, InitializeResult, AfterDarkHostContract.InitializeMessage);
            // Prebuild callers rather than editing executable bytes between runs:
            // Unicorn may cache decoded code. Every invocation uses a real CALL FAR.
            blankEnd = WriteCaller(callerBytes, BlankCaller, module, DrawingResult, BlankMessage);
            drawEnd = WriteCaller(callerBytes, DrawCaller, module, DrawingResult, DrawFrameMessage);
            closeEnd = WriteCaller(callerBytes, CloseCaller, module, CloseResult, CloseMessage);
            FarPointer16 wep = plan.ResolveCode(image.FindExport("WEP")!.Address!.Value);
            wepEnd = WriteCaller(callerBytes, WepCaller, wep, WepResult, null, WepSystemExit);
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

            guest.DispatchGateway = DispatchHostCall;
            guest.DispatchInterrupt = number =>
            {
                if (number != DosClock.InterruptNumber) throw new NotSupportedException($"Unsupported INT {number:X2}h during {guest.Phase}.");
                ushort ax = (ushort)guest.Get(X86.UC_X86_REG_AX);
                var reply = DosClock.Respond((byte)(ax >> 8), ax, this.timing.CivilTime);
                guest.Set(X86.UC_X86_REG_AX, reply.Ax);
                guest.Set(X86.UC_X86_REG_CX, reply.Cx);
                guest.Set(X86.UC_X86_REG_DX, reply.Dx);
                if (output != TextWriter.Null)
                    output.WriteLine($"   {guest.Phase}: INT 21h AH={ax >> 8:X2} -> AX={reply.Ax:X4} CX={reply.Cx:X4} DX={reply.Dx:X4}; resume after INT (no RETF/IRET)");
            };

        }
        catch
        {
            // A throwing constructor never reaches the caller's using block.
            // Release an engine even when mapping/setup only partially succeeded.
            guest.Dispose();
            throw;
        }
    }

    /// <summary>Execute DLL startup, PREINITIALIZE and INITIALIZE exactly once.</summary>
    public PhaseResult Initialize()
    {
        RequireState(SessionState.Loaded);
        try
        {
            PhaseResult initializedDll = RunPhase("DLL startup", StartupCaller, startupEnd, StartupResult, dllData);
            if (initializedDll.StoredAx == 0 || profile.Instance(initializedDll.State) != dllData || services.State.InitializedHeap is null)
                throw new InvalidOperationException("DLL startup did not succeed and save its instance handle; MODULE will not run.");

            // Call MODULE from a different DS, exercising the export-prologue patch.
            // Initialization may create/select GDI objects. The persistent
            // surface and device-context state already exist before that call.
            guest.Set(X86.UC_X86_REG_DS, HostData);
            PhaseResult preinitialized = RunPhase("PREINITIALIZE", PreinitializeCaller, preinitializeEnd, PreinitializeResult, HostData);
            if (profile.Compatibility(preinitialized.State) != 1)
                throw new InvalidOperationException("Module rejected the supplied compatibility fields; INITIALIZE will not run.");
            // PREINITIALIZE's AX is incidental in this binary; the compatibility
            // variable above is the meaningful proof, not an invented success code.
            PhaseResult initialized = RunPhase("INITIALIZE", InitializeCaller, initializeEnd, InitializeResult, HostData);
            profile.ValidateInitialized(initialized.State, options, HostData, services.State.LastReturnedTick);
            if (guest.InterruptCount != 3 || services.State.Blocks.OutstandingLocks != 0)
                throw new InvalidOperationException("Initialization clock/lock invariants failed.");
            output.WriteLine($"4. GUEST STATE: {initialized.State}");
            State = SessionState.Initialized;
            return initialized;
        }
        catch
        {
            // An interrupted guest may be inside a procedure or own locks.
            // Retrying startup/drawing would resume from an invalid lifecycle state.
            State = SessionState.Faulted;
            throw;
        }
    }

    /// <summary>Invoke original BLANK. Required once before drawing; can also clear a ready surface.</summary>
    public PhaseResult Blank()
    {
        RequireDrawing();
        RequireState(SessionState.Initialized, SessionState.Ready);
        return RunDrawingPhase("BLANK", BlankCaller, blankEnd);
    }

    /// <summary>Run one original DRAWFRAME and return to the host, preserving all guest state.</summary>
    public PhaseResult DrawFrame()
    {
        RequireDrawing();
        RequireState(SessionState.Ready);
        return RunDrawingPhase("DRAWFRAME", DrawCaller, drawEnd);
    }

    /// <summary>
    /// End a healthy drawing lifecycle: MODULE(CLOSE) then WEP(1), both real
    /// far calls. Stop at a call boundary first; a faulted guest cannot safely
    /// run cleanup code. Idempotent after success. Dispose remains host-only.
    /// </summary>
    public void Shutdown()
    {
        if (State == SessionState.Closed) return;
        RequireState(SessionState.Ready);
        try
        {
            RunPhase("CLOSE", CloseCaller, closeEnd, CloseResult, HostData, profile.CloseServiceExitLimit);
            PhaseResult wep = RunPhase("WEP", WepCaller, wepEnd, WepResult, HostData);
            if (wep.StoredAx != 1) throw new InvalidOperationException("Module WEP did not return success.");
            if (LivePenCount != 0) throw new InvalidOperationException("Module shutdown leaked guest pens.");
            State = SessionState.Closed;
        }
        catch
        {
            State = SessionState.Faulted;
            throw;
        }
    }

    /// <summary>Return a detached RGB snapshot; later guest calls cannot change this array.</summary>
    public byte[] CopyPixels()
    {
        RequireDrawing();
        RequireState(SessionState.Ready, SessionState.Closed);
        return surface!.CopyRgb();
    }

    /// <summary>Allocate host buffers once using this tightly packed RGB byte count.</summary>
    public int PixelByteCount
    {
        get { RequireDrawing(); return surface!.RgbByteCount; }
    }

    /// <summary>
    /// Copy a completed image into host-owned reusable storage. The host must
    /// finish consuming it before reusing it; the guest never retains this span.
    /// </summary>
    public void CopyPixelsTo(Span<byte> destination)
    {
        RequireDrawing();
        RequireState(SessionState.Ready, SessionState.Closed);
        surface!.CopyRgbTo(destination);
    }

    public Win16Drawing.Operation? LastDrawingOperation
    {
        get
        {
            RequireDrawing();
            RequireState(SessionState.Ready);
            return drawing!.LastOperation;
        }
    }

    /// <summary>Snapshot diagnostics before disposal. Later calls do not append to this result's lists.</summary>
    public Result GetResult()
    {
        RequireState(SessionState.Initialized, SessionState.Ready, SessionState.Closed);
        return new(plan, beforeExecution, phases.Snapshot(), calls.Snapshot(),
            guest.Interrupts, services.State.InitializedHeap!,
            services.State.Blocks.OutstandingLocks, guest.Instructions, (guest.Get(X86.UC_X86_REG_CR0) & 1) != 0,
            new(diagnostics.HistoryCapacity, calls.TotalCount, phases.TotalCount, guest.InterruptCount,
                new ReadOnlyDictionary<string, long>(new Dictionary<string, long>(importCalls))));
    }

    private PhaseResult RunDrawingPhase(string name, ushort begin, ushort end)
    {
        try
        {
            PhaseResult result = RunPhase(name, begin, end, DrawingResult, HostData);
            State = SessionState.Ready;
            return result;
        }
        catch
        {
            State = SessionState.Faulted;
            throw;
        }
    }

    private void RequireState(SessionState allowed, SessionState? alternative = null, SessionState? third = null)
    {
        ObjectDisposedException.ThrowIf(State == SessionState.Disposed, this);
        if (State != allowed && State != alternative && State != third)
            throw new InvalidOperationException($"After Dark session is {State}; expected {allowed}" +
                (alternative is null ? "" : $" or {alternative}") + (third is null ? "" : $" or {third}") + ".");
    }

    private void RequireDrawing()
    {
        ObjectDisposedException.ThrowIf(State == SessionState.Disposed, this);
        if (drawing is null) throw new InvalidOperationException("This session was created for initialization only.");
    }

    /// <summary>
    /// Release native resources once, even after a fault. Does not execute guest
    /// code. Live hosts explicitly Shutdown first; bounded lessons may just Dispose.
    /// </summary>
    public void Dispose()
    {
        if (State == SessionState.Disposed) return;
        State = SessionState.Disposed;
        guest.Dispose();
    }

    private PhaseResult RunPhase(string name, ushort begin, ushort end, ushort resultOffset, ushort expectedDs,
        int serviceExitLimit = SegmentedGuest.DefaultServiceExitLimit)
    {
        var returned = guest.RunUntil(name, new(Caller, begin), new(Caller, end), Math.Max(serviceExitLimit, profile.ServiceExitLimit));
        ushort stored = BinaryPrimitives.ReadUInt16LittleEndian(guest.Read(new(HostData, resultOffset), 2));
        if (returned.Ss != Stack || returned.Sp != InitialSp || returned.Bp != 0 || returned.Ds != expectedDs || stored != returned.Ax)
            throw new InvalidOperationException($"{name}: unbalanced stack, changed caller DS, or mismatched guest store: {returned}.");
        if (services.State.Blocks.OutstandingLocks != 0) throw new InvalidOperationException($"{name}: leaked global locks.");
        var observation = new PhaseResult(name, returned, stored,
            profile.Observe(guest.Read(new(dllData, 0), data.Bytes.Length)));
        phases.Add(observation);
        if (output != TextWriter.Null)
            output.WriteLine($"   {name} returned to {returned.Pc}; AX={stored:X4} (guest stored); DS={returned.Ds:X4}; SS:SP={returned.Ss:X4}:{returned.Sp:X4}");
        return observation;
    }

    private void DispatchHostCall()
    {
        HostCall call = DispatchImport(guest, bindings, services, Stack);
        calls.Add(call);
        long total = importCalls.GetValueOrDefault(call.Binding.Name);
        importCalls[call.Binding.Name] = total == long.MaxValue ? total : total + 1;
        if (output != TextWriter.Null)
            output.WriteLine($"   {call.Phase}: {call.Binding.Name}({string.Join(",", call.Arguments.Select(a => a.ToString("X4")))}) -> {call.Returned:X8}; RETF {call.Binding.ArgumentBytes}, resume {call.After.Pc}");
    }

    private ushort WriteCaller(byte[] callerBytes, ushort begin, FarPointer16 target, ushort resultOffset,
        ushort? message, ushort? wepReason = null)
    {
        var code = new List<byte>();
        if (message is ushort value)
        {
            Push(value); Push(AfterDarkHostContract.ReservedHdc); Push(AfterDarkHostContract.SystemHandle);
        }
        // WEP has its own ABI: one WORD reason, removed by the DLL's RETF 2.
        // MODULE instead removes three WORD arguments with RETF 6.
        if (wepReason is ushort reason) Push(reason);
        code.Add(0x9A); Word(target.Offset); Word(target.Selector); // CALL FAR immediate
        code.AddRange([0x50, 0xB8]); Word(HostData); // PUSH AX; MOV AX,HostData
        code.AddRange([0x8E, 0xC0, 0x58]); // MOV ES,AX; POP AX (preserve returned value)
        code.AddRange([0x26, 0xA3]); Word(resultOffset); // MOV ES:[resultOffset],AX
        code.CopyTo(callerBytes, begin);
        return checked((ushort)(begin + code.Count));
        void Push(ushort value) { code.Add(0x68); Word(value); }
        void Word(ushort value) { code.Add((byte)value); code.Add((byte)(value >> 8)); }
    }
    // Public so original tiny guest programs can test the SAME argument/return
    // path without requiring anyone else's copyrighted DLL. Shared by both lessons
    // so the learner can step directly from a trapped call into this mechanism.
    public static HostCall DispatchImport(SegmentedGuest guest,
        IReadOnlyList<Win16Imports.ImportEntry> bindings, Win16Api services, ushort stack = 0x40)
    {
        var before = guest.Snapshot();
        var binding = bindings.SingleOrDefault(b => b.Address == before.Pc)
            ?? throw new NotSupportedException($"Unknown import gateway {before.Pc} during {guest.Phase}.");
        if (binding.Implementation == Win16Imports.Handler.Unsupported)
            throw new NotSupportedException($"{guest.Phase}: {binding.Name} reached at {before.Pc}; initialization-only lesson stops here.");
        if (before.Ss != stack || before.Sp + FarReturnBytes + binding.ArgumentBytes!.Value > InitialSp)
            throw new InvalidOperationException($"Invalid far Pascal frame for {binding.Name}.");
        var frame = new FarPascalWordFrame(guest.Read(new(stack, before.Sp), FarReturnBytes + binding.ArgumentBytes.Value));
        var arguments = new ushort[frame.ArgumentCount];
        for (int i = 0; i < arguments.Length; i++) arguments[i] = unchecked((ushort)frame.ReadArgument(i));
        var returnAddress = new FarPointer16(frame.ReturnCs, frame.ReturnIp);
        guest.RequireCode(returnAddress, 1);
        Win16Imports.Reply reply;
        try { reply = Win16Imports.Invoke(services, binding, arguments); }
        catch (Exception error) { throw new InvalidOperationException($"{guest.Phase}: {binding.Name} at {before.Pc}: {error.Message}", error); }
        // SetRect16 and InvertRect16 are void (unlike their modern Win32
        // counterparts). Preserve AX/DX instead of inventing a success value.
        if (binding.ReturnLayout != Win16ReturnLayout.Void) guest.Set(X86.UC_X86_REG_AX, (ushort)reply.Value);
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
