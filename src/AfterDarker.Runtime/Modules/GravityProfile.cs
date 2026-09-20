using System.Buffers.Binary;
using AfterDarker.Core.AfterDark;
using AfterDarker.Core.Ne;

namespace AfterDarker.Runtime;

/// <summary>Fixed color/silent Gravity configuration and artifact-specific observations.</summary>
/// <remarks>See docs/research/gravity-execution.md. All sound and graphics services are shared implementations.</remarks>
internal sealed class GravityProfile : ModuleProfile<GravityState>
{
    private const ushort BallCount = 4, BallSize = 20;
    private const ushort ClearScreenEnabled = 1, SoundDisabled = 0;
    // These are observations of this exact module's DGROUP, not SDK fields.
    // Keep the names beside the addresses so interpreting a snapshot requires
    // no knowledge of the disassembly. The execution guide records the evidence.
    private const int CompatibilityOffset = 0x1C;
    private const int SoundHandleOffset = 0x3A2;
    private const int BallCountOffset = 0x3A4;
    private const int DeviceContextOffset = 0x3AA;
    private const int BallSizeOffset = 0x3AC;
    private const int BitmapHandleOffset = 0x3AE;
    private const int SystemPointerOffset = 0x3B0;
    private const int BallRecordsOffset = 0x3B4;
    private const int BallRecordBytes = 28, MaximumBallCount = 7;
    private const int BallColorOffset = 0, BallXOffset = 4, BallYOffset = 6;
    private const int InstanceHandleOffset = 0x4CC;
    private const int ModulePointerOffset = 0x4D0;
    public override string Name => "Gravity";
    public override string Sha256 => SupportedModules.GravitySha256;
    public override int ServiceExitLimit => 512;
    public override void ValidateOptions(PlaybackOptions options)
    {
        base.ValidateOptions(options);
        if (options.Width < 64 || options.Height < 64)
            throw new ArgumentOutOfRangeException(nameof(options), "Gravity's supported four-ball profile requires at least 64x64 pixels.");
    }
    public override (byte[] System, byte[] Module) CreateRecords(PlaybackOptions options)
    {
        var records = StandardModuleRecords.Create(options, BallCount, BallSize, ClearScreenEnabled, SoundDisabled);
        // Original initialization divides by ptAspect.x when computing its radius.
        // The software surface has square pixels, just as in the Hard Rain profile.
        BinaryPrimitives.WriteUInt16LittleEndian(records.System.AsSpan(AfterDarkHostContract.PixelAspectX), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(records.System.AsSpan(AfterDarkHostContract.PixelAspectY), 1);
        return records;
    }
    public override GravityState Observe(byte[] bytes)
    {
        ushort count = Word(bytes, BallCountOffset);
        if (count > MaximumBallCount) throw new InvalidOperationException("Gravity ball count exceeds the artifact's seven fixed records.");
        var balls = Enumerable.Range(0, count).Select(index =>
        {
            int offset = BallRecordsOffset + index * BallRecordBytes;
            return new GravityBall(BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset + BallColorOffset)),
                unchecked((short)Word(bytes, offset + BallXOffset)), unchecked((short)Word(bytes, offset + BallYOffset)));
        }).ToArray();
        return new(Word(bytes, CompatibilityOffset), Word(bytes, InstanceHandleOffset), Word(bytes, DeviceContextOffset),
            Pointer(bytes, SystemPointerOffset), Pointer(bytes, ModulePointerOffset), count,
            Word(bytes, BallSizeOffset), Word(bytes, BitmapHandleOffset), Word(bytes, SoundHandleOffset), balls);
    }
    public override ushort Instance(GravityState state) => state.InstanceHandle;
    public override ushort Compatibility(GravityState state) => state.Compatibility;
    public override void ValidateInitialized(GravityState state, PlaybackOptions options, ushort hostDataSelector, uint? lastReturnedTick)
    {
        if (state.BallCount != BallCount || state.BallSize != BallSize || state.Bitmap == 0 || state.Sound != 0 ||
            state.DeviceContext != AfterDarkHostContract.ReservedHdc ||
            state.System != new FarPointer16(hostDataSelector, SessionMemoryLayout.SystemOffset) ||
            state.Module != new FarPointer16(hostDataSelector, SessionMemoryLayout.ModuleOffset))
            throw new InvalidOperationException("Gravity initialization disagrees with the supplied silent four-ball profile.");
    }
    private static ushort Word(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset));
    private static FarPointer16 Pointer(byte[] bytes, int offset) => new(Word(bytes, offset + 2), Word(bytes, offset));
}
