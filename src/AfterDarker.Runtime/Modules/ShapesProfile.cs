using System.Buffers.Binary;
using AfterDarker.Core.AfterDark;
using AfterDarker.Core.Ne;

namespace AfterDarker.Runtime;

/// <summary>Shapes' fixed color/clear controls and observed state; rendering and execution stay shared.</summary>
/// <remarks>Static anchors and the direct-RGB palette policy: docs/research/shapes-execution.md.</remarks>
internal sealed class ShapesProfile : ModuleProfile<ShapesState>
{
    private const int CompatibilityOffset = 0x10, InstanceOffset = 0x388, DeviceContextOffset = 0x382;
    private const int RandomSeedOffset = 0x52, SystemPointerOffset = 0x384, ModulePointerOffset = 0x38A;
    private const ushort ClearScreenFirst = 1, ColorEnabled = 1;

    public override string Name => "Shapes";
    public override string Sha256 => SupportedModules.ShapesSha256;

    public override void ValidateOptions(PlaybackOptions options)
    {
        base.ValidateOptions(options);
        // S2:0132 skips coordinate initialization when both dimensions <= 4.
        // Otherwise S2:0191/01B4 divide by half the width/height. Requiring 5
        // avoids both uninitialized stack coordinates and zero divisors.
        if (options.Width < 5 || options.Height < 5)
            throw new ArgumentOutOfRangeException(nameof(options), "Shapes requires dimensions of at least 5x5.");
    }

    public override (byte[] System, byte[] Module) CreateRecords(PlaybackOptions options) =>
        StandardModuleRecords.Create(options, ClearScreenFirst, ColorEnabled, 0, 0);

    /// <summary>Accept the RGB palette request while drawing PALETTERGB's RGB components directly.</summary>
    public override void ValidateBlankResult(ushort result)
    {
        // The host advertises 24-bit color. No indexed palette or guest pointer
        // is manufactured. The original DRAWFRAME still chooses every color.
        if (result != AfterDarkHostContract.RgbPaletteRequest)
            throw new NotSupportedException($"Shapes returned unexpected BLANK result {result}.");
    }

    public override ShapesState Observe(byte[] bytes) => new(
        ReadWord(bytes, CompatibilityOffset), ReadWord(bytes, InstanceOffset), ReadWord(bytes, DeviceContextOffset),
        BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(RandomSeedOffset)),
        ReadPointer(bytes, SystemPointerOffset), ReadPointer(bytes, ModulePointerOffset));

    public override ushort Instance(ShapesState state) => state.InstanceHandle;
    public override ushort Compatibility(ShapesState state) => state.Compatibility;

    public override void ValidateInitialized(ShapesState state, PlaybackOptions options,
        ushort hostDataSelector, uint? lastReturnedTick)
    {
        if (state.DeviceContext != AfterDarkHostContract.ReservedHdc ||
            state.System != new FarPointer16(hostDataSelector, SessionMemoryLayout.SystemOffset) ||
            state.Module != new FarPointer16(hostDataSelector, SessionMemoryLayout.ModuleOffset))
            throw new InvalidOperationException("Shapes initialization disagrees with the supplied host records.");
    }

    private static ushort ReadWord(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset));
    private static FarPointer16 ReadPointer(byte[] bytes, int offset) => new(ReadWord(bytes, offset + 2), ReadWord(bytes, offset));
}
