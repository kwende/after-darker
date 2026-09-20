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
public partial class AfterDarkSession<TState> : IAnimationSession
{
    private readonly SessionMemoryLayout memoryLayout;

    /// <summary>Selector for host-built lifecycle caller instructions.</summary>
    public ushort Caller => memoryLayout.Caller;
    /// <summary>Selector for the synthetic import addresses intercepted by the CPU hook.</summary>
    public ushort Gateway => memoryLayout.Gateway;
    /// <summary>Selector for the caller-owned guest stack.</summary>
    public ushort Stack => memoryLayout.Stack;
    /// <summary>Selector for host-provided system/module records and result storage.</summary>
    public ushort HostData => memoryLayout.HostData;
    /// <summary>Linear base of the caller's code under our slot allocation policy.</summary>
    public uint CallerBase => SessionMemoryLayout.LinearBase(Caller);
    /// <summary>Linear base of the gateway's guard instructions.</summary>
    public uint GatewayBase => SessionMemoryLayout.LinearBase(Gateway);
    /// <summary>Linear base of the stack segment.</summary>
    public uint StackBase => SessionMemoryLayout.LinearBase(Stack);
    /// <summary>Linear base of host-owned guest data.</summary>
    public uint HostDataBase => SessionMemoryLayout.LinearBase(HostData);
    /// <summary>Empty descending-stack offset, before any PUSH or CALL.</summary>
    public const ushort InitialSp = SessionMemoryLayout.EmptyStackPointer;
    /// <summary>AD_SYSTEM offset in HostData.</summary>
    public const ushort SystemOffset = SessionMemoryLayout.SystemOffset;
    /// <summary>AD_MODULE offset in HostData.</summary>
    public const ushort ModuleOffset = SessionMemoryLayout.ModuleOffset;
    /// <summary>Empty DOS environment offset in HostData.</summary>
    public const ushort EnvironmentOffset = SessionMemoryLayout.EnvironmentOffset;
    private const ushort StartupResult = 0x20, PreinitializeResult = 0x22, InitializeResult = 0x24;
    private const ushort StartupCaller = 0, PreinitializeCaller = 0x100, InitializeCaller = 0x200;
    /// <summary>Fixed local civil time used by deterministic educational captures.</summary>
    public static readonly DateTime CivilTime = new(1993, 6, 15, 12, 34, 56, DateTimeKind.Unspecified);

    /// <summary>SDK message requesting initial blanking.</summary>
    public const ushort BlankMessage = 1;
    /// <summary>SDK message requesting one animation update.</summary>
    public const ushort DrawFrameMessage = 2;
    /// <summary>SDK message requesting module cleanup.</summary>
    public const ushort CloseMessage = 3;
    private const ushort BlankCaller = 0x300, DrawCaller = 0x400, DrawingResult = 0x26;
    private const ushort CloseCaller = 0x500, WepCaller = 0x600, CloseResult = 0x28, WepResult = 0x2A;
    private const ushort WepSystemExit = 1;

    private readonly SegmentedGuest guest;
    private readonly TextWriter output;
    private readonly PlaybackOptions options;
    private readonly ModuleProfile<TState> profile;
    private readonly NeLoadPlan plan;
    private readonly PreparedNeSegment automaticDataSegment;
    private readonly ushort libraryDataSelector;
    private readonly ushort startupEnd;
    private readonly ushort preinitializeEnd;
    private readonly ushort initializeEnd;
    private readonly ushort blankEnd;
    private readonly ushort drawEnd;
    private readonly ushort closeEnd;
    private readonly ushort wepEnd;
    private readonly IReadOnlyList<Win16Imports.ImportEntry> importBindings;
    private readonly TState beforeExecution;
    private readonly Win16Api services;
    private readonly Win16ImportGateway importGateway;
    private readonly SessionTiming timing;
    private readonly PixelSurface? surface;
    private readonly Win16Drawing? drawing;
    private readonly ImportFrameCapture? intermediateFrames;
    private bool executingPhase;
    private readonly DiagnosticOptions diagnostics;
    private readonly DiagnosticHistory<Win16CallTrace> calls;
    private readonly DiagnosticHistory<PhaseResult> phases;
    // Keys come only from the fixed import registry; this cannot grow per frame.
    private readonly Dictionary<string, long> importCalls = [];

