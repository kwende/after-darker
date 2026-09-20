using AfterDarker.Runtime;

namespace AfterDarker.Tests.LocalRainstorm;

/// <summary>Opt-in execution of the user's Rainstorm artifact; no original bytes are copied into test outputs.</summary>
[TestClass]
[TestCategory("LocalRainstorm")]
public sealed class RainstormTests
{
    private static byte[] ReadModule() => File.ReadAllBytes(Environment.GetEnvironmentVariable("AFTER_DARKER_RAINSTORM")
        ?? throw new InvalidOperationException("Set AFTER_DARKER_RAINSTORM before enabling TestLocalRainstorm."));

    [TestMethod]
    public void RainAndLightningPathsCompleteThreeHundredFramesWithBoundedPensAndCleanShutdown()
    {
        using var session = SupportedModules.Open(ReadModule(), new());
        Assert.AreEqual("Rainstorm", session.ModuleName);
        session.Initialize(); session.Blank();
        for (int frameIndex = 0; frameIndex < 300; frameIndex++) session.DrawFrame();
        byte[] pixels = new byte[session.PixelByteCount];
        session.CopyPixelsTo(pixels);
        Assert.IsTrue(pixels.Any(component => component != 0));
        var beforeShutdown = session.GetPlaybackResult();
        Assert.AreEqual(52L * 300, beforeShutdown.ImportCalls["USER!PtInRect (#76)"]);
        Assert.AreEqual(2L, beforeShutdown.ImportCalls["USER!InvertRect (#82)"]);
        Assert.AreEqual(1, beforeShutdown.PeakPens);
        Assert.AreEqual(0, beforeShutdown.LivePens);
        Assert.IsLessThanOrEqualTo(256, beforeShutdown.RetainedCalls);
        session.Shutdown(); session.Shutdown();
        AssertCleanShutdown(session.GetPlaybackResult());
        Console.WriteLine($"Rainstorm: {beforeShutdown.Instructions} instructions; {beforeShutdown.ImportCalls["USER!PtInRect (#76)"]} point tests; peak pens {beforeShutdown.PeakPens}.");
    }

    [TestMethod]
    public void IndependentGuestsProduceIdenticalFramesAndIgnoreTheUnrelatedSpeedOption()
    {
        byte[] file = ReadModule();
        using var first = SupportedModules.Open(file, new(320, 240, Speed: 0));
        using var second = SupportedModules.Open(file, new(320, 240, Speed: 100));
        first.Initialize(); first.Blank(); second.Initialize(); second.Blank();
        byte[] firstPixels = new byte[first.PixelByteCount], secondPixels = new byte[second.PixelByteCount];
        for (int frameIndex = 0; frameIndex < 30; frameIndex++)
        {
            first.DrawFrame(); second.DrawFrame();
            first.CopyPixelsTo(firstPixels); second.CopyPixelsTo(secondPixels);
            CollectionAssert.AreEqual(firstPixels, secondPixels);
        }
        Assert.IsTrue(firstPixels.Any(component => component != 0));
        Assert.AreEqual(first.GetPlaybackResult().Instructions, second.GetPlaybackResult().Instructions);
        first.Shutdown(); second.Shutdown();
        AssertCleanShutdown(first.GetPlaybackResult()); AssertCleanShutdown(second.GetPlaybackResult());
    }

    [TestMethod]
    [DataRow(1, 1)]
    [DataRow(2048, 2048)]
    public void DimensionExtremesKeepGuestCoordinatesAndShutdownValid(int width, int height)
    {
        using var session = SupportedModules.Open(ReadModule(), new((short)width, (short)height));
        session.Initialize(); session.Blank();
        for (int frameIndex = 0; frameIndex < 5; frameIndex++) session.DrawFrame();
        session.Shutdown();
        AssertCleanShutdown(session.GetPlaybackResult());
    }

    [TestMethod]
    public void AnotherArtifactRevisionIsRejectedBeforeExecution()
    {
        byte[] file = ReadModule();
        file[^1] ^= 1;
        Assert.Throws<NotSupportedException>(() => SupportedModules.Open(file, new()));
    }

    private static void AssertCleanShutdown(PlaybackResult result)
    {
        Assert.AreEqual(0, result.LivePens);
        Assert.AreEqual(0, result.OutstandingLocks);
        Assert.AreEqual("CLOSE", result.Phases[^2].Name);
        Assert.AreEqual("WEP", result.Phases[^1].Name);
        Assert.AreEqual((ushort)1, result.Phases[^1].StoredAx);
        Assert.AreEqual((ushort)0x1000, result.Phases[^1].Registers.Sp);
        Assert.AreEqual((ushort)0x48, result.Phases[^1].Registers.Ds);
    }
}
