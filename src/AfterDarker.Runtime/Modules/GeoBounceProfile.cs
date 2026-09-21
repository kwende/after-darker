namespace AfterDarker.Runtime;

/// <summary>GeoBounce's fixed color configuration and verified global locations.</summary>
/// <remarks>Tetrahedron, size 55, speed 50, colored and shaded faces. See docs/research/bitmap-module-family.md.</remarks>
internal sealed class GeoBounceProfile : BitmapModuleProfile
{
    public override string Name => "GeoBounce";
    public override string Sha256 => SupportedModules.GeoBounceSha256;
    protected override int CompatibilityOffset => 0x55A;
    protected override int InstanceOffset => 0xB30;
    protected override int DeviceContextOffset => 0xA08;
    protected override int SystemPointerOffset => 0xA18;
    protected override int ModulePointerOffset => 0xB50;
    protected override ushort[] Controls => [0, 55, 50, 2]; // Tetrahedron, size 55, speed 50, colored and shaded faces.
}
