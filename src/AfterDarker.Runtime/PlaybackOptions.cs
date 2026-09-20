using AfterDarker.Core.AfterDark;

namespace AfterDarker.Runtime;

/// <summary>Host settings offered by the current player; each module profile maps them to guest controls.</summary>
/// <param name="Width">Drawing width in guest pixels, before WPF presentation scaling.</param>
/// <param name="Height">Drawing height in guest pixels, before WPF presentation scaling.</param>
/// <param name="Speed">One of the five percentages for Mondrian/Spiral Gyra; Rainstorm, Fade Away and Lasers use fixed controls.</param>
/// <param name="Clear">Mondrian's clear-screen option; other supported modules do not use this setting.</param>
public sealed record PlaybackOptions(short Width = 640, short Height = 480, ushort Speed = 100, bool Clear = true)
{
    /// <summary>Reject dimensions and speeds outside the current tested player surface.</summary>
    public void Validate()
    {
        if (Width is < 1 or > 2048 || Height is < 1 or > 2048 || Speed is not (0 or 25 or 50 or 75 or 100))
            throw new ArgumentException("Playback requires dimensions 1..2048 and speed 0/25/50/75/100.");
    }
    /// <summary>Preserve the options used by the original Mondrian lessons.</summary>
    public static PlaybackOptions From(MondrianInitialization.Options options) =>
        new(options.Width, options.Height, options.Speed, options.Clear);

    /// <summary>Adapt these host options to the educational Mondrian contract.</summary>
    public MondrianInitialization.Options ForMondrian() => new(Width, Height, Speed, Clear);
}
