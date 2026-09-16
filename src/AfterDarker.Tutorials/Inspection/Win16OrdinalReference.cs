namespace AfterDarker.Tutorials.Inspection;

/// <summary>
/// Display-only annotations derived from the existing Wine 10.0 import census.
/// These names/kinds are reference evidence, never bytes recovered from the input.
/// This is not a callable ABI registry or an implementation of any import.
/// </summary>
internal static class Win16OrdinalReference
{
    private static readonly Dictionary<(string Module, ushort Ordinal), (string Name, string Kind)> Names = Load();

    public static string Describe(string module, ushort ordinal) =>
        Names.TryGetValue((module.ToUpperInvariant(), ordinal), out var known)
            ? $" [reference: {known.Name}; {known.Kind}]" : " [name/ABI unknown]";

    private static Dictionary<(string, ushort), (string, string)> Load()
    {
        using Stream stream = typeof(Win16OrdinalReference).Assembly.GetManifestResourceStream(
            "AfterDarker.Tutorials.Inspection.Win16OrdinalNames.tsv")!;
        using StreamReader reader = new(stream);
        Dictionary<(string, ushort), (string, string)> result = [];
        while (reader.ReadLine() is string line)
        {
            if (line.StartsWith('#')) continue;
            string[] columns = line.Split('\t');
            result.Add((columns[0], ushort.Parse(columns[1])), (columns[2], columns[3]));
        }
        return result;
    }
}
