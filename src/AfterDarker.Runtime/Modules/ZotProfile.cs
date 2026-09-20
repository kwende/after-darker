using System.Buffers.Binary;
using AfterDarker.Core.Ne;

namespace AfterDarker.Runtime;

/// <summary>Zot!'s fixed storm controls and original bolt-allocation observations.</summary>
/// <remarks>See docs/research/zot-execution.md for timing, transient flashes and artifact-specific offsets.</remarks>
internal sealed class ZotProfile : ModuleProfile<ZotState>
{
    private const ushort FewForks = 33, Stormy = 100;
    private const int CompatibilityOffset = 0x1E, InstanceOffset = 0x3E0;
    private const int ForkDivisorOffset = 0x14, FrequencyOffset = 0x18, NextStrikeOffset = 0x1A;
    private const int MainHandleOffset = 0x3D2, ForkHandleOffset = 0x3D4;
    private const int MainCountOffset = 0x364, ForkCountOffset = 0x366;
    private const int SystemPointerOffset = 0x3DC, ModulePointerOffset = 0x3E2;

    public override string Name => "Zot!";
    public override string Sha256 => SupportedModules.ZotSha256;
    public override bool AllowLocalHeapGrowth => true;
    public override int ServiceExitLimit => 4096;
    // The original 100,000-iteration delay executes hundreds of thousands of
    // instructions without an import. Let it run instead of patching out the loop.
    public override TimeSpan NativeSliceTimeout => TimeSpan.FromSeconds(3);
    // Both procedures restore the stock pen and delete their temporary pen.
    // Capture after that import returns: one completed white/gray bolt, or its
    // black erasure. The x86 delay loops and all drawing still execute normally.
    public override IReadOnlyList<ImportFrameCheckpoint> FrameCheckpoints { get; } = Array.AsReadOnly(new[]
    {
        new ImportFrameCheckpoint(new(2, 0x0771), new("GDI", 69, null), TimeSpan.FromMilliseconds(80)),
        new ImportFrameCheckpoint(new(2, 0x063F), new("GDI", 69, null), TimeSpan.FromMilliseconds(80))
    });

    public override (byte[] System, byte[] Module) CreateRecords(PlaybackOptions options) =>
        StandardModuleRecords.Create(options, FewForks, 0, Stormy, 0);

    public override ZotState Observe(byte[] dataBytes) => new(
        ReadWord(dataBytes, CompatibilityOffset), ReadWord(dataBytes, InstanceOffset),
        ReadWord(dataBytes, ForkDivisorOffset), ReadWord(dataBytes, FrequencyOffset),
        BinaryPrimitives.ReadUInt32LittleEndian(dataBytes.AsSpan(NextStrikeOffset)),
        ReadWord(dataBytes, MainHandleOffset), ReadWord(dataBytes, ForkHandleOffset),
        ReadWord(dataBytes, MainCountOffset), ReadWord(dataBytes, ForkCountOffset),
        ReadPointer(dataBytes, SystemPointerOffset), ReadPointer(dataBytes, ModulePointerOffset));

    public override ushort Instance(ZotState state) => state.InstanceHandle;
    public override ushort Compatibility(ZotState state) => state.Compatibility;

    public override void ValidateInitialized(ZotState state, PlaybackOptions options,
        ushort hostDataSelector, uint? lastReturnedTick)
    {
        if (state.ForkDivisor != 21 || state.Frequency != Stormy ||
            state.MainHandle != 0 || state.ForkHandle != 0 || lastReturnedTick is null ||
            unchecked(state.NextStrike - lastReturnedTick.Value) is < 500 or > 1999 ||
            state.System != new FarPointer16(hostDataSelector, SessionMemoryLayout.SystemOffset) ||
            state.Module != new FarPointer16(hostDataSelector, SessionMemoryLayout.ModuleOffset))
            throw new InvalidOperationException("Zot! initialization disagrees with its controls or scheduled strike.");
    }

    private static ushort ReadWord(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset));
    private static FarPointer16 ReadPointer(byte[] bytes, int offset) => new(ReadWord(bytes, offset + 2), ReadWord(bytes, offset));
}
