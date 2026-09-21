namespace AfterDarker.Runtime;

/// <summary>Punch Out's fixed color configuration and verified global locations.</summary>
/// <remarks>Circular punches, size 40, speed 5, sound off. See docs/research/bitmap-module-family.md.</remarks>
internal sealed class PunchOutProfile : BitmapModuleProfile
{
    public override string Name => "Punch Out";
    public override string Sha256 => SupportedModules.PunchOutSha256;
    protected override int CompatibilityOffset => 0x34;
    protected override int InstanceOffset => 0x3B4;
    protected override int DeviceContextOffset => 0x392;
    protected override int SystemPointerOffset => 0x39A;
    protected override int ModulePointerOffset => 0x3C2;
    protected override ushort[] Controls => [0, 40, 5, 0]; // Circular punches, size 40, speed 5, sound off.
    public override byte[] CreateInitialPixels(PlaybackOptions options) => WhiteBackground(options);
    public override void ValidateOptions(PlaybackOptions options)
    {
        base.ValidateOptions(options);
        // The guest allocates a full-screen bitmap with a ten-pixel border on each side.
        if (options.Width > 2028 || options.Height > 2028)
            throw new ArgumentOutOfRangeException(nameof(options), "Punch Out requires dimensions at most 2028 (2048 minus its bitmap border).");
    }
}
