namespace AfterDarker.Runtime;

/// <summary>An original module revision accepted by the runtime, independent of its local filename.</summary>
/// <param name="Id">Stable original-module identity, scoped to After Darker.</param>
/// <param name="DisplayName">Human-readable name of the original.</param>
/// <param name="Sha256">Exact supported bytes; other revisions remain unsupported.</param>
public sealed record SupportedArtifact(string Id, string DisplayName, string Sha256);
