using System.Security.Cryptography;
using AfterDarker.Core.AfterDark;

namespace AfterDarker.Runtime;

/// <summary>Verified artifact identities, not file names or a claim of general AD compatibility.</summary>
public static class SupportedModules
{
    /// <summary>Exact known Spiral Gyra artifact; names and extensions are insufficient evidence.</summary>
    public const string SpiralGyraSha256 = "8098AC464BA204E81719828ECDDB9DC78058231EF105F8B512072C21E3167529";
    /// <summary>Exact Rainstorm artifact exercised by the local integration suite.</summary>
    public const string RainstormSha256 = "41611FED9E314B2F4F1C0653D1A5B948E41E70E464293D2F6489691E4C8DF5C7";
    /// <summary>Exact Fade Away artifact verified with the Radar effect and a white initial image.</summary>
    public const string FadeAwaySha256 = "D224AF9A0EA75F3B4C24A837F9743B13851D3ACB03BE3607E0C9094DF6E28B21";
    /// <summary>Identify a tested module revision by its bytes, rejecting unsupported input before loading.</summary>
    public static string Identify(ReadOnlySpan<byte> file) => Convert.ToHexString(SHA256.HashData(file)) switch
    {
        MondrianInitialization.Sha256 => "Mondrian",
        SpiralGyraSha256 => "Spiral Gyra",
        RainstormSha256 => "Rainstorm",
        FadeAwaySha256 => "Fade Away",
        _ => throw new NotSupportedException("This AD file is not supported yet. Supported modules: the analyzed Mondrian, Spiral Gyra, Rainstorm and Fade Away versions. File names alone do not identify a supported version.")
    };
    /// <summary>Construct the selected profile and shared session without yet executing guest code.</summary>
    public static IAnimationSession Open(byte[] file, PlaybackOptions options, SessionTiming? timing = null,
        DiagnosticOptions? diagnostics = null, TextWriter? output = null) => Identify(file) switch
        {
            "Mondrian" => new MondrianSession(file, options.ForMondrian(), timing: timing, diagnostics: diagnostics, output: output),
            // One Spiral DRAWFRAME executes up to 30 drawing iterations, each with
            // eight line operations plus pen changes and integer math helpers.
            "Spiral Gyra" => new AfterDarkSession<SpiralGyraState>(file, new SpiralGyraProfile(), options,
                instructionLimit: 200_000, timing: timing, diagnostics: diagnostics, output: output),
            "Rainstorm" => new AfterDarkSession<RainstormState>(file, new RainstormProfile(), options,
                instructionLimit: 200_000, timing: timing, diagnostics: diagnostics, output: output),
            "Fade Away" => new AfterDarkSession<FadeAwayState>(file, new FadeAwayProfile(), options,
                timing: timing, diagnostics: diagnostics, output: output),
            _ => throw new InvalidOperationException()
        };
}
