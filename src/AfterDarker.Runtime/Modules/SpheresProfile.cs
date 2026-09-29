using System.Buffers.Binary;
using AfterDarker.Core.AfterDark;
using AfterDarker.Core.Ne;

namespace AfterDarker.Runtime;

/// <summary>Fixed color controls and inspected globals for the AD30 Spheres artifact.</summary>
/// <remarks>One DRAWFRAME draws one ellipse band, not an entire sphere. See docs/research/spheres-execution.md.</remarks>
internal sealed class SpheresProfile : ModuleProfile<SpheresState>
{
    private const ushort MaximumSize = 55, ShadingOffset = 5, ClearEvery = 100, ClearScreenFirst = 1;
    private const int CompatibilityOffset = 0x009E, InstanceOffset = 0x0444, DeviceContextOffset = 0x042C;
    private const int RandomSeedOffset = 0x00E2, SystemPointerOffset = 0x0432, ModulePointerOffset = 0x0450;
    private const int SphereNumberOffset = 0x0438, NextBandOffset = 0x043E, RadiusOffset = 0x042A;
    private const int CenterXOffset = 0x044C, CenterYOffset = 0x044E;

    public override string Name => "Spheres";
    public override string Sha256 => SupportedModules.SpheresSha256;

    public override (byte[] System, byte[] Module) CreateRecords(PlaybackOptions options)
    {
        var records = StandardModuleRecords.Create(options, MaximumSize, ShadingOffset, ClearEvery, ClearScreenFirst);
        // The guest scales each ellipse's horizontal radius by ptAspect.x / ptAspect.y.
        // Square pixels are a modern host choice; leaving these words zero divides by zero.
        BinaryPrimitives.WriteUInt16LittleEndian(records.System.AsSpan(AfterDarkHostContract.PixelAspectX), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(records.System.AsSpan(AfterDarkHostContract.PixelAspectY), 1);
        return records;
    }

    /// <summary>Accept this module's PRIMARY_PAL request on our 24-bit direct-RGB surface.</summary>
    public override void ValidateBlankResult(ushort result)
    {
        // No palette handle is invented. PALETTERGB retains its RGB components in
        // the shared GDI implementation, and the guest still chooses every shade.
        if (result != AfterDarkHostContract.PrimaryPaletteRequest)
            throw new NotSupportedException($"Spheres returned unexpected BLANK result {result}.");
    }

    public override SpheresState Observe(byte[] bytes) => new(
        ReadWord(bytes, CompatibilityOffset), ReadWord(bytes, InstanceOffset), ReadWord(bytes, DeviceContextOffset),
        BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(RandomSeedOffset)),
        ReadPointer(bytes, SystemPointerOffset), ReadPointer(bytes, ModulePointerOffset),
        ReadWord(bytes, SphereNumberOffset), ReadWord(bytes, NextBandOffset), ReadWord(bytes, RadiusOffset),
        ReadWord(bytes, CenterXOffset), ReadWord(bytes, CenterYOffset));

    public override ushort Instance(SpheresState state) => state.InstanceHandle;
    public override ushort Compatibility(SpheresState state) => state.Compatibility;

    public override void ValidateInitialized(SpheresState state, PlaybackOptions options,
        ushort hostDataSelector, uint? lastReturnedTick)
    {
        if (state.DeviceContext != AfterDarkHostContract.ReservedHdc ||
            state.System != new FarPointer16(hostDataSelector, SessionMemoryLayout.SystemOffset) ||
            state.Module != new FarPointer16(hostDataSelector, SessionMemoryLayout.ModuleOffset) ||
            state.SphereNumber != 1 || state.NextBand != 0 || state.Radius is < 30 or > 84 ||
            state.CenterX >= options.Width || state.CenterY >= options.Height)
            throw new InvalidOperationException("Spheres initialization disagrees with the supplied host records or fixed controls.");
    }

    private static ushort ReadWord(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset));
    private static FarPointer16 ReadPointer(byte[] bytes, int offset) => new(ReadWord(bytes, offset + 2), ReadWord(bytes, offset));
}
