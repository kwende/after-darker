using AfterDarker.Core.Rendering;
using AfterDarker.Runtime;

namespace AfterDarker.Tests.LocalStainedGlass;

[TestClass]
[TestCategory("LocalStainedGlass")]
public sealed class StainedGlassTests
{
    private static byte[] ReadModule() => File.ReadAllBytes(Environment.GetEnvironmentVariable("AFTER_DARKER_STAINED_GLASS")
        ?? throw new InvalidOperationException("Set AFTER_DARKER_STAINED_GLASS to the local analyzed module."));

    [TestMethod]
    public void OriginalModuleDrawsAndShutsDown()
    {
        using var session = (AfterDarkSession<StainedGlassState>)SupportedModules.Open(ReadModule(), new(320, 240));
        session.Initialize(); session.Blank();
        int changes = 0;
        long maximumInstructions = 0, maximumCalls = 0;
        var previous = session.GetResult();
        for (int draw = 1; draw <= 2000; draw++)
        {
            try { session.DrawFrame(); }
            catch (Exception error) { throw new InvalidOperationException($"Draw {draw}: {error.Message}", error); }
            if (session.LastDrawingOperation?.ChangedPixels > 0) changes++;
            var current = session.GetResult();
            maximumInstructions = Math.Max(maximumInstructions, current.Instructions - previous.Instructions);
            maximumCalls = Math.Max(maximumCalls, current.Diagnostics.TotalCalls - previous.Diagnostics.TotalCalls);
            previous = current;
            if (Environment.GetEnvironmentVariable("AFTER_DARKER_STAINED_GLASS_CAPTURE") is string folder && draw % 100 == 0)
            {
                Directory.CreateDirectory(folder);
                using var output = File.Create(Path.Combine(folder, $"frame-{draw:D4}.png"));
                PngWriter.Write(output, 320, 240, session.CopyPixels());
            }
        }
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(session.GetPlaybackResult()));
        Console.WriteLine($"Largest draw: {maximumInstructions} instructions; {maximumCalls} imports.");
        foreach (string reached in new[] { "GetWindowOrg", "SetWindowOrg", "SetROP2", "SetPixel", "FrameRect", "InflateRect", "OffsetRect", "IntersectRect", "EqualRect", "BitBlt" })
            Assert.IsTrue(session.GetPlaybackResult().ImportCalls.Any(call => call.Key.Contains("!" + reached + " ") && call.Value > 0), reached);
        Assert.IsGreaterThan(0, changes);
        session.Shutdown();
        AssertCleanShutdown(session.GetPlaybackResult());
    }

    [TestMethod]
    [DataRow(64, 64, 0, 600)]
    [DataRow(640, 480, 11, 1200)]
    [DataRow(321, 239, 23, 1200)]
    [DataRow(2048, 2048, 47, 400)]
    public void DifferentSeedsAndSupportedSizesDrawAndReleaseOwnedObjects(int width, int height, int seedSeconds, int draws)
    {
        var timing = SessionTiming.Deterministic() with { CivilTime = MondrianSession.CivilTime.AddSeconds(seedSeconds) };
        using var session = SupportedModules.Open(ReadModule(), new((short)width, (short)height), timing: timing);
        session.Initialize(); session.Blank();
        for (int draw = 0; draw < draws; draw++) session.DrawFrame();
        byte[] pixels = new byte[session.PixelByteCount]; session.CopyPixelsTo(pixels);
        Assert.IsTrue(pixels.Any(component => component != 0));
        session.Shutdown(); AssertCleanShutdown(session.GetPlaybackResult());
    }

    [TestMethod]
    public void IndependentGuestsRetainTheirOwnOriginObjectsAndPixels()
    {
        byte[] file = ReadModule();
        using var first = SupportedModules.Open(file, new(64, 64, Speed: 0));
        using var second = SupportedModules.Open(file, new(64, 64, Speed: 100));
        first.Initialize(); first.Blank(); second.Initialize(); second.Blank();
        byte[] firstPixels = new byte[first.PixelByteCount], secondPixels = new byte[second.PixelByteCount];
        for (int draw = 0; draw < 150; draw++)
        {
            first.DrawFrame(); second.DrawFrame();
            first.CopyPixelsTo(firstPixels); second.CopyPixelsTo(secondPixels);
            CollectionAssert.AreEqual(firstPixels, secondPixels, $"Draw {draw}");
        }
        first.Shutdown(); second.DrawFrame(); second.Shutdown();
        AssertCleanShutdown(first.GetPlaybackResult()); AssertCleanShutdown(second.GetPlaybackResult());
    }

    [TestMethod]
    public void InvalidSizesAndDifferentArtifactsAreRejectedBeforeExecution()
    {
        byte[] file = ReadModule();
        Assert.Throws<ArgumentOutOfRangeException>(() => SupportedModules.Open(file, new(63, 240)));
        Assert.Throws<ArgumentOutOfRangeException>(() => SupportedModules.Open(file, new(320, 63)));
        file[^1] ^= 1;
        Assert.Throws<NotSupportedException>(() => SupportedModules.Open(file, new()));
    }

    private static void AssertCleanShutdown(PlaybackResult result)
    {
        Assert.AreEqual(0, result.LivePens); Assert.AreEqual(0, result.LiveBrushes);
        Assert.AreEqual(0, result.OutstandingLocks); Assert.AreEqual(0, result.LocalHeap!.Allocations.Count);
        Assert.AreEqual(0, result.LocalHeap.OutstandingLocks);
        Assert.AreEqual("CLOSE", result.Phases[^2].Name); Assert.AreEqual("WEP", result.Phases[^1].Name);
        Assert.AreEqual((ushort)1, result.Phases[^1].StoredAx);
        Assert.AreEqual((ushort)0x1000, result.Phases[^1].Registers.Sp);
        Assert.AreEqual((ushort)0x68, result.Phases[^1].Registers.Ds);
    }
}
