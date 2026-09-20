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
        Assert.AreEqual(1L, beforeShutdown.IntermediateFrames);
        Assert.AreEqual(5_255_349L, beforeShutdown.Instructions); // Presentation adds no guest instructions.
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
        int flashes = 0;
        first.IntermediateFrameReady += (_, _) => flashes++;
        byte[] firstPixels = new byte[first.PixelByteCount], secondPixels = new byte[second.PixelByteCount];
        // Observe two flashes in one guest while the other has no subscriber.
        // Capturing images must not alter random state, completed pixels or timing.
        for (int frameIndex = 0; frameIndex < 452; frameIndex++)
        {
            first.DrawFrame(); second.DrawFrame();
            first.CopyPixelsTo(firstPixels); second.CopyPixelsTo(secondPixels);
            Assert.IsTrue(firstPixels.AsSpan().SequenceEqual(secondPixels),
                $"Completed images differ at draw {frameIndex + 1}.");
        }
        Assert.IsTrue(firstPixels.Any(component => component != 0));
        Assert.AreEqual(2, flashes);
        Assert.AreEqual(2L, second.GetPlaybackResult().IntermediateFrames);
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

    [TestMethod]
    [DataRow(1, 1)]
    [DataRow(321, 239)]
    [DataRow(2048, 2048)]
    public void LightningPublishesTheExactInvertedSurfaceOnDraw226(int width, int height)
    {
        using var session = (AfterDarkSession<RainstormState>)SupportedModules.Open(ReadModule(), new((short)width, (short)height));
        var flashImages = new List<byte[]>();
        session.IntermediateFrameReady += (pixels, hold) =>
        {
            Assert.AreEqual(TimeSpan.FromMilliseconds(80), hold);
            flashImages.Add(pixels.ToArray()); // Copy the borrowed checkpoint buffer.
        };
        session.Initialize(); session.Blank();
        for (int draw = 0; draw < 225; draw++) session.DrawFrame();
        Assert.AreEqual(0, flashImages.Count);
        byte[] expectedFlash = session.CopyPixels();
        for (int component = 0; component < expectedFlash.Length; component++)
            expectedFlash[component] ^= 0xFF;

        var completed = session.DrawFrame();
        Assert.AreEqual(1, flashImages.Count);
        Assert.IsTrue(expectedFlash.AsSpan().SequenceEqual(flashImages[0]),
            "The checkpoint image must be the exact inverse of the preceding completed image.");
        Assert.AreEqual((short)225, completed.State.LightningCountdown);
        Assert.AreEqual(2L, session.GetPlaybackResult().ImportCalls["USER!InvertRect (#82)"]);
        Assert.AreEqual(1L, session.GetPlaybackResult().IntermediateFrames);
        Assert.IsFalse(session.CopyPixels().AsSpan().SequenceEqual(expectedFlash));
        session.Shutdown();
        AssertCleanShutdown(session.GetPlaybackResult());
    }

    [TestMethod]
    public async Task CancellingDuringAPublishedFlashStillRestoresTheImageAndCompletesGuestCleanup()
    {
        using var stop = new CancellationTokenSource();
        var mailbox = new LatestFrameMailbox(640 * 480 * 3);
        Task<PlaybackResult> running = AfterDarkPlayback.RunAsync(ReadModule(), new(), mailbox, stop.Token);
        byte[] pixels = new byte[640 * 480 * 3];
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        FrameInfo? flash = null;
        try
        {
            while (flash is null)
            {
                if (running.IsCompleted) await running; // Report guest failure rather than just timing out.
                if (mailbox.TryCopyTo(pixels, out var frame) && frame!.IsIntermediate) flash = frame;
                else await Task.Delay(5, deadline.Token);
            }
        }
        finally
        {
            stop.Cancel(); // Wakes the hold; the second InvertRect must still execute.
            await running.WaitAsync(TimeSpan.FromSeconds(10));
        }
        Assert.AreEqual(226L, flash!.DrawCalls);
        Assert.IsGreaterThan(pixels.Length / 2, pixels.Count(component => component > 127));
        var result = await running;
        Assert.AreEqual(1L, result.IntermediateFrames);
        Assert.AreEqual(2L, result.ImportCalls["USER!InvertRect (#82)"]);
        AssertCleanShutdown(result);

        // The worker publishes the completed, restored image before honoring Stop.
        Assert.IsTrue(mailbox.TryCopyTo(pixels, out var restored));
        Assert.IsFalse(restored!.IsIntermediate);
        Assert.AreEqual(226L, restored.DrawCalls);
        Assert.IsLessThan(pixels.Length / 2, pixels.Count(component => component > 127));
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
