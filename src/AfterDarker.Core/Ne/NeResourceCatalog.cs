namespace AfterDarker.Core.Ne;

/// <summary>Owns immutable file bytes and resolves the parser's resource directory to bounded payloads.</summary>
/// <remarks>Resource bytes are file data, not executable segments or guest pointers. See docs/ne-bitmap-resources.md.</remarks>
public sealed class NeResourceCatalog
{
    private readonly byte[] file;
    private readonly NeResource[] resources;
    /// <summary>Decoded legacy aliases, such as Nocturnes' EYES name for bitmap ID 1.</summary>
    public IReadOnlyList<NeResourceAlias> Aliases { get; }
    /// <summary>Snapshot both inputs so a later caller mutation cannot change resource loading.</summary>
    public NeResourceCatalog(ReadOnlySpan<byte> fileBytes, NeImage image)
    {
        file = fileBytes.ToArray();
        resources = image.Resources.ToArray();
        foreach (var resource in resources)
            if (resource.FileOffset < 0 || resource.Length < 0 || (long)resource.FileOffset + resource.Length > file.Length)
                throw new InvalidDataException("NE resource lies outside the source file.");
        Aliases = Array.AsReadOnly(resources.Where(resource => resource.Type.Number == NeFormat.ResourceTypes.NameTable)
            .SelectMany(resource => NeResourceAlias.Decode(file.AsSpan(resource.FileOffset, resource.Length))).ToArray());
    }
    /// <summary>Copy an identified payload; names compare without case and missing resources return null.</summary>
    public byte[]? Find(NeResourceIdentifier type, NeResourceIdentifier identifier)
    {
        if (type.Number is null || identifier.Number is null)
        {
            var aliases = Aliases.Where(alias => Matches(alias.Type, type) && Matches(alias.Id, identifier)).ToArray();
            if (aliases.Length > 1) throw new InvalidDataException("Ambiguous RT_NAMETABLE alias.");
            if (aliases.Length == 1)
            {
                type = new(aliases[0].TypeNumber, null);
                identifier = new(aliases[0].IdNumber, null);
            }
        }
        var matches = resources.Where(resource => Matches(resource.Type, type) && Matches(resource.Id, identifier)).ToArray();
        if (matches.Length > 1) throw new InvalidDataException("Ambiguous duplicate NE resource identity.");
        return matches.Length == 0 ? null : file.AsSpan(matches[0].FileOffset, matches[0].Length).ToArray();
    }
    private static bool Matches(NeResourceIdentifier first, NeResourceIdentifier second) =>
        first.Number is ushort number ? second.Number == number : second.Number is null &&
        first.Name is not null && string.Equals(first.Name, second.Name, StringComparison.OrdinalIgnoreCase);
}
