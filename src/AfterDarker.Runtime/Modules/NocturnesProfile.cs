namespace AfterDarker.Runtime;

/// <summary>Nocturnes's fixed color configuration and verified global locations.</summary>
/// <remarks>Colored eyes with moderate density. Unused template controls stay zero. See docs/research/bitmap-module-family.md.</remarks>
internal sealed class NocturnesProfile : BitmapModuleProfile
{
    public override string Name => "Nocturnes";
    public override string Sha256 => SupportedModules.NocturnesSha256;
    protected override int CompatibilityOffset => 0x4E;
    protected override int InstanceOffset => 0x55C;
    protected override int DeviceContextOffset => 0x424;
    protected override int SystemPointerOffset => 0x42A;
    protected override int ModulePointerOffset => 0x568;
    // The resource labels for the first two controls are leftovers from another module.
    // The observed drawing code reads Color and Density, not "Flying Objects" or "Toast".
    protected override ushort[] Controls => [0, 0, 1, 50];
}
