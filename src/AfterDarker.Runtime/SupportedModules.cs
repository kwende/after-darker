using System.Buffers.Binary;
using System.Security.Cryptography;
using AfterDarker.Core.AfterDark;
using AfterDarker.Core.Ne;

namespace AfterDarker.Runtime;

public sealed record PlaybackOptions(short Width = 640, short Height = 480, ushort Speed = 100, bool Clear = true)
{
    public void Validate()
    {
        if (Width is < 1 or > 2048 || Height is < 1 or > 2048 || Speed is not (0 or 25 or 50 or 75 or 100))
            throw new ArgumentException("Playback requires dimensions 1..2048 and speed 0/25/50/75/100.");
    }
    public static PlaybackOptions From(MondrianInitialization.Options options) => new(options.Width, options.Height, options.Speed, options.Clear);
    public MondrianInitialization.Options ForMondrian() => new(Width, Height, Speed, Clear);
}

public interface IAnimationSession : IDisposable
{
    string ModuleName { get; }
    int PixelByteCount { get; }
    void Initialize();
    void Blank();
    void DrawFrame();
    void CopyPixelsTo(Span<byte> destination);
    void Shutdown();
    PlaybackResult GetPlaybackResult();
}
public sealed record PlaybackPhase(string Name, ushort StoredAx, SegmentedGuest.CpuState Registers);
public sealed record PlaybackResult(string ModuleName, IReadOnlyList<PlaybackPhase> Phases, long Instructions,
    int OutstandingLocks, int RetainedCalls, int LivePens, int PeakPens, IReadOnlyDictionary<string, long> ImportCalls);

/// <summary>Verified artifact identities, not file names or a claim of general AD compatibility.</summary>
public static class SupportedModules
{
    public const string SpiralGyraSha256 = "8098AC464BA204E81719828ECDDB9DC78058231EF105F8B512072C21E3167529";
    public static string Identify(ReadOnlySpan<byte> file) => Convert.ToHexString(SHA256.HashData(file)) switch
    {
        MondrianInitialization.Sha256 => "Mondrian",
        SpiralGyraSha256 => "Spiral Gyra",
        _ => throw new NotSupportedException("This AD file is not supported yet. Supported modules: the analyzed Mondrian and Spiral Gyra versions. File names alone do not identify a supported version.")
    };
    public static IAnimationSession Open(byte[] file, PlaybackOptions options, SessionTiming? timing = null,
        DiagnosticOptions? diagnostics = null, TextWriter? output = null) => Identify(file) switch
    {
        "Mondrian" => new MondrianSession(file, options.ForMondrian(), timing: timing, diagnostics: diagnostics, output: output),
        // One Spiral DRAWFRAME executes up to 30 drawing iterations, each with
        // eight line operations plus pen changes and integer math helpers.
        "Spiral Gyra" => new AfterDarkSession<SpiralGyraState>(file, new SpiralGyraProfile(), options,
            instructionLimit: 200_000, timing: timing, diagnostics: diagnostics, output: output),
        _ => throw new InvalidOperationException()
    };
}

/// <summary>Only artifact-specific record layouts and proof checks belong here.
/// CPU setup, relocations, imports and the lifecycle loop are shared.</summary>
public abstract class ModuleProfile<TState>
{
    public abstract string Name { get; }
    public abstract string Sha256 { get; }
    public virtual int ServiceExitLimit => SegmentedGuest.DefaultServiceExitLimit;
    public virtual int CloseServiceExitLimit => ServiceExitLimit;
    public virtual void ValidateOptions(PlaybackOptions options) => options.Validate();
    public abstract (byte[] System, byte[] Module) CreateRecords(PlaybackOptions options);
    public abstract TState Observe(byte[] bytes);
    public abstract ushort Instance(TState state);
    public abstract ushort Compatibility(TState state);
    public abstract void ValidateInitialized(TState state, PlaybackOptions options, ushort hostData, uint? tick);
}

