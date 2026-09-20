using System.Buffers.Binary;
using AfterDarker.Core.AfterDark;
using AfterDarker.Core.Ne;

namespace AfterDarker.Runtime;

/// <summary>Lasers' fixed controls and original globals; heap and drawing implementations stay shared.</summary>
/// <remarks>See docs/research/lasers-execution.md and docs/win16-local-heap.md.</remarks>
internal sealed class LasersProfile : ModuleProfile<LasersState>
{
    private const ushort RaysChoice = 2, WidthControl = 35, ColorSpeed = 50, ClearFirst = 1;
    private const int CompatibilityOffset = 0x16, InstanceOffset = 0x3FA;
    private const int RayCountOffset = 0x10, HistoryLengthOffset = 0x3F0, ColorIntervalOffset = 0x4B0;
    private const int HistoryHandleOffset = 0x12, HistoryPointerOffset = 0x14, DrawCountOffset = 0x3F6;
    private const int SystemPointerOffset = 0x3E6, ModulePointerOffset = 0x4B2;

    public override string Name => "Lasers";
    public override string Sha256 => SupportedModules.LasersSha256;
    public override bool AllowLocalHeapGrowth => true;
    // CLOSE drains 30 trail positions; periodic DRAWFRAME regeneration does the same.
    public override int ServiceExitLimit => 1024;

    public override void ValidateOptions(PlaybackOptions options)
    {
        base.ValidateOptions(options);
        // Original initialization divides by (dimension - 140) to choose its origin.
        if (options.Width <= 140 || options.Height <= 140)
            throw new ArgumentException("Lasers requires width and height of at least 141 pixels for its original origin calculation.");
    }

    public override (byte[] System, byte[] Module) CreateRecords(PlaybackOptions options)
    {
        byte[] system = AfterDarkHostContract.CreateSystemRecord();
        WriteWord(system, AfterDarkHostContract.ColorDepth, 24);
        byte[] module = new byte[0x22]; // Full SDK AD_MODULE, with unused fields zero.
        WriteWord(module, 2, (ushort)options.Width);
        WriteWord(module, 4, (ushort)options.Height);
        ushort[] controls = [RaysChoice, WidthControl, ColorSpeed, ClearFirst];
        for (int control = 0; control < controls.Length; control++)
        {
            WriteWord(module, 6 + 2 * control, controls[control]);
            WriteWord(module, 0x0E + 2 * control, (ushort)(control + 1));
        }
        return (system, module);
    }

    public override LasersState Observe(byte[] bytes) => new(ReadWord(bytes, CompatibilityOffset),
        ReadWord(bytes, InstanceOffset), ReadWord(bytes, RayCountOffset), ReadWord(bytes, HistoryLengthOffset),
        ReadWord(bytes, ColorIntervalOffset), ReadWord(bytes, HistoryHandleOffset), ReadWord(bytes, HistoryPointerOffset),
        BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(DrawCountOffset)),
        ReadPointer(bytes, SystemPointerOffset), ReadPointer(bytes, ModulePointerOffset));

    public override ushort Instance(LasersState state) => state.InstanceHandle;
    public override ushort Compatibility(LasersState state) => state.Compatibility;

    public override void ValidateInitialized(LasersState state, PlaybackOptions options,
        ushort hostDataSelector, uint? lastReturnedTick)
    {
        if (state.RayCount != RaysChoice + 1 || state.HistoryLength != 30 || state.ColorInterval != 250 ||
            state.RayHistoryHandle == 0 || state.RayHistoryOffset == 0 || state.DrawCount != 0 ||
            state.System != new FarPointer16(hostDataSelector, SessionMemoryLayout.SystemOffset) ||
            state.Module != new FarPointer16(hostDataSelector, SessionMemoryLayout.ModuleOffset))
            throw new InvalidOperationException("Lasers initialization disagrees with its records or allocated ray history.");
    }

    public override void ValidateBlankResult(ushort result)
    {
        // SDK HSV_PAL = 10 asks the original host for a hue/saturation palette.
        // Our explicit 24-bit RGB path consumes the guest's COLORREFs directly.
        // No palette handles, palette realization or indexed-color behavior is claimed.
        if (result != AfterDarkHostContract.HueSaturationPaletteRequest)
            throw new NotSupportedException($"Lasers returned unexpected BLANK result {result}.");
    }

    private static ushort ReadWord(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset));
    private static FarPointer16 ReadPointer(byte[] bytes, int offset) => new(ReadWord(bytes, offset + 2), ReadWord(bytes, offset));
    private static void WriteWord(byte[] bytes, int offset, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset), value);
}
