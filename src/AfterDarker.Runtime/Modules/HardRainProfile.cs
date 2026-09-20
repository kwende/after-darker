using System.Buffers.Binary;
using AfterDarker.Core.AfterDark;
using AfterDarker.Core.Ne;

namespace AfterDarker.Runtime;

/// <summary>Hard Rain's fixed controls and typed observations; execution and drawing remain shared.</summary>
/// <remarks>Static anchors, rendering policy and proof limits: docs/research/hard-rain-execution.md.</remarks>
internal sealed class HardRainProfile : ModuleProfile<HardRainState>
{
    // Validated host choices within the resource ranges, not recovered saved settings.
    private const ushort DropCount = 5, DropSize = 20, ClearScreenFirst = 1;
    private const int CompatibilityOffset = 0x14, InstanceOffset = 0x502;
    private const int DropCountOffset = 0x12, DropSizeOffset = 0x10, NextDropOffset = 0x504;
    private const int DropArrayOffset = 0x382, DropRecordBytes = 18, MaximumDrops = 9;
    private const int SystemPointerOffset = 0x4FE, ModulePointerOffset = 0x506;

    public override string Name => "Hard Rain";
    public override string Sha256 => SupportedModules.HardRainSha256;

    public override (byte[] System, byte[] Module) CreateRecords(PlaybackOptions options)
    {
        var records = StandardModuleRecords.Create(options, DropCount, DropSize, 0, ClearScreenFirst);
        // S2:0301..0331 computes radius * ptAspect.y / ptAspect.x.
        // Our software surface has square pixels. A zero-filled ratio would
        // cause a real divide-by-zero in the module's 32-bit integer helper.
        WriteWord(records.System, AfterDarkHostContract.PixelAspectX, 1);
        WriteWord(records.System, AfterDarkHostContract.PixelAspectY, 1);
        return records;
    }

    public override HardRainState Observe(byte[] bytes)
    {
        ushort count = ReadWord(bytes, DropCountOffset);
        if (count > MaximumDrops) throw new InvalidOperationException("Hard Rain's drop count exceeds its static array capacity.");
        var drops = new HardRainDrop[count];
        for (int index = 0; index < drops.Length; index++)
        {
            int offset = DropArrayOffset + index * DropRecordBytes;
            uint color = ReadWord(bytes, offset) | (uint)ReadWord(bytes, offset + 2) << 8 | (uint)ReadWord(bytes, offset + 4) << 16;
            drops[index] = new(color, (short)ReadWord(bytes, offset + 6), (short)ReadWord(bytes, offset + 8),
                ReadDword(bytes, offset + 10), ReadDword(bytes, offset + 14));
        }
        return new(ReadWord(bytes, CompatibilityOffset), ReadWord(bytes, InstanceOffset), count,
            ReadWord(bytes, DropSizeOffset), ReadWord(bytes, NextDropOffset), Array.AsReadOnly(drops),
            ReadPointer(bytes, SystemPointerOffset), ReadPointer(bytes, ModulePointerOffset));
    }

    public override ushort Instance(HardRainState state) => state.InstanceHandle;
    public override ushort Compatibility(HardRainState state) => state.Compatibility;

    public override void ValidateInitialized(HardRainState state, PlaybackOptions options,
        ushort hostDataSelector, uint? lastReturnedTick)
    {
        bool controlsMatch = state.DropCount == DropCount && state.DropSize == DropSize && state.NextDrop == 0;
        bool dropsMatch = state.Drops.All(drop => drop.Radius == 4 && drop.MaximumRadius is >= 7 and <= 26 &&
            drop.CenterX >= 0 && drop.CenterX < options.Width && drop.CenterY >= 0 && drop.CenterY < options.Height &&
            drop.Color is 0x0000FF or 0xFF00FF or 0xFF0000 or 0xFFFF00 or 0x00FF00 or 0x00FFFF);
        bool pointersMatch = state.System == new FarPointer16(hostDataSelector, SessionMemoryLayout.SystemOffset) &&
            state.Module == new FarPointer16(hostDataSelector, SessionMemoryLayout.ModuleOffset);
        if (!controlsMatch || !dropsMatch || !pointersMatch)
            throw new InvalidOperationException("Hard Rain's initialization disagrees with its controls and drop-record layout.");
    }

    private static ushort ReadWord(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset));
    private static uint ReadDword(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset));
    private static FarPointer16 ReadPointer(byte[] bytes, int offset) => new(ReadWord(bytes, offset + 2), ReadWord(bytes, offset));
    private static void WriteWord(byte[] bytes, int offset, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset), value);
}
