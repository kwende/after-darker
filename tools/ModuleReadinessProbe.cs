using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using AfterDarker.Core.AfterDark;
using AfterDarker.Core.Ne;
using AfterDarker.Core.Rendering;
using AfterDarker.Core.Win16;
using AfterDarker.Runtime;
using UnicornEngine.Const;

namespace AfterDarker.Tests.Research;

/// <summary>
/// Opt-in, process-isolated investigation of local modules using current production services.
/// Passing this test means a report was written, not that the module is compatible.
/// The Python launcher supplies a process watchdog. Unknown imports stop on use;
/// they never receive invented return values. See docs/research/windows98-readiness-audit.md.
/// </summary>
[TestClass]
public sealed class ModuleReadinessProbe
{
    private const ushort EmptyStack = 0x1000;
    private const ushort SystemOffset = 0x100, ModuleOffset = 0x200, EnvironmentOffset = 0x300;
    private const int Width = 320, Height = 240;

    /// <summary>Environment-supplied local inputs, isolated from ordinary test discovery.</summary>
    public sealed record Request(string ModulePath, string ReportPath, string RelativePath,
        int Frames = 30, uint TickStep = 2500, ushort HostVersion = 200,
        bool WhiteBackground = false, ushort[]? Controls = null, bool DirectRgbPaletteRequests = false,
        bool ModuleSelected = false);

