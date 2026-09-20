using AfterDarker.Runtime;

namespace AfterDarker.Tests.LocalFadeAway;

/// <summary>Original Fade Away execution against an explicitly supplied private artifact, never copied to test outputs.</summary>
[TestClass]
[TestCategory("LocalFadeAway")]
public sealed class FadeAwayTests
{
    private static byte[] ReadModule() => File.ReadAllBytes(Environment.GetEnvironmentVariable("AFTER_DARKER_FADE_AWAY")
        ?? throw new InvalidOperationException("Set AFTER_DARKER_FADE_AWAY before enabling TestLocalFadeAway."));

    [TestMethod]
    [DataRow(320, 240)]
    [DataRow(640, 480)]
    [DataRow(1, 1)]
    [DataRow(2048, 2048)]
    [DataRow(321, 239)]
    public void RadarErasesWhiteInTwoPassesThenRemainsBlackUntilShutdown(int width, int height)
    {
        using var session = Open(new((short)width, (short)height));
        session.Initialize(); session.Blank();
        byte[] initialPixels = session.CopyPixels();
        Assert.IsTrue(initialPixels.All(component => component == 255));

        int drawCount = 0;
        bool sawFinePass = false;
        // A perimeter at step two then step one, plus allowance for corner transitions.
        int maximumDraws = 3 * (width + height) + 32;
        AfterDarkSession<FadeAwayState>.PhaseResult phase;
        do
        {
            phase = session.DrawFrame();
            drawCount++;
            Assert.AreEqual((ushort)0, phase.StoredAx);
            sawFinePass |= phase.State.Step == 1;
            Assert.IsLessThanOrEqualTo(maximumDraws, drawCount);
            if (drawCount == 30 && width >= 320)
            {
                byte[] intermediatePixels = session.CopyPixels();
                Assert.IsTrue(intermediatePixels.Any(component => component == 0));
                Assert.IsTrue(intermediatePixels.Any(component => component == 255));
            }
        } while (!phase.State.Finished);

        Assert.IsTrue(sawFinePass);
        Assert.IsTrue(session.CopyPixels().All(component => component == 0));
        var atCompletion = session.GetPlaybackResult();
        long linesAtCompletion = atCompletion.ImportCalls["GDI!LineTo (#19)"];
        Assert.AreEqual((long)drawCount, linesAtCompletion);
        Assert.AreEqual(1L, atCompletion.ImportCalls["USER!FillRect (#81)"]);
        Assert.AreEqual(0, atCompletion.PeakPens); // Radar only selects the stock black pen.
        Assert.IsLessThanOrEqualTo(256, atCompletion.RetainedCalls);

        for (int idleDraw = 0; idleDraw < 20; idleDraw++)
            Assert.IsTrue(session.DrawFrame().State.Finished);
        session.Blank(); // Original BLANK is a no-op; it must not re-seed the host image.
        Assert.IsTrue(session.CopyPixels().All(component => component == 0));
        Assert.AreEqual(linesAtCompletion, session.GetPlaybackResult().ImportCalls["GDI!LineTo (#19)"]);
        session.Shutdown(); session.Shutdown();
        AssertCleanShutdown(session.GetPlaybackResult());
        Console.WriteLine($"Fade Away {width}x{height}: complete after {drawCount} draws; {atCompletion.Instructions} instructions; {linesAtCompletion} original lines.");
    }

    [TestMethod]
    public void FreshGuestsRestartWhiteAndProduceTheSameFramesDespiteTheUnrelatedSpeedOption()
    {
        byte[] file = ReadModule();
        using var first = (AfterDarkSession<FadeAwayState>)SupportedModules.Open(file, new(320, 240, Speed: 0));
        using var second = (AfterDarkSession<FadeAwayState>)SupportedModules.Open(file, new(320, 240, Speed: 100));
        first.Initialize(); first.Blank();
        for (int frameIndex = 0; frameIndex < 30; frameIndex++) first.DrawFrame();
        byte[] expected = first.CopyPixels();
        first.Shutdown();
        second.Initialize(); second.Blank();
        Assert.IsTrue(second.CopyPixels().All(component => component == 255));
        for (int frameIndex = 0; frameIndex < 30; frameIndex++) second.DrawFrame();
        CollectionAssert.AreEqual(expected, second.CopyPixels());
        second.Shutdown();
        AssertCleanShutdown(first.GetPlaybackResult()); AssertCleanShutdown(second.GetPlaybackResult());
    }

    [TestMethod]
    public void ShutdownBeforeFirstDrawAndChangedArtifactRejectionRemainSafe()
    {
        using var session = Open(new());
        session.Initialize(); session.Blank(); session.Shutdown();
        AssertCleanShutdown(session.GetPlaybackResult());
        Assert.IsTrue(session.CopyPixels().All(component => component == 255));
        byte[] changed = ReadModule(); changed[^1] ^= 1;
        Assert.Throws<NotSupportedException>(() => SupportedModules.Open(changed, new()));
    }

    private static AfterDarkSession<FadeAwayState> Open(PlaybackOptions options) =>
        (AfterDarkSession<FadeAwayState>)SupportedModules.Open(ReadModule(), options);

    private static void AssertCleanShutdown(PlaybackResult result)
    {
        Assert.AreEqual(0, result.LivePens);
        Assert.AreEqual(0, result.OutstandingLocks);
        Assert.AreEqual("CLOSE", result.Phases[^2].Name);
        Assert.AreEqual((ushort)0, result.Phases[^2].StoredAx);
        Assert.AreEqual("WEP", result.Phases[^1].Name);
        Assert.AreEqual((ushort)1, result.Phases[^1].StoredAx);
        Assert.AreEqual((ushort)0x1000, result.Phases[^1].Registers.Sp);
        Assert.AreEqual((ushort)0x48, result.Phases[^1].Registers.Ds);
    }
}
