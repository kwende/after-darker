using System.Buffers.Binary;
using AfterDarker.Core.AfterDark;
using AfterDarker.Core.Ne;

namespace AfterDarker.Runtime;

/// <summary>Artifact-specific controls and observations; Windows drawing behavior remains in shared services.</summary>
/// <remarks>See docs/research/stained-glass-execution.md for evidence and the supported configuration.</remarks>
internal sealed class StainedGlassProfile : ModuleProfile<StainedGlassState>
{
    private const int CompatibilityOffset = 0x198, InstanceOffset = 0x8B4, DeviceContextOffset = 0x892;
    private const int SystemPointerOffset = 0x8A6, ModulePointerOffset = 0x8C2, ColorDisplayOffset = 0x10;
    private const int ComplexityOffset = 0x4B0, DuplicationOffset = 0x4B2, ColorOffset = 0x4B4;
    private const ushort Complexity = 10, Duplication = 100, Color = 100;

    public override string Name => "Stained Glass";
    public override string Sha256 => SupportedModules.StainedGlassSha256;
    // A single pattern step may issue many rectangle helpers and blits. Keep a
    // finite per-invocation ceiling; this is not a total lifetime call allowance.
    public override int ServiceExitLimit => 4096;
    public override void ValidateOptions(PlaybackOptions options)
    {
        base.ValidateOptions(options);
        // Pattern construction divides the viewport into small nested cells.
        // Keep this first supported profile within the exercised size range.
        if (options.Width < 64 || options.Height < 64)
            throw new ArgumentOutOfRangeException(nameof(options), "Stained Glass requires dimensions of at least 64x64.");
    }
    public override (byte[] System, byte[] Module) CreateRecords(PlaybackOptions options) =>
        StandardModuleRecords.Create(options, Complexity, Duplication, Color, 0);

    public override StainedGlassState Observe(byte[] bytes) => new(
        Word(bytes, CompatibilityOffset), Word(bytes, InstanceOffset), Word(bytes, DeviceContextOffset),
        Pointer(bytes, SystemPointerOffset), Pointer(bytes, ModulePointerOffset), Word(bytes, ColorDisplayOffset),
        Word(bytes, ComplexityOffset), Word(bytes, DuplicationOffset), Word(bytes, ColorOffset));
    public override ushort Instance(StainedGlassState state) => state.InstanceHandle;
    public override ushort Compatibility(StainedGlassState state) => state.Compatibility;
    public override void ValidateInitialized(StainedGlassState state, PlaybackOptions options, ushort hostDataSelector, uint? lastReturnedTick)
    {
        if (state.DeviceContext != AfterDarkHostContract.ReservedHdc || state.ColorDisplay != 1 ||
            state.System != new FarPointer16(hostDataSelector, SessionMemoryLayout.SystemOffset) ||
            state.Module != new FarPointer16(hostDataSelector, SessionMemoryLayout.ModuleOffset))
            throw new InvalidOperationException("Stained Glass initialization disagrees with the supplied color host records.");
    }

    private static ushort Word(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset));
    private static FarPointer16 Pointer(byte[] bytes, int offset) => new(Word(bytes, offset + 2), Word(bytes, offset));
}
