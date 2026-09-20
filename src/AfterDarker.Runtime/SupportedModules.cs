using System.Security.Cryptography;
using AfterDarker.Core.AfterDark;

namespace AfterDarker.Runtime;

/// <summary>Verified artifact identities, not file names or a claim of general AD compatibility.</summary>
public static class SupportedModules
{
    /// <summary>Exact Shapes artifact verified with Color and Clear Screen First enabled.</summary>
    public const string ShapesSha256 = "16E51D41E4BA8E05EBF9ED5E4A59FDCFD32255634304E38DAC2BA93AE1C4C67C";
    /// <summary>Exact Hard Rain artifact verified with five drops, size 20 and Clear Screen First.</summary>
    public const string HardRainSha256 = "0A93389EFC588962EAF0FE683836EFCB934BF1D036B2316FF202212A0800B331";
    /// <summary>Exact String Theory artifact verified with three groups of 100 strings.</summary>
    public const string StringTheorySha256 = "76A6905FBA156A2B510EE645511B74EA7DECE650BAA62781941D4FE95B9B66B8";
    /// <summary>Exact Zot! artifact verified with Few forks and Stormy frequency.</summary>
    public const string ZotSha256 = "B1C71AC07BD0CB55BF520B5E2BD69127726206136FE0AE7BB85A4B04ABB04E76";
    /// <summary>Exact Magic artifact verified with a 100-line trail and horizontal mirroring.</summary>
    public const string MagicSha256 = "777DFD7BEA0084E3947EFCD2E4E3E3ACD9BA22445F51D6AB8C19EFC7ABCC2763";
    /// <summary>Exact Lasers artifact verified with three rays and its movable local-heap history.</summary>
    public const string LasersSha256 = "8B800EABA0E1F121EF52947BB116FC6254724CEEB5E460315DF0F21E347AC430";
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
        LasersSha256 => "Lasers",
        MagicSha256 => "Magic",
        StringTheorySha256 => "String Theory",
        ZotSha256 => "Zot!",
        HardRainSha256 => "Hard Rain",
        ShapesSha256 => "Shapes",
        _ => throw new NotSupportedException("This AD file is not supported yet. Supported modules: the analyzed Mondrian, Spiral Gyra, Rainstorm, Fade Away, Lasers, Magic, String Theory, Zot!, Hard Rain and Shapes versions. File names alone do not identify a supported version.")
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
            "Lasers" => new AfterDarkSession<LasersState>(file, new LasersProfile(), options,
                instructionLimit: 200_000, timing: timing, diagnostics: diagnostics, output: output),
            "Magic" => new AfterDarkSession<MagicState>(file, new MagicProfile(), options,
                timing: timing, diagnostics: diagnostics, output: output),
            "String Theory" => new AfterDarkSession<StringTheoryState>(file, new StringTheoryProfile(), options,
                instructionLimit: 200_000, timing: timing, diagnostics: diagnostics, output: output),
            "Zot!" => new AfterDarkSession<ZotState>(file, new ZotProfile(), options,
                instructionLimit: 2_000_000, timing: timing, diagnostics: diagnostics, output: output),
            "Hard Rain" => new AfterDarkSession<HardRainState>(file, new HardRainProfile(), options,
                timing: timing, diagnostics: diagnostics, output: output),
            "Shapes" => new AfterDarkSession<ShapesState>(file, new ShapesProfile(), options,
                timing: timing, diagnostics: diagnostics, output: output),
            _ => throw new InvalidOperationException()
        };
}