internal sealed class MondrianProfile : ModuleProfile<MondrianInitialization.State>
{
    public override string Name => "Mondrian";
    public override string Sha256 => MondrianInitialization.Sha256;
    public override int CloseServiceExitLimit => 208; // 200 inversions + blanking + lock/unlock services
    public override (byte[], byte[]) CreateRecords(PlaybackOptions options) => MondrianInitialization.CreateRecords(options.ForMondrian());
    public override MondrianInitialization.State Observe(byte[] bytes) => MondrianInitialization.Observe(bytes);
    public override ushort Instance(MondrianInitialization.State state) => state.InstanceHandle;
    public override ushort Compatibility(MondrianInitialization.State state) => state.Compatibility;
    public override void ValidateInitialized(MondrianInitialization.State s, PlaybackOptions o, ushort hostData, uint? tick)
    {
        if (s.Clear != (o.Clear ? 1 : 0) || s.Threshold != o.ForMondrian().ExpectedThreshold || s.Counter != 0 ||
            s.Rectangles != 0 || s.Tick != tick || s.Seed != (ushort)s.Time ||
            s.System != new FarPointer16(hostData, 0x100) || s.Module != new FarPointer16(hostData, 0x200))
            throw new InvalidOperationException("Mondrian initialization disagrees with its host contract.");
    }
}

public sealed record SpiralGyraState(ushort Compatibility, ushort InstanceHandle, ushort Width, ushort Height,
    uint Tick, uint Seed, uint Time, FarPointer16 System, FarPointer16 Module, ushort BlackPen, ushort DrawingPen);

internal sealed class SpiralGyraProfile : ModuleProfile<SpiralGyraState>
{
    public override string Name => "Spiral Gyra";
    public override string Sha256 => SupportedModules.SpiralGyraSha256;
    public override int ServiceExitLimit => 768;
    public override (byte[] System, byte[] Module) CreateRecords(PlaybackOptions options)
    {
        byte[] system = AfterDarkHostContract.CreateSystemRecord();
        // System +0A is the color capability inspected at S2:05C7. At least
        // four enables the guest's own color cycling; 24 describes our RGB surface.
        Put(system, AfterDarkHostContract.ColorDepth, 24);
        byte[] module = new byte[12];
        Put(module, 2, (ushort)options.Width); Put(module, 4, (ushort)options.Height);
        // These are NOT Mondrian's speed/clear fields. Spiral reads angle,
        // length percentage and speed percentage at S2:056A..05C0.
        Put(module, 6, 270); Put(module, 8, 40); Put(module, 10, options.Speed);
        return (system, module);
    }
    // Artifact-specific data offsets, anchored by the static path notes.
    private const int Compatible = 0x66, InstanceHandle = 0xB1C, Width = 0x30, Height = 0x34;
    private const int LastTick = 0x476, RandomSeed = 0x192, ConvertedTime = 0x492;
    private const int SystemPointer = 0x4D6, ModulePointer = 0xB28, BlackPen = 0xB20, DrawingPen = 0x7FA;
    public override SpiralGyraState Observe(byte[] b) => new(Word(b, Compatible), Word(b, InstanceHandle),
        Word(b, Width), Word(b, Height), Dword(b, LastTick), Dword(b, RandomSeed), Dword(b, ConvertedTime),
        Pointer(b, SystemPointer), Pointer(b, ModulePointer), Word(b, BlackPen), Word(b, DrawingPen));
    public override ushort Instance(SpiralGyraState s) => s.InstanceHandle;
    public override ushort Compatibility(SpiralGyraState s) => s.Compatibility;
    public override void ValidateInitialized(SpiralGyraState s, PlaybackOptions o, ushort hostData, uint? tick)
    {
        if (s.Width != o.Width || s.Height != o.Height || s.Tick != tick ||
            s.System != new FarPointer16(hostData, 0x100) || s.Module != new FarPointer16(hostData, 0x200) ||
            s.BlackPen == 0 || s.DrawingPen == 0 || s.BlackPen == s.DrawingPen)
            throw new InvalidOperationException("Spiral Gyra initialization disagrees with its host contract.");
    }
    private static ushort Word(byte[] b, int o) => BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(o));
    private static uint Dword(byte[] b, int o) => BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(o));
    private static FarPointer16 Pointer(byte[] b, int o) => new(Word(b, o + 2), Word(b, o));
    private static void Put(byte[] b, int o, ushort v) => BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(o), v);
}
