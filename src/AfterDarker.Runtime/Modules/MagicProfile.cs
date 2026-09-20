using System.Buffers.Binary;
using AfterDarker.Core.AfterDark;
using AfterDarker.Core.Ne;

namespace AfterDarker.Runtime;

/// <summary>Magic's fixed controls and observed globals, with shared heap, gateway and GDI services.</summary>
/// <remarks>Artifact offsets and control conversions are documented in docs/research/magic-execution.md.</remarks>
internal sealed class MagicProfile : ModuleProfile<MagicState>
{
    // These are host choices, not a claim to reproduce saved settings or resource defaults.
    // The Lines slider passes 60 to the module; its own lookup turns that into 100 lines.
    private const ushort LinesControl = 60, LineSpeed = 100, ColorSpeed = 85, HorizontalMirror = 1;
    private const int CompatibilityOffset = 0x16, InstanceOffset = 0x400;
    private const int HistoryHandleOffset = 0x10, HistoryPointerOffset = 0x12;
    private const int LineDelayOffset = 0x3FA, ColorIntervalOffset = 0x41C, MirrorOffset = 0x394;
    private const int HistoryLengthOffset = 0x418, HistoryIndexOffset = 0x3FE;
    private const int MotionCountOffset = 0x3F8, ColorIndexOffset = 0x3EE;
    private const int SystemPointerOffset = 0x3F4, ModulePointerOffset = 0x41E;

    public override string Name => "Magic";
    public override string Sha256 => SupportedModules.MagicSha256;
    public override bool AllowLocalHeapGrowth => true;

    public override void ValidateOptions(PlaybackOptions options)
    {
        base.ValidateOptions(options);
        // S2:0333..035F chooses initial coordinates using dimension - 2 as a divisor.
        if (options.Width <= 2 || options.Height <= 2)
            throw new ArgumentException("Magic requires width and height of at least 3 pixels for its original coordinate calculation.");
    }

    public override (byte[] System, byte[] Module) CreateRecords(PlaybackOptions options)
    {
        byte[] system = AfterDarkHostContract.CreateSystemRecord();
        WriteWord(system, AfterDarkHostContract.ColorDepth, 24);
        byte[] module = new byte[0x22]; // Full SDK AD_MODULE; unused fields remain zero.
        WriteWord(module, 2, (ushort)options.Width);
        WriteWord(module, 4, (ushort)options.Height);
        ushort[] controls = [LinesControl, LineSpeed, ColorSpeed, HorizontalMirror];
        for (int control = 0; control < controls.Length; control++)
        {
            WriteWord(module, 6 + 2 * control, controls[control]);
            WriteWord(module, 0x0E + 2 * control, (ushort)(control + 1));
        }
        return (system, module);
    }

    public override MagicState Observe(byte[] dataBytes) => new(
        Compatibility: ReadWord(dataBytes, CompatibilityOffset),
        InstanceHandle: ReadWord(dataBytes, InstanceOffset),
        HistoryHandle: ReadWord(dataBytes, HistoryHandleOffset),
        HistoryOffset: ReadWord(dataBytes, HistoryPointerOffset),
        HistoryLength: ReadWord(dataBytes, HistoryLengthOffset),
        HistoryIndex: ReadWord(dataBytes, HistoryIndexOffset),
        LineDelay: ReadWord(dataBytes, LineDelayOffset),
        ColorInterval: ReadWord(dataBytes, ColorIntervalOffset),
        Mirror: ReadWord(dataBytes, MirrorOffset),
        MotionCount: ReadWord(dataBytes, MotionCountOffset),
        ColorIndex: ReadWord(dataBytes, ColorIndexOffset),
        System: ReadPointer(dataBytes, SystemPointerOffset),
        Module: ReadPointer(dataBytes, ModulePointerOffset));

    public override ushort Instance(MagicState state) => state.InstanceHandle;
    public override ushort Compatibility(MagicState state) => state.Compatibility;

    public override void ValidateInitialized(MagicState state, PlaybackOptions options,
        ushort hostDataSelector, uint? lastReturnedTick)
    {
        bool controlsMatch = state.HistoryLength == 100 && state.LineDelay == 0 &&
            state.ColorInterval == 75 && state.Mirror == HorizontalMirror;
        bool ownsHistory = state.HistoryHandle != 0 && state.HistoryOffset != 0 && state.HistoryIndex == 0;
        bool recordPointersMatch =
            state.System == new FarPointer16(hostDataSelector, SessionMemoryLayout.SystemOffset) &&
            state.Module == new FarPointer16(hostDataSelector, SessionMemoryLayout.ModuleOffset);
        if (!controlsMatch || !ownsHistory || !recordPointersMatch || state.MotionCount != 0 || state.ColorIndex != 1)
            throw new InvalidOperationException("Magic initialization disagrees with its controls or allocated line history.");
    }

    public override void ValidateBlankResult(ushort result)
    {
        // The same explicit true-color policy used by Spiral Gyra and Lasers:
        // consume guest COLORREFs directly, without claiming indexed palette support.
        if (result != AfterDarkHostContract.HueSaturationPaletteRequest)
            throw new NotSupportedException($"Magic returned unexpected BLANK result {result}.");
    }

    private static ushort ReadWord(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset));
    private static FarPointer16 ReadPointer(byte[] bytes, int offset) => new(ReadWord(bytes, offset + 2), ReadWord(bytes, offset));
    private static void WriteWord(byte[] bytes, int offset, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset), value);
}
