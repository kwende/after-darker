using AfterDarker.Core.Ne;
using AfterDarker.Core.Rendering;

namespace AfterDarker.Tutorials.Lessons;

/// <summary>Inspect resource aliases and turn NE bitmap payloads into typed RGB images, without running guest code.</summary>
/// <remarks>Put breakpoints at Find and Decode to follow the directory-to-pixels boundary.
/// The same catalog and decoder back the real Win16 LoadBitmap handler. See docs/ne-bitmap-resources.md.</remarks>
public sealed class Tutorial11BitmapResources(string? inputPath = null, string? outputDirectory = null) : ITutorial
{
    public string Id => "11";
    public string Title => "Follow an NE bitmap resource from its name to its pixels";

    /// <summary>One resource's directory metadata and decoded image, ready for a renderer or debugger.</summary>
    public sealed record ResourceBitmap(NeResource Resource, DecodedBitmap Bitmap);

    /// <summary>Decode all standard RT_BITMAP entries using the production implementation.</summary>
    public static IReadOnlyList<ResourceBitmap> DecodeBitmaps(NeImage image, NeResourceCatalog catalog) =>
        Array.AsReadOnly(image.Resources.Where(resource => resource.Type.Number == NeFormat.ResourceTypes.Bitmap).Select(resource =>
            new ResourceBitmap(resource, DibBitmapDecoder.Decode(catalog.Find(resource.Type, resource.Id) ??
                throw new InvalidDataException("Resource directory and catalog disagree.")))).ToArray());

    public void Run()
    {
        string? path = inputPath;
        if (string.IsNullOrWhiteSpace(path))
        {
            Console.Write("Path to a local .AD / NE file: ");
            path = Console.ReadLine()?.Trim().Trim('"');
        }
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Supply a local module for lesson 11.");
        byte[] file = File.ReadAllBytes(path);
        NeImage image = NeReader.Read(file);
        var catalog = new NeResourceCatalog(file, image);
        foreach (NeResourceAlias alias in catalog.Aliases)
            Console.WriteLine($"Alias: {Label(alias.Type)} / {Label(alias.Id)} -> type #{alias.TypeNumber}, ID #{alias.IdNumber}");

        IReadOnlyList<ResourceBitmap> bitmaps = DecodeBitmaps(image, catalog);
        string destination = Path.GetFullPath(outputDirectory ?? Path.Combine("artifacts", "resource-bitmaps"));
        if (bitmaps.Count > 0) Directory.CreateDirectory(destination);
        for (int index = 0; index < bitmaps.Count; index++)
        {
            ResourceBitmap item = bitmaps[index];
            Console.WriteLine($"BITMAP {Label(item.Resource.Id)}: file +0x{item.Resource.FileOffset:X}, {item.Resource.Length} stored bytes");
            Console.WriteLine($"  DIB -> {item.Bitmap.Width}x{item.Bitmap.Height}, {item.Bitmap.SourceBitsPerPixel} bits/pixel -> {item.Bitmap.Rgb.Length} RGB bytes");
            string outputPath = Path.Combine(destination, $"bitmap-{index + 1:D3}.png");
            using var output = File.Create(outputPath);
            PngWriter.Write(output, item.Bitmap.Width, item.Bitmap.Height, item.Bitmap.Rgb);
            Console.WriteLine($"  {outputPath}");
        }
        Console.WriteLine($"Decoded {bitmaps.Count} bitmap resources. No CPU engine or guest handles were needed.");
    }

    private static string Label(NeResourceIdentifier identifier) => identifier.Number is ushort number ? $"#{number}" : identifier.Name ?? "<missing>";
}