    /// <summary>One bounded attempt; production-supported hashes use their real profiles.</summary>
    [TestMethod]
    public void InspectAndProbe()
    {
        string? requestJson = Environment.GetEnvironmentVariable("AFTER_DARK_AUDIT_REQUEST");
        if (requestJson is null) Assert.Inconclusive("Run through tools/audit-local-modules.py.");
        Request request = JsonSerializer.Deserialize<Request>(requestJson!)!;
        byte[] file = File.ReadAllBytes(request.ModulePath);
        var phases = new List<object>();
        var calls = new Dictionary<string, int>();
        var report = new Dictionary<string, object?>
        {
            ["File"] = request.RelativePath,
            ["Sha256"] = Convert.ToHexString(SHA256.HashData(file)),
            ["FramesRequested"] = request.Frames,
            ["TickStep"] = request.TickStep,
            ["HostVersion"] = request.HostVersion,
            ["WhiteBackground"] = request.WhiteBackground,
            ["DirectRgbPaletteRequests"] = request.DirectRgbPaletteRequests,
            ["ModuleSelected"] = request.ModuleSelected,
            ["Phases"] = phases, ["Calls"] = calls
        };
        string phase = "parse";
        void Save()
        {
            // A watchdog can kill the process during a write. Publish complete
            // JSON only, retaining the preceding phase if this write is interrupted.
            string pendingPath = request.ReportPath + ".pending";
            File.WriteAllText(pendingPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(pendingPath, request.ReportPath, overwrite: true);
        }

        try
        {
            NeImage image = NeReader.Read(file);
            report["ModuleDescriptions"] = image.Names.Where(name => name.Ordinal == 0).Select(name => name.Text).ToArray();
            report["Segments"] = image.Segments.Count;
            report["HeapBytes"] = image.Header.HeapBytes;
            report["Resources"] = image.Resources.GroupBy(resource => resource.Type.Number?.ToString() ?? resource.Type.Name!)
                .ToDictionary(group => group.Key, group => group.Count());
            report["OsFixups"] = image.Relocations.Count(relocation => relocation.Kind == 3);
            report["ModuleEntry"] = image.FindExport("MODULE")?.Address?.ToString();
            report["WepEntry"] = image.FindExport("WEP")?.Address?.ToString();

            string? supportedName;
            try { supportedName = SupportedModules.Identify(file); }
            catch (NotSupportedException) { supportedName = null; }
            report["SupportedName"] = supportedName;
            if (supportedName is not null)
            {
                // This is the actual player entry point, including its artifact-specific controls.
                phase = "supported profile construction"; Save();
                using IAnimationSession session = SupportedModules.Open(file, new PlaybackOptions(Width, Height));
                phase = "INITIALIZE"; Save(); session.Initialize();
                phase = "BLANK"; Save(); session.Blank();
                for (int frame = 1; frame <= request.Frames; frame++)
                {
                    phase = $"DRAWFRAME {frame}"; Save(); session.DrawFrame();
                }
                byte[] pixels = new byte[session.PixelByteCount];
                session.CopyPixelsTo(pixels);
                report["PixelHash"] = Convert.ToHexString(SHA256.HashData(pixels));
                phase = "shutdown"; Save(); session.Shutdown();
                report["Playback"] = session.GetPlaybackResult();
                report["Outcome"] = "Supported profile completed drawing and shutdown";
                return;
            }

            phase = "import bindings";
            ushort caller = checked((ushort)((image.Segments.Count + 1) * 8));
            ushort gateway = checked((ushort)(caller + 8)), stack = checked((ushort)(caller + 16));
            ushort hostData = checked((ushort)(caller + 24));
            var bindings = new List<Win16Imports.ImportEntry>();
            foreach (NeImport import in image.Imports)
            {
                // Use the real C# registry, including named AD_SND services. The older
                // static regex census does not recognize all those name-based bindings.
                NeImage singleImport = image with { Relocations = image.Relocations.Where(item => item.Import == import).ToArray() };
                Win16Imports.ImportEntry binding;
                try { binding = Win16Imports.BindImports(singleImport, gateway, true).Single(); }
                catch (NotSupportedException)
                {
                    binding = new(import, $"{import.Module}!{import.Name ?? $"#{import.Ordinal}"}",
                        new(gateway, 0), Win16Imports.Handler.Unsupported, null, null);
                }
                bindings.Add(binding with { Address = new(gateway, checked((ushort)(0x100 + bindings.Count * 0x10))) });
            }
            report["ImportCoverage"] = bindings.Select(binding => new
            {
                binding.Import.Module, binding.Import.Ordinal, binding.Import.Name,
                Symbol = binding.Name, Implementation = binding.Implementation.ToString()
            }).ToArray();
            phase = "loader"; Save();
            NeLoadPlan plan = NeLoadPlan.CreateWithImportResolver(file, NeLoadPlan.PlaceSegments(image),
                import => bindings.Single(binding => binding.Import == import).Address);
            report["Loader"] = "pass";
            report["Patches"] = plan.Patches.Count;

            phase = "host setup"; Save();
            NeAddress moduleEntry = image.FindExport("MODULE")?.Address
                ?? throw new NotSupportedException("No exported MODULE entry point for the current lifecycle contract.");
            NeAddress wepEntry = image.FindExport("WEP")?.Address
                ?? throw new NotSupportedException("No exported WEP entry point for the current shutdown contract.");
            var data = plan.Segments.Single(segment => segment.Source.Number == image.Header.AutomaticDataSegment);
            using var guest = new SegmentedGuest(instructionLimit: 2_000_000, nativeSliceTimeout: TimeSpan.FromSeconds(3));
            foreach (PreparedNeSegment segment in plan.Segments)
            {
                byte[] mapped = segment.Bytes;
                if (segment == data) { mapped = new byte[65536]; segment.Bytes.CopyTo(mapped, 0); }
                guest.Map(segment.Placement.Selector, segment.Placement.LinearBase, mapped, !segment.Source.IsData);
            }

            ushort[] controls = ReadDiagnosticControls(file, image, report);
            if (request.Controls is not null)
            {
                if (request.Controls.Length != 4) throw new ArgumentException("Supply four control words.");
                controls = request.Controls;
            }
            report["ControlWords"] = controls;
            byte[] hostBytes = new byte[4096];
            void HostWord(int offset, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(hostBytes.AsSpan(offset), value);
            AfterDarkHostContract.CreateSystemRecord().CopyTo(hostBytes, SystemOffset);
            HostWord(SystemOffset, 2); HostWord(SystemOffset + 2, 3);
            HostWord(SystemOffset + AfterDarkHostContract.ScreenWidth, Width);
            HostWord(SystemOffset + AfterDarkHostContract.ScreenHeight, Height);
            HostWord(SystemOffset + AfterDarkHostContract.ColorDepth, 24);
            HostWord(SystemOffset + AfterDarkHostContract.PixelAspectX, 1);
            HostWord(SystemOffset + AfterDarkHostContract.PixelAspectY, 1);
            HostWord(SystemOffset + 0x10, 96); HostWord(SystemOffset + 0x12, 96);
            HostWord(SystemOffset + AfterDarkHostContract.SystemVersion, request.HostVersion);
            HostWord(ModuleOffset + 2, Width); HostWord(ModuleOffset + 4, Height);
            for (int index = 0; index < 4; index++)
            {
                HostWord(ModuleOffset + 6 + index * 2, controls[index]);
                HostWord(ModuleOffset + 14 + index * 2, (ushort)(index + 1));
            }
            guest.Map(hostData, LinearBase(hostData), hostBytes, false);
            guest.Map(stack, LinearBase(stack), new byte[4096], false);
            int gatewayBytesNeeded = 0x100 + bindings.Count * 0x10 + 2;
            byte[] gatewayBytes = new byte[(gatewayBytesNeeded + 4095) & ~4095];
            foreach (var binding in bindings)
            {
                gatewayBytes[binding.Address.Offset] = 0x0F;
                gatewayBytes[binding.Address.Offset + 1] = 0x0B;
            }
            guest.Map(gateway, LinearBase(gateway), gatewayBytes, true);

            byte[] callerBytes = new byte[4096];
            (ushort Begin, ushort End) BuildCaller(ushort start, FarPointer16 target, params ushort[] arguments)
            {
                var instructions = new List<byte>();
                void Word(ushort value) { instructions.Add((byte)value); instructions.Add((byte)(value >> 8)); }
                foreach (ushort argument in arguments) { instructions.Add(0x68); Word(argument); }
                instructions.Add(0x9A); Word(target.Offset); Word(target.Selector);
                instructions.CopyTo(callerBytes, start);
                return (start, checked((ushort)(start + instructions.Count)));
            }
            FarPointer16 module = plan.ResolveCode(moduleEntry);
            var startup = BuildCaller(0, plan.ResolveCode(image.Header.Startup!.Value));
            var preinitialize = BuildCaller(0x100, module, 12, AfterDarkHostContract.ReservedHdc, AfterDarkHostContract.SystemHandle);
            var initialize = BuildCaller(0x200, module, 0, AfterDarkHostContract.ReservedHdc, AfterDarkHostContract.SystemHandle);
            var blank = BuildCaller(0x300, module, 1, AfterDarkHostContract.ReservedHdc, AfterDarkHostContract.SystemHandle);
            var draw = BuildCaller(0x400, module, 2, AfterDarkHostContract.ReservedHdc, AfterDarkHostContract.SystemHandle);
            var close = BuildCaller(0x500, module, 3, AfterDarkHostContract.ReservedHdc, AfterDarkHostContract.SystemHandle);
            var wep = BuildCaller(0x600, plan.ResolveCode(wepEntry), 1);
            var selected = BuildCaller(0x700, module, 5, AfterDarkHostContract.ReservedHdc, AfterDarkHostContract.SystemHandle);
            guest.Map(caller, LinearBase(caller), callerBytes, true);
            guest.Install(gateway);

            var surface = new PixelSurface(Width, Height);
            if (request.WhiteBackground) surface.Paint(new(0, 0, Width, Height), invert: true);
            var drawing = new Win16Drawing(); drawing.Register(AfterDarkHostContract.ReservedHdc, surface);
            int heapStart = Math.Max(data.Source.FileBytes, data.Source.MinimumAllocationBytes);
            var api = new Win16Api(new Win16ApiState(guest,
                new(new(data.Placement.Selector, checked((ushort)heapStart)), image.Header.HeapBytes),
                new(hostData, EnvironmentOffset), tickStep: request.TickStep, localHeapCapacityBytes: 65536 - heapStart)
            {
                Drawing = drawing,
                ModuleResources = new(data.Placement.Selector, new NeResourceCatalog(file, image))
            });
            api.State.Blocks.Register(AfterDarkHostContract.SystemHandle, new(hostData, SystemOffset), AfterDarkHostContract.SystemBytes);
            api.State.Blocks.Register(AfterDarkHostContract.ModuleHandle, new(hostData, ModuleOffset), 0x22);
            var guestStack = new Win16Stack(guest, stack, EmptyStack);
            var dispatcher = new Win16ImportGateway(guest, bindings, api, guestStack);
            guest.DispatchGateway = () =>
            {
                var binding = bindings.Single(item => item.Address == guest.Snapshot().Pc);
                report["LastImport"] = binding.Name;
                report["LastRegisters"] = guest.Snapshot();
                report.Remove("LastArguments");
                if (binding.ArgumentBytes is int argumentBytes)
                    report["LastArguments"] = guestStack.ReadCallFrame(argumentBytes, binding.Name).Arguments;
                calls[binding.Name] = calls.GetValueOrDefault(binding.Name) + 1;
                dispatcher.Dispatch();
            };
            guest.DispatchInterrupt = number =>
            {
                if (number != 0x21) throw new NotSupportedException($"INT {number:X2}");
                ushort ax = (ushort)guest.Get(X86.UC_X86_REG_AX);
                var reply = DosClock.Respond((byte)(ax >> 8), ax, AfterDarkSession<object>.CivilTime);
                guest.Set(X86.UC_X86_REG_AX, reply.Ax); guest.Set(X86.UC_X86_REG_CX, reply.Cx); guest.Set(X86.UC_X86_REG_DX, reply.Dx);
            };
            Win16RegisterConvention.SetUpPrologRegisters(guest, new(data.Placement.Selector, image.Header.HeapBytes, stack, EmptyStack));
            ushort RunPhase(string name, (ushort Begin, ushort End) code)
            {
                phase = name; report["Phase"] = phase; Save();
                var registers = guest.RunUntil(name, new(caller, code.Begin), new(caller, code.End), 4096);
                if (registers.Sp != EmptyStack || registers.Ss != stack || registers.Bp != 0)
                    throw new InvalidOperationException("Unbalanced caller stack.");
                phases.Add(new
                {
                    Name = name, registers.Ax, Instructions = guest.Instructions, surface.Revision,
                    Locks = api.State.Blocks.OutstandingLocks, drawing.LivePenCount, drawing.LiveBrushCount,
                    drawing.LiveBitmapCount, drawing.LiveMemoryDcCount, drawing.LiveRegionCount,
                    LocalHeap = api.State.LocalHeap?.Snapshot()
                });
                report["PixelHash"] = Convert.ToHexString(SHA256.HashData(surface.CopyRgb()));
                Save();
                return registers.Ax;
            }
            if (RunPhase("startup", startup) == 0) throw new InvalidOperationException("DLL startup returned failure.");
            Win16RegisterConvention.SetUpModuleCallerDataSegment(guest, hostData);
            if (request.ModuleSelected) RunPhase("MODULESELECTED", selected);
            RunPhase("PREINITIALIZE", preinitialize);
            void RequireSuccess(string name, (ushort Begin, ushort End) code)
            {
                ushort result = RunPhase(name, code);
                if (name == "BLANK" && request.DirectRgbPaletteRequests && result is 10 or 11 or 13)
                {
                    // SDK: HSV_PAL=10, RGB_PAL=11, PRIMARY_PAL=13. This explicit
                    // research continuation tests direct COLORREF drawing on RGB24.
                    // It supplies no palette handles and no indexed-palette emulation.
                    report["AcceptedPaletteRequest"] = result;
                    return;
                }
                if (result != 0) throw new NotSupportedException($"Module returned {result}; no unverified host response was supplied.");
            }
            RequireSuccess("INITIALIZE", initialize);
            RequireSuccess("BLANK", blank);
            int changedCalls = 0, colorFrames = 0;
            string? previousHash = report["PixelHash"] as string;
            for (int frame = 1; frame <= request.Frames; frame++)
            {
                RequireSuccess($"DRAWFRAME {frame}", draw);
                string? currentHash = report["PixelHash"] as string;
                if (currentHash != previousHash) changedCalls++;
                previousHash = currentHash;
                byte[] pixels = surface.CopyRgb();
                var colors = new HashSet<int>();
                for (int pixel = 0; pixel < pixels.Length; pixel += 3)
                    colors.Add((pixels[pixel] << 16) | (pixels[pixel + 1] << 8) | pixels[pixel + 2]);
                if (colors.Any(color => ((color >> 16) & 255) != ((color >> 8) & 255) || (color & 255) != ((color >> 8) & 255))) colorFrames++;
                report["ChangedDrawCalls"] = changedCalls;
                report["FramesWithColorPixels"] = colorFrames;
                report["LastFrameDistinctColors"] = colors.Count;
                if (frame == 1 || frame % 100 == 0 || frame == request.Frames)
                {
                    string capturePath = Path.ChangeExtension(request.ReportPath, $"frame-{frame:D4}.png");
                    using var capture = File.Create(capturePath);
                    PngWriter.Write(capture, Width, Height, pixels);
                }
            }
            RunPhase("CLOSE", close);
            if (RunPhase("WEP", wep) != 1) throw new InvalidOperationException("WEP did not return success.");
            if (api.State.Blocks.OutstandingLocks != 0 || drawing.LivePenCount != 0 || drawing.LiveBrushCount != 0 ||
                drawing.LiveBitmapCount != 0 || drawing.LiveMemoryDcCount != 0 || drawing.LiveRegionCount != 0 ||
                api.State.LocalHeap?.Snapshot().Allocations.Count > 0)
                throw new InvalidOperationException("Guest-owned objects or locks remain after shutdown.");
            report["Outcome"] = "Diagnostic records completed bounded drawing and shutdown; profile validation still required";
        }
        catch (Exception error)
        {
            report["FailedPhase"] = phase;
            report["Error"] = error.Message;
        }
        finally { Save(); }
    }

    private static uint LinearBase(ushort selector) => (uint)(selector / 8) << 16;

    /// <summary>Resource-derived diagnostic controls, never claimed to be original engine defaults.</summary>
    private static ushort[] ReadDiagnosticControls(byte[] file, NeImage image, IDictionary<string, object?> report)
    {
        ushort[] values = new ushort[4];
        var controls = new List<object>();
        foreach (var resource in image.Resources.Where(item => item.Type.Number == 1000 && item.Id.Number is >= 1 and <= 4))
        {
            byte[] bytes = file.AsSpan(resource.FileOffset, resource.Length).ToArray();
            ushort Word(int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset, 2));
            string Label(int offset, int count) => System.Text.Encoding.Latin1.GetString(bytes, offset, count).Split('\0')[0];
            int kind = Word(0), raw = Word(24), value = raw;
            string title = Label(2, 14);
            var choices = new List<string>();
            var bounds = new List<int>();
            if (kind is 1 or 3)
                for (int index = 0; index < Word(22); index++) choices.Add(Label(32 + index * 16, 16));
            if (kind == 1)
            {
                value = 0;
                for (int index = 0; index < Word(22); index++)
                {
                    int bound = Word(32 + Word(22) * 16 + index * 2);
                    bounds.Add(bound); if (bound <= raw) value = bound;
                }
            }
            if (kind == 2) { bounds.Add(Word(48)); bounds.Add(Word(50)); value = (Word(48) + Word(50)) / 2; }
            if (kind is 0 or 4) value = 0;
            if (title.Contains("Sound", StringComparison.OrdinalIgnoreCase)) value = 0;
            if (title.TrimEnd(':').Equals("Color", StringComparison.OrdinalIgnoreCase) && kind == 5) value = 1;
            values[resource.Id.Number!.Value - 1] = checked((ushort)value);
            controls.Add(new { Id = resource.Id.Number, Kind = kind, Title = title, RawDefault = raw, ProbeValue = value, Bounds = bounds, Choices = choices });
        }
        report["Controls"] = controls;
        return values;
    }
}
