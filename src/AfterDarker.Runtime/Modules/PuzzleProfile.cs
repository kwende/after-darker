namespace AfterDarker.Runtime;

/// <summary>Puzzle's original sliding tiles, with small tiles, slow motion, sound off and no inversion.</summary>
/// <remarks>Global offsets come from the exact artifact's S1 dispatcher. See docs/research/puzzle-execution.md.</remarks>
internal sealed class PuzzleProfile : BitmapModuleProfile
{
    public override string Name => "Puzzle";
    public override string Sha256 => SupportedModules.PuzzleSha256;
    protected override int CompatibilityOffset => 0x22;
    protected override int InstanceOffset => 0x414;
    protected override int DeviceContextOffset => 0x3F4;
    protected override int SystemPointerOffset => 0x3FA;
    protected override int ModulePointerOffset => 0x41C;
    protected override ushort[] Controls => [0, 0, 0, 0];

    /// <summary>Provide a visible, generated image to scramble; this replaces a captured desktop, not the animation.</summary>
    public override byte[] CreateInitialPixels(PlaybackOptions options)
    {
        byte[] pixels = new byte[options.Width * options.Height * 3];
        for (int row = 0; row < options.Height; row++)
            for (int column = 0; column < options.Width; column++)
            {
                int offset = (row * options.Width + column) * 3;
                pixels[offset] = (byte)(40 + column * 200 / options.Width);
                pixels[offset + 1] = (byte)(40 + row * 200 / options.Height);
                pixels[offset + 2] = (byte)(((column / 24 + row / 24) % 2 == 0) ? 210 : 60);
            }
        return pixels;
    }
}
