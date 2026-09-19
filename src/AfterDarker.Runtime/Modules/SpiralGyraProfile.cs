using System.Buffers.Binary;
using AfterDarker.Core.AfterDark;
using AfterDarker.Core.Ne;

namespace AfterDarker.Runtime;

/// <summary>Spiral Gyra's control meanings and observed globals, isolated from execution mechanics.</summary>
/// <remarks>Offsets refer only to the hashed artifact in docs/research/spiral-gyra-execution.md.</remarks>
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
        WriteWord(system, AfterDarkHostContract.ColorDepth, 24);
        byte[] module = new byte[12];
        WriteWord(module, RegionWidthOffset, (ushort)options.Width);
        WriteWord(module, RegionHeightOffset, (ushort)options.Height);
        // These are NOT Mondrian's speed/clear fields. Spiral reads angle,
        // length percentage and speed percentage at S2:056A..05C0.
        WriteWord(module, AngleControlOffset, 270);
        WriteWord(module, LengthControlOffset, 40);
        WriteWord(module, SpeedControlOffset, options.Speed);
        return (system, module);
    }
    // Artifact-specific data offsets, anchored by the static path notes.
    private const int Compatible = 0x66, InstanceHandle = 0xB1C, Width = 0x30, Height = 0x34;
    private const int LastTick = 0x476, RandomSeed = 0x192, ConvertedTime = 0x492;
    private const int SystemPointer = 0x4D6, ModulePointer = 0xB28, BlackPen = 0xB20, DrawingPen = 0x7FA;
    private const int RegionWidthOffset = 2;
    private const int RegionHeightOffset = 4;
    private const int AngleControlOffset = 6;
    private const int LengthControlOffset = 8;
    private const int SpeedControlOffset = 10;

    public override SpiralGyraState Observe(byte[] dataBytes) => new(
        Compatibility: ReadWord(dataBytes, Compatible),
        InstanceHandle: ReadWord(dataBytes, InstanceHandle),
        Width: ReadWord(dataBytes, Width),
        Height: ReadWord(dataBytes, Height),
        Tick: ReadDoubleWord(dataBytes, LastTick),
        Seed: ReadDoubleWord(dataBytes, RandomSeed),
        Time: ReadDoubleWord(dataBytes, ConvertedTime),
        System: ReadFarPointer(dataBytes, SystemPointer),
        Module: ReadFarPointer(dataBytes, ModulePointer),
        BlackPen: ReadWord(dataBytes, BlackPen),
        DrawingPen: ReadWord(dataBytes, DrawingPen));

    public override ushort Instance(SpiralGyraState state) => state.InstanceHandle;
    public override ushort Compatibility(SpiralGyraState state) => state.Compatibility;

    public override void ValidateInitialized(SpiralGyraState state, PlaybackOptions options,
        ushort hostDataSelector, uint? lastReturnedTick)
    {
        var expectedSystem = new FarPointer16(hostDataSelector, SessionMemoryLayout.SystemOffset);
        var expectedModule = new FarPointer16(hostDataSelector, SessionMemoryLayout.ModuleOffset);
        bool dimensionsMatch = state.Width == options.Width && state.Height == options.Height;
        bool clockMatches = state.Tick == lastReturnedTick;
        bool recordPointersMatch = state.System == expectedSystem && state.Module == expectedModule;
        bool ownsDistinctPens = state.BlackPen != 0 && state.DrawingPen != 0 && state.BlackPen != state.DrawingPen;

        if (!dimensionsMatch || !clockMatches || !recordPointersMatch || !ownsDistinctPens)
        {
            throw new InvalidOperationException("Spiral Gyra initialization disagrees with its host contract.");
        }
    }

    /// <summary>Read one Win16 word without depending on host structure packing.</summary>
    private static ushort ReadWord(byte[] bytes, int offset) =>
        BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset));

    /// <summary>Read a four-byte guest integer in x86 byte order.</summary>
    private static uint ReadDoubleWord(byte[] bytes, int offset) =>
        BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset));

    /// <summary>A stored far pointer has offset first, then selector; its display order is selector:offset.</summary>
    private static FarPointer16 ReadFarPointer(byte[] bytes, int offset) =>
        new(ReadWord(bytes, offset + sizeof(ushort)), ReadWord(bytes, offset));

    /// <summary>Write a guest control word explicitly, retaining the current narrow record size.</summary>
    private static void WriteWord(byte[] bytes, int offset, ushort value) =>
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset), value);
}
