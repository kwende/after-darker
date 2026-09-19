using System.Security.Cryptography;
using System.Text.Json;
using AfterDarker.Core.AfterDark;
using AfterDarker.Core.Ne;
using AfterDarker.Core.Rendering;
using AfterDarker.Core.Win16;
using AfterDarker.Runtime;

namespace AfterDarker.Tutorials.Lessons;

/// <summary>
/// Follow Capture: the host calls MODULE, the original x86 chooses rectangles,
/// Win16Api paints them, and only then do we inspect/save the resulting pixels.
/// There is deliberately no C# Mondrian algorithm here.
/// </summary>
public sealed class Tutorial09MondrianFrames(string? path = null, string? frameCount = null,
    TextReader? input = null, TextWriter? output = null) : ITutorial
{
    public string Id => "09";
    public string Title => "Run original Mondrian and capture changed frames as PNGs";

    public sealed record Frame(int Number, int DrawCall, string FileName, string RgbSha256,
        int ChangedPixels, Win16Drawing.Operation? LastOperation, MondrianInitialization.State GuestState);
    public sealed record CaptureResult(MondrianSession.Result Execution, IReadOnlyList<Frame> Frames, int DrawCalls);

    public void Run()
    {
        TextWriter writer = output ?? Console.Out;
        string? filePath = path;
        if (filePath is null)
        {
            writer.Write("Path to your local Mondrian.ad (the analyzed version): ");
            filePath = (input ?? Console.In).ReadLine()?.Trim().Trim('"');
        }
        if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("A local Mondrian path is required.");
        int count = frameCount is null ? 30 : int.Parse(frameCount);
        ValidateCount(count);
        using var stream = File.OpenRead(filePath);
        if (stream.Length > NeReader.MaximumFileBytes) throw new InvalidDataException("Module exceeds the NE input limit.");
        byte[] file = new byte[(int)stream.Length]; stream.ReadExactly(file);
        // Unique, ignored output; never overwrite an earlier experiment. Find
        // the repository from the executable so F5 and CLI use the same place.
        string directory = Path.Combine(RepositoryRoot(), "artifacts", "mondrian-frames",
            DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(directory);
        writer.WriteLine($"Captures: {directory}");
        var options = new MondrianInitialization.Options(Speed: 100);
        try
        {
            var result = Capture(file, count, options, save: (frame, pixels) =>
            {
                using var png = new FileStream(Path.Combine(directory, frame.FileName), FileMode.CreateNew);
                PngWriter.Write(png, options.Width, options.Height, pixels);
                writer.WriteLine($"   frame {frame.Number:D4}: DRAWFRAME #{frame.DrawCall}, {frame.ChangedPixels} changed pixels; rectangles={frame.GuestState.Rectangles}");
            });
            WriteJson("report.json", new
            {
                Status = "complete", ModuleSha256 = MondrianInitialization.Sha256,
                Options = options, CivilTime = MondrianSession.CivilTime,
                InitialTick = Win16ApiState.DefaultInitialTick, TickStepPerRequest = Win16ApiState.DefaultTickStep,
                ClockPolicy = "deterministic synthetic milliseconds per GetTickCount request; not wall-clock playback",
                RasterPolicy = "RGB8 identity coordinates; full-surface clip; black brush; Windows PatBlt-tested rectangle bounds",
                Shutdown = "bounded capture; guest disposed without CLOSE or WEP",
                result.DrawCalls, result.Execution.Instructions, result.Execution.ProtectedMode, result.Execution.OutstandingLocks,
                Imports = result.Execution.Calls.GroupBy(c => c.Binding.Name).Select(g => new { Name = g.Key, Calls = g.Count() }),
                Frames = result.Frames
            });
            WriteGallery(directory, result.Frames);
            writer.WriteLine($"PASS: original Mondrian produced {count} changed PNG frames in {result.DrawCalls} DRAWFRAME calls.");
            writer.WriteLine($"Open {Path.Combine(directory, "index.html")} to play/step the captures; report.json records evidence.");
        }
        catch (Exception error)
        {
            WriteJson("failure.json", new { Status = "incomplete", Error = error.Message });
            throw;
        }
        void WriteJson(string name, object value) => File.WriteAllText(Path.Combine(directory, name),
            JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
    }

    /// <summary>
    /// A bounded experiment, also usable by local integration tests without disk
    /// output. One callback per changed image; the supplied pixel array is a copy.
    /// </summary>
    public static CaptureResult Capture(byte[] file, int frameCount = 30,
        MondrianInitialization.Options? options = null, int maximumDrawCalls = 10_000,
        Action<Frame, byte[]>? save = null)
    {
        ValidateCount(frameCount);
        if (maximumDrawCalls is < 1 or > 10_000) throw new ArgumentOutOfRangeException(nameof(maximumDrawCalls));
        options ??= new(Speed: 100);
        options.Validate();
        // The host owns the session lifetime explicitly. Each method returns
        // normally to C#; the guest's registers, memory and pixels stay alive.
        using var session = new MondrianSession(file, options);
        session.Initialize();
        session.Blank();
        var frames = new List<Frame>();
        int draws = 0;
        byte[] previous = session.CopyPixels();
        // A call can pass a timing gate without drawing, or touch pixels
        // without changing the final image. Count actual changed snapshots.
        while (frames.Count < frameCount && draws < maximumDrawCalls)
        {
            var returned = session.DrawFrame();
            draws++;
            byte[] pixels = session.CopyPixels();
            int changed = CountChangedPixels(previous, pixels);
            if (changed == 0) continue;
            int number = frames.Count + 1;
            var frame = new Frame(number, draws, $"frame-{number:D4}.png", Convert.ToHexString(SHA256.HashData(pixels)),
                changed, session.LastDrawingOperation, returned.State);
            save?.Invoke(frame, pixels);
            frames.Add(frame);
            previous = pixels;
        }
        if (frames.Count != frameCount)
            throw new InvalidOperationException($"Capture budget exhausted: {frames.Count}/{frameCount} changed images after {draws} DRAWFRAME calls.");
        return new(session.GetResult(), frames.AsReadOnly(), draws);
    }

    private static int CountChangedPixels(byte[] before, byte[] after)
    {
        int changed = 0;
        for (int i = 0; i < before.Length; i += 3)
            if (before[i] != after[i] || before[i + 1] != after[i + 1] || before[i + 2] != after[i + 2]) changed++;
        return changed;
    }
    private static void ValidateCount(int count)
    {
        if (count is < 1 or > 300) throw new ArgumentOutOfRangeException(nameof(count), "Capture 1..300 changed frames.");
    }
    private static string RepositoryRoot()
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "AfterDarker.sln"))) return dir.FullName;
        throw new DirectoryNotFoundException("Run the lesson from a repository build so captures stay in ignored artifacts.");
    }
    private static void WriteGallery(string directory, IReadOnlyList<Frame> frames)
    {
        // A local viewer, not a timing reconstruction. Display cadence is a
        // modern presentation choice; stepping preserves each captured image.
        string names = JsonSerializer.Serialize(frames.Select(f => f.FileName));
        File.WriteAllText(Path.Combine(directory, "index.html"), """
            <!doctype html><meta charset="utf-8"><title>Original Mondrian · captured frames</title>
            <style>body{background:#202126;color:#eee;font:18px system-ui;margin:32px}img{display:block;margin:20px 0;max-width:100%;border:1px solid #777;image-rendering:pixelated}button,input{margin:8px;font:inherit}p{max-width:760px}</style>
            <h1>Original Mondrian</h1><p>Original Win16 code → emulated imports → software pixels. Playback is a modern preview, not historical pacing.</p>
            <button id="play">Play</button><button id="previous">Previous</button><button id="next">Next</button>
            <input id="position" type="range" min="0" value="0"><span id="label"></span><img id="frame" alt="Captured Mondrian frame">
            <p><a href="report.json" style="color:#9cf">Reproducibility report</a></p><script>
            const frames =
            """ + names + """
            ;let index=0,timer;const slider=document.getElementById('position');slider.max=frames.length-1;
            function show(){document.getElementById('frame').src=frames[index];slider.value=index;document.getElementById('label').textContent=`${index+1} / ${frames.length}`;}
            function step(delta){index=(index+delta+frames.length)%frames.length;show();}
            document.getElementById('next').onclick=()=>step(1);document.getElementById('previous').onclick=()=>step(-1);
            slider.oninput=()=>{index=Number(slider.value);show();};
            document.getElementById('play').onclick=function(){if(timer){clearInterval(timer);timer=null;this.textContent='Play';}else{timer=setInterval(()=>step(1),150);this.textContent='Pause';}};show();</script>
            """);
    }
}
