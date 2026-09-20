using System.Buffers.Binary;
using AfterDarker.Core.AfterDark;
using AfterDarker.Core.Ne;

namespace AfterDarker.Runtime;

/// <summary>Fade Away's Radar settings, white starting image and artifact-specific state checks.</summary>
/// <remarks>Windows calls and the lifecycle remain shared; see docs/research/fade-away-execution.md.</remarks>
internal sealed class FadeAwayProfile : ModuleProfile<FadeAwayState>
{
    private const ushort RadarStyle = 4;
    private const int ModuleRecordBytes = 0x22;
    private const int RegionWidthOffset = 2, RegionHeightOffset = 4;
    private const int FirstControlValueOffset = 6, FirstControlIdOffset = 0x0E;

    // These are globals in this exact binary, not SDK structure offsets.
    private const int CompatibilityOffset = 0x18, InstanceOffset = 0x3A0;
    private const int StyleOffset = 0x39E, FinishedOffset = 0x10;
    private const int StepOffset = 0x394, EdgeOffset = 0x3A2;
    private const int EndpointXOffset = 0x39A, EndpointYOffset = 0x39C;
    private const int SystemPointerOffset = 0x396, ModulePointerOffset = 0x3A4;

    public override string Name => "Fade Away";
    public override string Sha256 => SupportedModules.FadeAwaySha256;

    public override byte[] CreateInitialPixels(PlaybackOptions options)
    {
        byte[] whiteImage = new byte[checked(options.Width * options.Height * 3)];
        Array.Fill(whiteImage, byte.MaxValue);
        return whiteImage;
    }

    public override (byte[] System, byte[] Module) CreateRecords(PlaybackOptions options)
    {
        byte[] system = AfterDarkHostContract.CreateSystemRecord();
        WriteWord(system, AfterDarkHostContract.ColorDepth, 24);
        byte[] module = new byte[ModuleRecordBytes];
        WriteWord(module, RegionWidthOffset, (ushort)options.Width);
        WriteWord(module, RegionHeightOffset, (ushort)options.Height);
        WriteWord(module, FirstControlValueOffset, RadarStyle);
        for (int controlIndex = 0; controlIndex < 4; controlIndex++)
            WriteWord(module, FirstControlIdOffset + controlIndex * sizeof(ushort), (ushort)(controlIndex + 1));
        // Other controls are unused by Radar; palette, sound and region fields stay zero.
        // There is no speed control. The host paces calls; the guest advances its own sweep.
        return (system, module);
    }

    public override FadeAwayState Observe(byte[] bytes) => new(
        ReadWord(bytes, CompatibilityOffset), ReadWord(bytes, InstanceOffset), ReadWord(bytes, StyleOffset),
        ReadWord(bytes, FinishedOffset) != 0, ReadWord(bytes, StepOffset), ReadWord(bytes, EdgeOffset),
        new(unchecked((short)ReadWord(bytes, EndpointXOffset)), unchecked((short)ReadWord(bytes, EndpointYOffset))),
        ReadPointer(bytes, SystemPointerOffset), ReadPointer(bytes, ModulePointerOffset));

    public override ushort Instance(FadeAwayState state) => state.InstanceHandle;
    public override ushort Compatibility(FadeAwayState state) => state.Compatibility;

    public override void ValidateInitialized(FadeAwayState state, PlaybackOptions options,
        ushort hostDataSelector, uint? lastReturnedTick)
    {
        bool sweepStartsAtTop = state.Endpoint.X == options.Width / 2 && state.Endpoint.Y == 0 &&
            state.Step == 2 && state.Edge == 0 && !state.Finished && state.Style == RadarStyle;
        bool pointersMatch = state.System == new FarPointer16(hostDataSelector, SessionMemoryLayout.SystemOffset) &&
            state.Module == new FarPointer16(hostDataSelector, SessionMemoryLayout.ModuleOffset);
        if (!sweepStartsAtTop || !pointersMatch)
            throw new InvalidOperationException("Fade Away initialization disagrees with its Radar host contract.");
    }

    /// <summary>Read a guest word independently of host memory packing.</summary>
    private static ushort ReadWord(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset));
    /// <summary>Stored far pointers place the offset before the selector.</summary>
    private static FarPointer16 ReadPointer(byte[] bytes, int offset) => new(ReadWord(bytes, offset + 2), ReadWord(bytes, offset));
    /// <summary>Write one SDK word in little-endian order.</summary>
    private static void WriteWord(byte[] bytes, int offset, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset), value);
}
