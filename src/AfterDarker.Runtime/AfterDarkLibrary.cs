using System.Security.Cryptography;

namespace AfterDarker.Runtime;

/// <summary>A supported original and its user-owned location. No file bytes are retained by discovery.</summary>
public sealed record LocalAfterDarkArtifact(SupportedArtifact Identity, string Path);

/// <summary>Discovery results, including readable reasons for skipped inputs. Scanning never runs guest code.</summary>
public sealed record AfterDarkDiscovery(IReadOnlyList<LocalAfterDarkArtifact> Artifacts, IReadOnlyList<string> Diagnostics);

/// <summary>Find supported revisions recursively, preserving distinct bytes and deduplicating identical copies.</summary>
public static class AfterDarkLibrary
{
    /// <summary>Bound file reads well above current supported module sizes.</summary>
    public const int MaximumFileBytes = 4 * 1024 * 1024;

    /// <summary>Scan off the calling thread. Unreadable directories and reparse points are skipped.</summary>
    public static Task<AfterDarkDiscovery> DiscoverAsync(string directory, CancellationToken stop = default) => Task.Run(() =>
    {
        var artifacts = new List<LocalAfterDarkArtifact>();
        var diagnostics = new List<string>();
        var hashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            return new AfterDarkDiscovery(artifacts, ["Choose an existing After Dark module folder in settings."]);
        try
        {
            var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true,
                AttributesToSkip = FileAttributes.ReparsePoint, MaxRecursionDepth = 32 };
            int visited = 0;
            foreach (string path in Directory.EnumerateFiles(directory, "*", options))
            {
                stop.ThrowIfCancellationRequested();
                if (++visited > 10000) { diagnostics.Add("Stopped after 10,000 files; choose a smaller collection folder."); break; }
                if (!Path.GetExtension(path).Equals(".ad", StringComparison.OrdinalIgnoreCase)) continue;
                try
                {
                    byte[] bytes = ReadBounded(path);
                    var identity = SupportedModules.FindByHash(Convert.ToHexString(SHA256.HashData(bytes)));
                    if (identity is null) diagnostics.Add($"Unsupported revision: {path}");
                    else if (hashes.Add(identity.Sha256)) artifacts.Add(new(identity, Path.GetFullPath(path)));
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                { diagnostics.Add($"Skipped {path}: {error.Message}"); }
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
        { diagnostics.Add($"Unable to finish discovery: {error.Message}"); }
        return new AfterDarkDiscovery(artifacts.AsReadOnly(), diagnostics.AsReadOnly());
    }, stop);

    /// <summary>Read and revalidate the actual selected bytes, detecting replacement after discovery.</summary>
    public static byte[] ReadVerified(LocalAfterDarkArtifact artifact)
    {
        byte[] bytes = ReadBounded(artifact.Path);
        if (!Convert.ToHexString(SHA256.HashData(bytes)).Equals(artifact.Identity.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"The discovered {artifact.Identity.DisplayName} file has changed; rescan the collection.");
        return bytes;
    }

    private static byte[] ReadBounded(string path)
    {
        using var stream = File.OpenRead(path);
        if (stream.Length is < 1 or > MaximumFileBytes) throw new InvalidDataException("Module size is outside the discovery limit.");
        byte[] bytes = new byte[(int)stream.Length];
        stream.ReadExactly(bytes);
        return bytes;
    }
}
