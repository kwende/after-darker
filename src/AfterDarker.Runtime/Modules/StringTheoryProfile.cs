using System.Buffers.Binary;
using AfterDarker.Core.AfterDark;
using AfterDarker.Core.Ne;

namespace AfterDarker.Runtime;

/// <summary>String Theory's controls and observed globals; its x86 code owns all motion and history updates.</summary>
/// <remarks>See docs/research/string-theory-execution.md for artifact offsets and verification.</remarks>
internal sealed class StringTheoryProfile : ModuleProfile<StringTheoryState>
{
    private const ushort ThreeGroupsChoice = 2, HundredStringsControl = 60, ColorSpeed = 96, ClearFirst = 1;
    private const int CompatibilityOffset = 0x18, InstanceOffset = 0x3EA, GroupCountOffset = 0x10;
    private const int HistoryHandleOffset = 0x12, HistoryPointerOffset = 0x14, HistoryLengthOffset = 0x3EC;
    private const int HistoryIndexOffset = 0x456, ColorIntervalOffset = 0x45A, MotionCountOffset = 0x3E6;
    private const int FirstGroupColorOffset = 0x406, GroupRecordBytes = 26;
    private const int SystemPointerOffset = 0x3E2, ModulePointerOffset = 0x45C;

    public override string Name => "String Theory";
    public override string Sha256 => SupportedModules.StringTheorySha256;
    public override bool AllowLocalHeapGrowth => true;

    public override void ValidateOptions(PlaybackOptions options)
    {
        base.ValidateOptions(options);
        // Initialization chooses endpoints with dimension - 2 as a divisor.
        if (options.Width <= 2 || options.Height <= 2)
            throw new ArgumentException("String Theory requires width and height of at least 3 pixels.");
    }

    public override (byte[] System, byte[] Module) CreateRecords(PlaybackOptions options) =>
        StandardModuleRecords.Create(options, ThreeGroupsChoice, HundredStringsControl, ColorSpeed, ClearFirst);

    public override StringTheoryState Observe(byte[] dataBytes)
    {
        ushort groups = ReadWord(dataBytes, GroupCountOffset);
        if (groups > 4) throw new InvalidOperationException("String Theory's group count exceeds its four static group records.");
        var colors = Array.AsReadOnly(Enumerable.Range(0, groups)
            .Select(group => ReadWord(dataBytes, FirstGroupColorOffset + group * GroupRecordBytes)).ToArray());
        return new(ReadWord(dataBytes, CompatibilityOffset), ReadWord(dataBytes, InstanceOffset), groups,
            ReadWord(dataBytes, HistoryHandleOffset), ReadWord(dataBytes, HistoryPointerOffset),
            ReadWord(dataBytes, HistoryLengthOffset), ReadWord(dataBytes, HistoryIndexOffset),
            ReadWord(dataBytes, ColorIntervalOffset), ReadWord(dataBytes, MotionCountOffset), colors,
            ReadPointer(dataBytes, SystemPointerOffset), ReadPointer(dataBytes, ModulePointerOffset));
    }

    public override ushort Instance(StringTheoryState state) => state.InstanceHandle;
    public override ushort Compatibility(StringTheoryState state) => state.Compatibility;

    public override void ValidateInitialized(StringTheoryState state, PlaybackOptions options,
        ushort hostDataSelector, uint? lastReturnedTick)
    {
        if (state.GroupCount != 3 || state.HistoryLength != 100 || state.ColorInterval != 20 ||
            state.HistoryHandle == 0 || state.HistoryOffset == 0 || state.HistoryIndex != 0 || state.MotionCount != 0 ||
            state.System != new FarPointer16(hostDataSelector, SessionMemoryLayout.SystemOffset) ||
            state.Module != new FarPointer16(hostDataSelector, SessionMemoryLayout.ModuleOffset))
            throw new InvalidOperationException("String Theory initialization disagrees with its controls or allocated history.");
    }

    public override void ValidateBlankResult(ushort result)
    {
        // Shared true-color policy: consume guest COLORREFs without indexed palette emulation.
        if (result != AfterDarkHostContract.HueSaturationPaletteRequest)
            throw new NotSupportedException($"String Theory returned unexpected BLANK result {result}.");
    }

    private static ushort ReadWord(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset));
    private static FarPointer16 ReadPointer(byte[] bytes, int offset) => new(ReadWord(bytes, offset + 2), ReadWord(bytes, offset));
}
