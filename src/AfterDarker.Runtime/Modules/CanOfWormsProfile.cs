namespace AfterDarker.Runtime;

/// <summary>Can of Worms's fixed color configuration and verified global locations.</summary>
/// <remarks>Weavy motion, eleven segments, ten worms, sound off. See docs/research/bitmap-module-family.md.</remarks>
internal sealed class CanOfWormsProfile : BitmapModuleProfile
{
    public override string Name => "Can of Worms";
    public override string Sha256 => SupportedModules.CanOfWormsSha256;
    protected override int CompatibilityOffset => 0x26;
    protected override int InstanceOffset => 0x3FE;
    protected override int DeviceContextOffset => 0x3EA;
    protected override int SystemPointerOffset => 0x3F8;
    protected override int ModulePointerOffset => 0x11C2;
    protected override ushort[] Controls => [50, 11, 10, 0]; // Weavy motion, eleven segments, ten worms, sound off.
    public override byte[] CreateInitialPixels(PlaybackOptions options) => WhiteBackground(options);
}