    /// <inheritdoc/>
    public string ModuleName => profile.Name;
    /// <summary>Owned pens currently held by the guest, excluding stock objects.</summary>
    public int LivePenCount => drawing?.LivePenCount ?? 0;
    /// <summary>Maximum concurrent owned pens in this session.</summary>
    public int PeakPenCount => drawing?.PeakPenCount ?? 0;
    /// <summary>Owned brushes currently held by the guest, excluding stock objects.</summary>
    public int LiveBrushCount => drawing?.LiveBrushCount ?? 0;
    /// <summary>Maximum simultaneous owned brushes in this session.</summary>
    public int PeakBrushCount => drawing?.PeakBrushCount ?? 0;
    /// <inheritdoc/>
    public event IntermediateFrameHandler? IntermediateFrameReady;
    void IAnimationSession.Initialize() => Initialize();
    void IAnimationSession.Blank() => Blank();
    void IAnimationSession.DrawFrame() => DrawFrame();
    /// <inheritdoc/>
    public PlaybackResult GetPlaybackResult()
    {
        var result = GetResult();
        return new(ModuleName, result.Phases.Select(phase => new PlaybackPhase(phase.Name, phase.StoredAx, phase.Registers)).ToArray(),
            result.Instructions, result.OutstandingLocks, result.Calls.Count, LivePenCount, PeakPenCount, result.Diagnostics.ImportCalls)
        { LocalHeap = result.LocalHeap, IntermediateFrames = intermediateFrames?.TotalVisits ?? 0,
            LiveBrushes = LiveBrushCount, PeakBrushes = PeakBrushCount,
            LiveBitmaps = drawing?.LiveBitmapCount ?? 0, PeakBitmaps = drawing?.PeakBitmapCount ?? 0,
            LiveMemoryDcs = drawing?.LiveMemoryDcCount ?? 0, PeakMemoryDcs = drawing?.PeakMemoryDcCount ?? 0,
            BitmapBytes = drawing?.BitmapBytes ?? 0 };
    }

