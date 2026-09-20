using System.Buffers.Binary;
using AfterDarker.Core.AfterDark;
using AfterDarker.Core.Ne;
using AfterDarker.Core.Win16;

namespace AfterDarker.Runtime;

/// <summary>Rainstorm's fixed controls and artifact-specific observations; all execution stays in the shared session.</summary>
/// <remarks>See docs/research/rainstorm-execution.md for the disassembly anchors and supported settings.</remarks>
internal sealed class RainstormProfile : ModuleProfile<RainstormState>
{
    // Explicit host choices, not a reconstruction of the original slider defaults.
    private const ushort Strength = 60, Lightning = 50, Drops = 52, Wind = 40;
    private const int InitialLightningCountdown = 425 - 4 * Lightning;
    private const int ModuleRecordBytes = 0x22;
    private const int RegionWidthOffset = 2, RegionHeightOffset = 4;
    private const int FirstControlValueOffset = 6, FirstControlIdOffset = 0x0E;

    // Data-segment locations from this exact artifact, not part of the SDK ABI.
    private const int CompatibilityOffset = 0x10, InstanceOffset = 0x3B2;
    private const int DropBoundsOffset = 0x398, DropCountOffset = 0x3A6;
    private const int StrengthOffset = 0x3B0, WindOffset = 0x3B4, LightningCountdownOffset = 0x3AC;
    private const int SystemPointerOffset = 0x3A8, ModulePointerOffset = 0x9F6;

    public override string Name => "Rainstorm";
    public override string Sha256 => SupportedModules.RainstormSha256;
    // 52 drops can each issue geometry, drawing and pen-lifetime calls in one frame.
    public override int ServiceExitLimit => 1024;

    public override (byte[] System, byte[] Module) CreateRecords(PlaybackOptions options)
    {
        byte[] system = AfterDarkHostContract.CreateSystemRecord();
        WriteWord(system, AfterDarkHostContract.ColorDepth, 24);
        byte[] module = new byte[ModuleRecordBytes];
        WriteWord(module, RegionWidthOffset, (ushort)options.Width);
        WriteWord(module, RegionHeightOffset, (ushort)options.Height);
        ushort[] controlValues = [Strength, Lightning, Drops, Wind];
        for (int controlIndex = 0; controlIndex < controlValues.Length; controlIndex++)
        {
            WriteWord(module, FirstControlValueOffset + controlIndex * sizeof(ushort), controlValues[controlIndex]);
            WriteWord(module, FirstControlIdOffset + controlIndex * sizeof(ushort), (ushort)(controlIndex + 1));
        }
        // Full SDK allocation; unused region, palette and sound fields stay zero.
        // Rainstorm has no speed slider. PlaybackOptions.Speed does not alter these controls.
        return (system, module);
    }

    public override RainstormState Observe(byte[] bytes) => new(
        ReadWord(bytes, CompatibilityOffset), ReadWord(bytes, InstanceOffset),
        Rectangle16.Decode(bytes.AsSpan(DropBoundsOffset, Rectangle16.ByteCount)),
        ReadWord(bytes, DropCountOffset), ReadWord(bytes, StrengthOffset),
        unchecked((short)ReadWord(bytes, WindOffset)), unchecked((short)ReadWord(bytes, LightningCountdownOffset)),
        ReadPointer(bytes, SystemPointerOffset), ReadPointer(bytes, ModulePointerOffset));

    public override ushort Instance(RainstormState state) => state.InstanceHandle;
    public override ushort Compatibility(RainstormState state) => state.Compatibility;

    public override void ValidateInitialized(RainstormState state, PlaybackOptions options,
        ushort hostDataSelector, uint? lastReturnedTick)
    {
        var expectedBounds = new Rectangle16(-25, -25, (short)(options.Width + 5), (short)(options.Height + 5));
        bool controlsMatch = state.Strength == Strength && state.DropCount == Drops &&
            Math.Abs((int)state.Wind) == Wind / 10 && state.LightningCountdown == InitialLightningCountdown;
        bool pointersMatch = state.System == new FarPointer16(hostDataSelector, SessionMemoryLayout.SystemOffset) &&
            state.Module == new FarPointer16(hostDataSelector, SessionMemoryLayout.ModuleOffset);
        if (!controlsMatch || !pointersMatch || state.DropBounds != expectedBounds)
            throw new InvalidOperationException("Rainstorm initialization disagrees with its host records and fixed controls.");
    }

    /// <summary>Read an artifact word without relying on host structure packing.</summary>
    private static ushort ReadWord(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset));
    /// <summary>Stored far pointers have offset first, selector second.</summary>
    private static FarPointer16 ReadPointer(byte[] bytes, int offset) => new(ReadWord(bytes, offset + 2), ReadWord(bytes, offset));
    /// <summary>Serialize a guest word in x86 byte order.</summary>
    private static void WriteWord(byte[] bytes, int offset, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset), value);
}
