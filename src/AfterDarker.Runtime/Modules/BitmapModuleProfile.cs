using System.Buffers.Binary;
using AfterDarker.Core.AfterDark;
using AfterDarker.Core.Ne;

namespace AfterDarker.Runtime;

/// <summary>Common SDK inputs and observations for modules drawing through off-screen bitmaps.</summary>
/// <remarks>Each derived profile names its own controls and artifact-specific global offsets.
/// No bitmap operation or animation algorithm belongs in a profile. See docs/research/bitmap-module-family.md.</remarks>
internal abstract class BitmapModuleProfile : ModuleProfile<BitmapModuleState>
{
    protected abstract int CompatibilityOffset { get; }
    protected abstract int InstanceOffset { get; }
    protected abstract int DeviceContextOffset { get; }
    protected abstract int SystemPointerOffset { get; }
    protected abstract int ModulePointerOffset { get; }
    protected abstract ushort[] Controls { get; }
    public override int ServiceExitLimit => 4096;
    public override void ValidateOptions(PlaybackOptions options)
    {
        base.ValidateOptions(options);
        if (options.Width < 128 || options.Height < 128)
            throw new ArgumentOutOfRangeException(nameof(options), "This fixed bitmap-module profile requires at least 128x128 pixels.");
    }
    public override (byte[] System, byte[] Module) CreateRecords(PlaybackOptions options)
    {
        var records = StandardModuleRecords.Create(options, Controls);
        // AD_SYSTEM describes the desktop; AD_MODULE describes the drawing area.
        // Our single-surface host supplies the same dimensions to both records.
        BinaryPrimitives.WriteInt16LittleEndian(records.System.AsSpan(AfterDarkHostContract.ScreenWidth), options.Width);
        BinaryPrimitives.WriteInt16LittleEndian(records.System.AsSpan(AfterDarkHostContract.ScreenHeight), options.Height);
        BinaryPrimitives.WriteUInt16LittleEndian(records.System.AsSpan(AfterDarkHostContract.PixelAspectX), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(records.System.AsSpan(AfterDarkHostContract.PixelAspectY), 1);
        return records;
    }
    public override BitmapModuleState Observe(byte[] bytes)
    {
        ushort Word(int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset));
        FarPointer16 Pointer(int offset) => new(Word(offset + 2), Word(offset));
        return new(Word(CompatibilityOffset), Word(InstanceOffset), Word(DeviceContextOffset),
            Pointer(SystemPointerOffset), Pointer(ModulePointerOffset));
    }
    public override ushort Instance(BitmapModuleState state) => state.InstanceHandle;
    public override ushort Compatibility(BitmapModuleState state) => state.Compatibility;
    public override void ValidateInitialized(BitmapModuleState state, PlaybackOptions options, ushort hostDataSelector, uint? lastReturnedTick)
    {
        if (state.DeviceContext != AfterDarkHostContract.ReservedHdc ||
            state.System != new FarPointer16(hostDataSelector, SessionMemoryLayout.SystemOffset) ||
            state.Module != new FarPointer16(hostDataSelector, SessionMemoryLayout.ModuleOffset))
            throw new InvalidOperationException($"{Name} did not retain the supplied drawing context and SDK records.");
    }
    /// <summary>Owned white input for modules designed to consume an existing desktop image.</summary>
    protected static byte[] WhiteBackground(PlaybackOptions options)
    {
        byte[] pixels = new byte[options.Width * options.Height * 3];
        Array.Fill(pixels, (byte)255);
        return pixels;
    }
}