    /// <summary>Valid lifecycle stages; faults forbid further guest execution.</summary>
    public enum SessionState
    {
        /// <summary>Mapped and prepared, with no guest instructions executed.</summary>
        Loaded,
        /// <summary>Startup and initialization completed; BLANK is required before drawing.</summary>
        Initialized,
        /// <summary>BLANK completed; DRAWFRAME or orderly Shutdown may run.</summary>
        Ready,
        /// <summary>CLOSE and WEP completed successfully.</summary>
        Closed,
        /// <summary>Execution failed; only inspection-free disposal is safe.</summary>
        Faulted,
        /// <summary>The native engine has been released.</summary>
        Disposed
    }
    /// <summary>Current lifecycle gate, checked before every operation.</summary>
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
            byte[]? initialPixels = profile.CreateInitialPixels(options);
            if (initialPixels is not null) surface.LoadRgb(initialPixels);
            drawing = new Win16Drawing();
            drawing.Register(AfterDarkHostContract.ReservedHdc, surface);
        }
        NeImage image = NeReader.Read(file);
        memoryLayout = new SessionMemoryLayout(image.Segments.Count);
        importBindings = Win16Imports.BindImports(image, Gateway, enableDrawing: drawing is not null);
        var bindingsByImport = importBindings.ToDictionary(binding => binding.Import);
        plan = NeLoadPlan.CreateWithImportResolver(file, NeLoadPlan.PlaceSegments(image),
            import => bindingsByImport[import].Address);
        if (surface is not null && profile.FrameCheckpoints.Count > 0)
            intermediateFrames = new(profile.FrameCheckpoints, plan.ResolveCode, surface.RgbByteCount);
        automaticDataSegment = plan.Segments.Single(segment => segment.Source.Number == image.Header.AutomaticDataSegment);
        libraryDataSelector = automaticDataSegment.Placement.Selector;
        FarPointer16 startup = plan.ResolveCode(image.Header.Startup!.Value);
        FarPointer16 module = plan.ResolveCode(image.FindExport("MODULE")!.Address!.Value);
        beforeExecution = profile.Observe(automaticDataSegment.Bytes);
        output.WriteLine($"1. PREPARE {hash}: {plan.Segments.Count} segments; {plan.Patches.Count} patches; startup {startup}; MODULE {module}");

        guest = new SegmentedGuest(output, trace, instructionLimit, this.diagnostics, profile.NativeSliceTimeout);
        try
        {
            MapLibrarySegments();
            var records = profile.CreateRecords(this.options);
            MapHostSegments(records);
            services = CreateWindowsServices(image.Header.HeapBytes, records);
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

            var startupContext = new LibraryStartupContext(libraryDataSelector, image.Header.HeapBytes, Stack, InitialSp);
            Win16RegisterConvention.SetUpPrologRegisters(guest, startupContext);
            output.WriteLine($"3. STARTUP INPUTS: DS={libraryDataSelector:X4}, DI={libraryDataSelector:X4}, CX={image.Header.HeapBytes}, ES:SI=0000:0000, SS:SP={Stack:X4}:{InitialSp:X4}, BP=0");

            var stack = new Win16Stack(guest, Stack, InitialSp);
            importGateway = new Win16ImportGateway(guest, importBindings, services, stack);
            guest.DispatchGateway = DispatchHostCall;
            var dosDispatcher = new DosInterruptDispatcher(guest, this.timing, output);
            guest.DispatchInterrupt = dosDispatcher.Dispatch;

        }
        catch
        {
            // A throwing constructor never reaches the caller's using block.
            // Release an engine even when mapping/setup only partially succeeded.
            guest.Dispose();
            throw;
        }
    }

    /// <summary>Copy relocated DLL code and data into mapped guest segments.</summary>
    private void MapLibrarySegments()
    {
        foreach (PreparedNeSegment segment in plan.Segments)
        {
            // MemMap creates storage; MemWrite copies ALREADY relocated machine
            // code/data. Unicorn later fetches those bytes without any C# opcode enum.
            byte[] mappedBytes = segment.Bytes;
            if (segment == automaticDataSegment && profile.AllowLocalHeapGrowth)
            {
                // A Win16 local heap stays in DGROUP. Reserve its full bounded
                // address space now so later growth needs no moving native pointers
                // or descriptor reloads. Startup still receives the NE's INITIAL size.
                mappedBytes = new byte[65536];
                segment.Bytes.CopyTo(mappedBytes, 0);
            }
            guest.Map(segment.Placement.Selector, segment.Placement.LinearBase, mappedBytes, !segment.Source.IsData);
            output.WriteLine($"   S{segment.Source.Number} -> {segment.Placement.Selector:X4}:0000 at linear {segment.Placement.LinearBase:X5}, {mappedBytes.Length} mapped bytes");
        }

    }

    /// <summary>Map records, an empty stack, and code guards which the gateway hook must stop before executing.</summary>
    private void MapHostSegments((byte[] System, byte[] Module) records)
    {
        byte[] hostBytes = new byte[SegmentedGuest.PageBytes];
        records.System.CopyTo(hostBytes, SystemOffset);
        records.Module.CopyTo(hostBytes, ModuleOffset);
        // EnvironmentOffset already holds two NUL bytes: a valid empty string list.
        // No TZ selects the guest runtime's built-in timezone, not the host's.
        for (int resultByteOffset = StartupResult; resultByteOffset <= InitializeResult + 1; resultByteOffset++)
        {
            hostBytes[resultByteOffset] = 0xCC;
        }
        guest.Map(HostData, HostDataBase, hostBytes, code: false);
        guest.Map(Stack, StackBase, new byte[SegmentedGuest.PageBytes], code: false);

        byte[] gatewayBytes = new byte[SegmentedGuest.PageBytes];
        foreach (var binding in importBindings)
        {
            gatewayBytes[binding.Address.Offset] = 0x0F;
            gatewayBytes[binding.Address.Offset + 1] = 0x0B; // UD2; never execute the synthetic address.
            output.WriteLine($"   {binding.Name} -> {binding.Address}: {binding.Implementation}");
        }
        guest.Map(Gateway, GatewayBase, gatewayBytes, code: true);
    }

    /// <summary>Give Windows services checked guest memory and registered global-block handles.</summary>
    private Win16Api CreateWindowsServices(ushort heapBytes, (byte[] System, byte[] Module) records)
    {
        int heapStart = Math.Max(automaticDataSegment.Source.FileBytes, automaticDataSegment.Source.MinimumAllocationBytes);
        if (automaticDataSegment.Bytes.Length != heapStart + heapBytes)
            throw new InvalidOperationException("Prepared data does not include the declared heap tail.");
        var heapAddress = new FarPointer16(libraryDataSelector, checked((ushort)heapStart));
        var heapReservation = new LocalHeapReservation(heapAddress, heapBytes);
        var environmentAddress = new FarPointer16(HostData, EnvironmentOffset);
        int heapCapacity = profile.AllowLocalHeapGrowth ? 65536 - heapStart : heapBytes;
        var apiState = new Win16ApiState(guest, heapReservation, environmentAddress, clock: timing.Clock,
            localHeapCapacityBytes: heapCapacity)
        {
            Drawing = drawing
        };
        var api = new Win16Api(apiState);
        api.State.Blocks.Register(AfterDarkHostContract.SystemHandle, new(HostData, SystemOffset), records.System.Length);
        api.State.Blocks.Register(AfterDarkHostContract.ModuleHandle, new(HostData, ModuleOffset), records.Module.Length);
        return api;
    }

    /// <summary>Execute DLL startup, PREINITIALIZE and INITIALIZE exactly once.</summary>
    public PhaseResult Initialize()
    {
        RequireState(SessionState.Loaded);
        try
        {
            PhaseResult initializedDll = RunPhase("DLL startup", StartupCaller, startupEnd, StartupResult, libraryDataSelector);
            if (initializedDll.StoredAx == 0 || profile.Instance(initializedDll.State) != libraryDataSelector || services.State.InitializedHeap is null)
                throw new InvalidOperationException("DLL startup did not succeed and save its instance handle; MODULE will not run.");

            // Call MODULE from a different DS, exercising the export-prologue patch.
            // Initialization may create/select GDI objects. The persistent
            // surface and device-context state already exist before that call.
            Win16RegisterConvention.SetUpModuleCallerDataSegment(guest, HostData);
            PhaseResult preinitialized = RunPhase("PREINITIALIZE", PreinitializeCaller, preinitializeEnd, PreinitializeResult, HostData);
            if (profile.Compatibility(preinitialized.State) != 1)
                throw new InvalidOperationException("Module rejected the supplied compatibility fields; INITIALIZE will not run.");
            // PREINITIALIZE's AX is incidental in this binary; the compatibility
            // variable above is the meaningful proof, not an invented success code.
            PhaseResult initialized = RunPhase("INITIALIZE", InitializeCaller, initializeEnd, InitializeResult, HostData);
            if (initialized.StoredAx != 0) throw new InvalidOperationException($"{ModuleName} INITIALIZE failed with code {initialized.StoredAx}.");
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
        intermediateFrames?.BeginDraw();
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
            if (LiveBrushCount != 0) throw new InvalidOperationException("Module shutdown leaked guest brushes.");
            if (drawing?.LiveMemoryDcCount > 0 || drawing?.LiveBitmapCount > 0 || drawing?.BitmapBytes > 0)
                throw new InvalidOperationException("Module shutdown leaked guest bitmap/DC resources.");
            if (services.State.LocalHeap?.Snapshot().Allocations.Count > 0)
                throw new InvalidOperationException("Module shutdown leaked local heap allocations.");
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

    /// <summary>Most recent raster operation, available while the drawing session is ready.</summary>
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
                new ReadOnlyDictionary<string, long>(new Dictionary<string, long>(importCalls))))
        { LocalHeap = services.State.LocalHeap?.Snapshot() };
    }

    /// <summary>Execute BLANK or DRAWFRAME and transition to Ready, or Faulted on failure.</summary>
    private PhaseResult RunDrawingPhase(string name, ushort begin, ushort end)
    {
        try
        {
            PhaseResult result = RunPhase(name, begin, end, DrawingResult, HostData);
            if (name == "BLANK") profile.ValidateBlankResult(result.StoredAx);
            else if (result.StoredAx != 0) throw new NotSupportedException($"{ModuleName} DRAWFRAME returned unsupported code {result.StoredAx}.");
            State = SessionState.Ready;
            return result;
        }
        catch
        {
            State = SessionState.Faulted;
            throw;
        }
    }

    /// <summary>Reject lifecycle misuse before touching the CPU.</summary>
    private void RequireState(SessionState allowed, SessionState? alternative = null, SessionState? third = null)
    {
        ObjectDisposedException.ThrowIf(State == SessionState.Disposed, this);
        if (executingPhase) throw new InvalidOperationException("A guest call is active; presentation callbacks cannot reenter the session.");
        if (State != allowed && State != alternative && State != third)
            throw new InvalidOperationException($"After Dark session is {State}; expected {allowed}" +
                (alternative is null ? "" : $" or {alternative}") + (third is null ? "" : $" or {third}") + ".");
    }

    /// <summary>Reject rendering operations on initialization-only or disposed sessions.</summary>
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
        if (executingPhase) throw new InvalidOperationException("A guest call is active; dispose only after it returns.");
        if (State == SessionState.Disposed) return;
        State = SessionState.Disposed;
        guest.Dispose();
    }

    /// <summary>Run the guest caller, check restored stack and DS, and observe the original DLL globals.</summary>
    private PhaseResult RunPhase(string name, ushort begin, ushort end, ushort resultOffset, ushort expectedDs,
        int serviceExitLimit = SegmentedGuest.DefaultServiceExitLimit)
    {
        // A presenter runs while the CPU is stopped inside an unfinished far call.
        // It may copy supplied pixels, but cannot start another call or dispose
        // the engine. Keep that invariant explicit rather than relying on the UI.
        if (executingPhase) throw new InvalidOperationException("A guest call is already active.");
        executingPhase = true;
        try { return ExecuteAndObservePhase(name, begin, end, resultOffset, expectedDs, serviceExitLimit); }
        finally { executingPhase = false; }
    }

    /// <summary>Execute the bounded caller and verify its return frame before recording module state.</summary>
    private PhaseResult ExecuteAndObservePhase(string name, ushort begin, ushort end, ushort resultOffset,
        ushort expectedDs, int serviceExitLimit)
    {
        var returned = guest.RunUntil(name, new(Caller, begin), new(Caller, end), Math.Max(serviceExitLimit, profile.ServiceExitLimit));
        ushort stored = BinaryPrimitives.ReadUInt16LittleEndian(guest.Read(new(HostData, resultOffset), 2));
        if (returned.Ss != Stack || returned.Sp != InitialSp || returned.Bp != 0 || returned.Ds != expectedDs || stored != returned.Ax)
            throw new InvalidOperationException($"{name}: unbalanced stack, changed caller DS, or mismatched guest store: {returned}.");
        if (services.State.Blocks.OutstandingLocks != 0) throw new InvalidOperationException($"{name}: leaked global locks.");
        var observation = new PhaseResult(name, returned, stored,
            profile.Observe(guest.Read(new(libraryDataSelector, 0), automaticDataSegment.Bytes.Length)));
        phases.Add(observation);
        if (output != TextWriter.Null)
            output.WriteLine($"   {name} returned to {returned.Pc}; AX={stored:X4} (guest stored); DS={returned.Ds:X4}; SS:SP={returned.Ss:X4}:{returned.Sp:X4}");
        return observation;
    }

    /// <summary>Record a managed gateway dispatch without embedding the stack or register convention here.</summary>
    private void DispatchHostCall()
    {
        Win16CallTrace call = importGateway.Dispatch();
        calls.Add(call);
        long total = importCalls.GetValueOrDefault(call.Binding.Name);
        importCalls[call.Binding.Name] = total == long.MaxValue ? total : total + 1;
        intermediateFrames?.AfterImport(call.Phase, call.After.Pc, call.Binding.Import, surface!, IntermediateFrameReady);
        if (output != TextWriter.Null)
            output.WriteLine($"   {call.Phase}: {call.Binding.Name}({string.Join(",", call.Arguments.Select(argument => argument.ToString("X4")))}) -> {call.Returned:X8}; RETF {call.Binding.ArgumentBytes}, resume {call.After.Pc}");
    }

    /// <summary>Choose the lifecycle call's Pascal arguments, then encode a real x86 caller.</summary>
    private ushort WriteCaller(byte[] callerBytes, ushort begin, FarPointer16 target, ushort resultOffset,
        ushort? message, ushort? wepReason = null)
    {
        ushort[] arguments = [];
        if (message is ushort lifecycleMessage)
        {
            arguments = [lifecycleMessage, AfterDarkHostContract.ReservedHdc, AfterDarkHostContract.SystemHandle];
        }
        else if (wepReason is ushort exitReason)
        {
            arguments = [exitReason];
        }

        // MODULE removes three words with RETF 6; WEP removes one with RETF 2.
        return GuestCallerBuilder.WriteCaller(callerBytes, begin, target,
            new FarPointer16(HostData, resultOffset), arguments);
    }
}
