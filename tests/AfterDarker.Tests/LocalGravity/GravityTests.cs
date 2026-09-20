using AfterDarker.Core.Rendering;
using AfterDarker.Runtime;
using AfterDarker.Core.Win16;
using AfterDarker.Tests.Fixtures;

namespace AfterDarker.Tests.LocalGravity;

[TestClass]
[TestCategory("LocalGravity")]
public sealed class GravityTests
{
    private static byte[] ReadModule() => File.ReadAllBytes(Environment.GetEnvironmentVariable("AFTER_DARKER_GRAVITY")
        ?? throw new InvalidOperationException("Set AFTER_DARKER_GRAVITY to the analyzed local module."));

    [TestMethod]
    public void OriginalGravityDrawsSilentlyAndShutsDown()
    {
        using var session = (AfterDarkSession<GravityState>)SupportedModules.Open(ReadModule(), new(320, 240));
        var initial = session.Initialize().State;
        Assert.AreEqual((ushort)4, initial.BallCount); Assert.AreEqual((ushort)20, initial.BallSize);
        Assert.AreEqual((ushort)0, initial.Sound);
        session.Blank();
        long maximumInstructions = 0, previousInstructions = session.GetPlaybackResult().Instructions;
        var positions = new HashSet<(short X, short Y)>();
        for (int draw = 1; draw <= 1000; draw++)
        {
            var phase = session.DrawFrame();
            foreach (var ball in phase.State.Balls) positions.Add((ball.X, ball.Y));
            var current = session.GetPlaybackResult();
            Assert.AreEqual(1, current.LiveBitmaps); Assert.AreEqual(3600, current.BitmapBytes);
            Assert.AreEqual(0, current.LiveMemoryDcs); Assert.AreEqual(0, current.LiveBrushes);
            Assert.AreEqual((ushort)0, phase.State.Sound);
            maximumInstructions = Math.Max(maximumInstructions, current.Instructions - previousInstructions);
            previousInstructions = current.Instructions;
            if (Environment.GetEnvironmentVariable("AFTER_DARKER_GRAVITY_CAPTURE") is string folder && draw % 10 == 0)
            {
                Directory.CreateDirectory(folder);
                using var output = File.Create(Path.Combine(folder, $"frame-{draw:D4}.png"));
                PngWriter.Write(output, 320, 240, session.CopyPixels());
            }
        }
        Assert.IsTrue(session.CopyPixels().Any(value => value != 0));
        Assert.IsGreaterThan(100, positions.Count);
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(session.GetPlaybackResult()));
        Console.WriteLine($"Largest draw: {maximumInstructions} instructions.");
        foreach (var call in session.GetResult().Calls.Where(call => call.Binding.Import.Module == "AD_SND"))
            Assert.AreEqual(0u, call.Returned);
        var imports = session.GetPlaybackResult().ImportCalls;
        foreach (string name in new[] { "CreateCompatibleDC", "CreateCompatibleBitmap", "DeleteDC", "PatBlt", "BitBlt" })
            Assert.IsTrue(imports.Any(call => call.Key.Contains("!" + name + " ") && call.Value > 0));
        Assert.IsGreaterThan(0L, imports["AD_SND!ADWPLAYSOUND"]); // Even Sound off doesn't bypass every call.
        session.Blank();
        Assert.IsTrue(session.CopyPixels().All(value => value == 0));
        session.Shutdown();
        AssertCleanShutdown(session.GetPlaybackResult());
        Assert.AreEqual(1L, session.GetPlaybackResult().ImportCalls["AD_SND!ADWCLOSESOUND"]);
        Assert.AreEqual(1L, session.GetPlaybackResult().ImportCalls["AD_SND!ADWFREESOUND"]);
    }

    [TestMethod]
    [DataRow(64, 64, 0)]
    [DataRow(321, 239, 13)]
    [DataRow(2048, 2048, 27)]
    public void SupportedDimensionsAndSeedsRetainOneBitmapAndNoTemporaryContexts(int width, int height, int seedSeconds)
    {
        var timing = SessionTiming.Deterministic() with { CivilTime = MondrianSession.CivilTime.AddSeconds(seedSeconds) };
        using var session = SupportedModules.Open(ReadModule(), new((short)width, (short)height), timing: timing);
        session.Initialize(); session.Blank();
        for (int draw = 0; draw < 500; draw++) session.DrawFrame();
        var result = session.GetPlaybackResult();
        Assert.AreEqual(1, result.LiveBitmaps); Assert.AreEqual(1, result.PeakBitmaps);
        Assert.AreEqual(0, result.LiveMemoryDcs); Assert.AreEqual(1, result.PeakMemoryDcs);
        byte[] pixels = new byte[session.PixelByteCount]; session.CopyPixelsTo(pixels);
        Assert.IsTrue(pixels.Any(value => value != 0));
        session.Shutdown(); AssertCleanShutdown(session.GetPlaybackResult());
    }

    [TestMethod]
    public void IndependentGuestsOwnTheirBitmapsAndIgnoreUnrelatedSpeedControl()
    {
        byte[] file = ReadModule();
        using var first = SupportedModules.Open(file, new(128, 96, Speed: 0));
        using var second = SupportedModules.Open(file, new(128, 96, Speed: 100));
        first.Initialize(); second.Initialize(); first.Blank(); second.Blank();
        byte[] firstPixels = new byte[first.PixelByteCount], secondPixels = new byte[second.PixelByteCount];
        for (int draw = 0; draw < 200; draw++)
        {
            first.DrawFrame(); second.DrawFrame(); first.CopyPixelsTo(firstPixels); second.CopyPixelsTo(secondPixels);
            CollectionAssert.AreEqual(firstPixels, secondPixels, $"Draw {draw}");
        }
        first.Shutdown(); second.DrawFrame(); second.Shutdown();
        AssertCleanShutdown(first.GetPlaybackResult()); AssertCleanShutdown(second.GetPlaybackResult());
    }

    [TestMethod]
    public void LiveClockProgressesWithoutSimulatedTicksInsideOneDraw()
    {
        var time = new ManualTimeProvider();
        using var session = SupportedModules.Open(ReadModule(), new(320, 240), timing: SessionTiming.Live(time));
        session.Initialize(); session.Blank();
        for (int draw = 0; draw < 300; draw++) { time.Advance(TimeSpan.FromMilliseconds(16)); session.DrawFrame(); }
        byte[] pixels = new byte[session.PixelByteCount]; session.CopyPixelsTo(pixels);
        Assert.IsTrue(pixels.Any(value => value != 0));
        session.Shutdown(); AssertCleanShutdown(session.GetPlaybackResult());
    }

    [TestMethod]
    public void UnsafeDimensionsAndChangedArtifactAreRejectedBeforeGuestExecution()
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
        Assert.AreEqual(0, result.LiveBitmaps); Assert.AreEqual(0, result.LiveMemoryDcs); Assert.AreEqual(0, result.BitmapBytes);
        Assert.AreEqual(0, result.OutstandingLocks); Assert.AreEqual(0, result.LocalHeap!.Allocations.Count);
        Assert.AreEqual(0, result.LocalHeap.OutstandingLocks);
        Assert.AreEqual("CLOSE", result.Phases[^2].Name); Assert.AreEqual("WEP", result.Phases[^1].Name);
        Assert.AreEqual((ushort)1, result.Phases[^1].StoredAx); Assert.AreEqual((ushort)0x1000, result.Phases[^1].Registers.Sp);
        Assert.AreEqual((ushort)0x48, result.Phases[^1].Registers.Ds);
    }
}
