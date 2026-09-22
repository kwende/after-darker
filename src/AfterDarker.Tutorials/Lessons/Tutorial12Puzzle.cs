using System.Text.Json;
using AfterDarker.Core.Ne;
using AfterDarker.Core.Rendering;
using AfterDarker.Runtime;

namespace AfterDarker.Tutorials.Lessons;

/// <summary>Run a previously unsupported original module using hand rolled scrolling.</summary>
/// <remarks>See docs/research/puzzle-execution.md. Break in Win16Api.ScrollDC and Win16Drawing.ScrollDC.</remarks>
public sealed class Tutorial12Puzzle(string? inputPath = null, string? outputDirectory = null) : ITutorial
{
    public string Id => "12";
    public string Title => "Play original Puzzle with hand rolled ScrollDC";

    /// <summary>Measured animation and complete import counts after guest shutdown.</summary>
    public sealed record CaptureResult(int Draws, int ChangedFrames, PlaybackResult Playback);

    public void Run()
    {
        string root = FindRepositoryRoot();
        string path = inputPath ?? Path.Combine(root, "ad", "windows98-2026-09-20", "AFTERDRK", "AD30", "PUZZLE.AD");
        using var input = File.OpenRead(path);
        if (input.Length > NeReader.MaximumFileBytes) throw new InvalidDataException("Module exceeds the NE input limit.");
        byte[] file = new byte[(int)input.Length]; input.ReadExactly(file);
        string directory = outputDirectory ?? Path.Combine(root, "artifacts", "puzzle",
            DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..8]);
        if (Directory.Exists(directory)) throw new IOException("Choose a new capture directory; prior evidence is preserved.");
        Directory.CreateDirectory(directory);
        Console.WriteLine("Original Puzzle chooses the tiles and their movement; our hand rolled ScrollDC moves the pixels.");
        Console.WriteLine($"Captures: {Path.GetFullPath(directory)}");
        CaptureResult result = Capture(file, directory);
        File.WriteAllText(Path.Combine(directory, "report.json"), JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Completed {result.Draws} draws: {result.ChangedFrames} changed images, clean CLOSE/WEP and disposal.");
        foreach (var import in result.Playback.ImportCalls) Console.WriteLine($"  {import.Key}: {import.Value}");
    }

    /// <summary>Capture thirty animation images plus the initial patterned host image.</summary>
    public static CaptureResult Capture(byte[] file, string directory)
    {
        const short width = 320, height = 240;
        using var session = (AfterDarkSession<BitmapModuleState>)SupportedModules.Open(file,
            new(width, height));
        session.Initialize(); session.Blank();
        Save("initial.png", session.CopyPixels());
        byte[] previous = session.CopyPixels(), pixels = new byte[session.PixelByteCount];
        int changed = 0;
        for (int draw = 1; draw <= 1800; draw++)
        {
            session.DrawFrame(); session.CopyPixelsTo(pixels);
            if (!pixels.AsSpan().SequenceEqual(previous)) changed++;
            pixels.CopyTo(previous, 0);
            if (draw % 60 == 0) Save($"puzzle-{draw:D4}.png", pixels);
        }
        session.Shutdown();
        PlaybackResult playback = session.GetPlaybackResult();
        if (changed == 0 || !playback.ImportCalls.TryGetValue("USER!ScrollDC (#221)", out long scrolls) || scrolls == 0)
            throw new InvalidOperationException("Puzzle did not demonstrate scrolling and changed frames.");
        if (playback.LivePens + playback.LiveBrushes + playback.LiveBitmaps + playback.LiveMemoryDcs + playback.LiveRegions +
            playback.OutstandingLocks + playback.BitmapBytes != 0 || playback.LocalHeap?.Allocations.Count != 0)
            throw new InvalidOperationException("Puzzle retained guest resources after shutdown.");
        session.Dispose();
        return new(1800, changed, playback);

        void Save(string name, byte[] pixels)
        {
            using var output = new FileStream(Path.Combine(directory, name), FileMode.CreateNew);
            PngWriter.Write(output, width, height, pixels);
        }
    }

    private static string FindRepositoryRoot()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "AfterDarker.sln"))) return directory.FullName;
        return Environment.CurrentDirectory;
    }
}
