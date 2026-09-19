using AfterDarker.Runtime;

namespace AfterDarker.Tests.LocalModule;

[TestClass]
[TestCategory("LocalModule")]
public sealed class MondrianShutdownTests
{
    private static byte[] ReadModule() => File.ReadAllBytes(Environment.GetEnvironmentVariable("AFTER_DARKER_MONDRIAN")
        ?? throw new InvalidOperationException("Set AFTER_DARKER_MONDRIAN before enabling TestLocalMondrian."));

    [TestMethod]
    [DataRow(0, true)]
    [DataRow(30, true)]
    [DataRow(300, true)]
    [DataRow(30, false)]
    public void OriginalCloseReplaysSavedRectanglesAndWepBalancesItsOwnFarFrame(int draws, bool clear)
    {
        using var session = new MondrianSession(ReadModule(), new(Width: 64, Height: 48, Speed: 100, Clear: clear),
            diagnostics: DiagnosticOptions.Full);
        Assert.Throws<InvalidOperationException>(() => session.Shutdown());
        session.Initialize();
        Assert.Throws<InvalidOperationException>(() => session.Shutdown()); // drawing lifecycle has not begun
        session.Blank();
        for (int i = 0; i < draws; i++) session.DrawFrame();
        var before = session.GetResult();
        byte[] pixels = session.CopyPixels();
        ushort rectangles = before.Phases[^1].State.Rectangles;
        session.Shutdown();
        Assert.AreEqual(MondrianSession.SessionState.Closed, session.State);
        var after = session.GetResult();
        CollectionAssert.AreEqual(new[] { "CLOSE", "WEP" }, after.Phases.TakeLast(2).Select(p => p.Name).ToArray());
        Assert.AreEqual((ushort)1, after.Phases[^1].StoredAx);
        Assert.AreEqual(0, after.OutstandingLocks);
        foreach (var phase in after.Phases.TakeLast(2))
        {
            Assert.AreEqual(MondrianSession.InitialSp, phase.Registers.Sp);
            Assert.AreEqual(MondrianSession.HostData, phase.Registers.Ds);
            Assert.AreEqual((ushort)0, phase.Registers.Bp);
        }
        var closing = after.Calls.Where(c => c.Phase == "CLOSE").ToArray();
        Assert.AreEqual(rectangles + (clear ? 7 : 4), closing.Length);
        Assert.IsFalse(after.Calls.Any(c => c.Phase == "WEP"));
        // CLOSE first clears (when configured), then re-inverts the stored
        // rectangles. Clearing first redraws the picture; without clearing,
        // XOR removes the existing picture. Neither path resets the guest count.
        Assert.AreEqual(rectangles, after.Phases[^2].State.Rectangles);
        if (clear) CollectionAssert.AreEqual(pixels, session.CopyPixels());
        else Assert.IsTrue(session.CopyPixels().All(b => b == 0));
        session.Shutdown(); // exactly once
        Assert.AreEqual(after.Instructions, session.GetResult().Instructions);
        Assert.Throws<InvalidOperationException>(() => session.DrawFrame());
        Assert.Throws<InvalidOperationException>(() => session.Blank());
        session.Dispose();
        Assert.Throws<ObjectDisposedException>(() => session.Shutdown());
    }

    [TestMethod]
    public void FaultedGuestRejectsShutdownAndReleasesNativeResources()
    {
        using var session = new MondrianSession(ReadModule(), instructionLimit: 10);
        Assert.Throws<InvalidOperationException>(() => session.Initialize());
        Assert.Throws<InvalidOperationException>(() => session.Shutdown());
        session.Dispose();
        Assert.AreEqual(MondrianSession.SessionState.Disposed, session.State);
    }

    [TestMethod]
    public void FailureInsideClosePreventsWepAndCannotBeRetried()
    {
        using var output = new FailDuringCloseWriter();
        using var session = new MondrianSession(ReadModule(), output: output);
        session.Initialize(); session.Blank(); session.DrawFrame();
        Assert.Throws<IOException>(() => session.Shutdown());
        Assert.AreEqual(MondrianSession.SessionState.Faulted, session.State);
        Assert.Throws<InvalidOperationException>(() => session.Shutdown());
        Assert.IsFalse(output.ToString().Contains("WEP returned"));
        session.Dispose();
        Assert.AreEqual(MondrianSession.SessionState.Disposed, session.State);
    }

    private sealed class FailDuringCloseWriter : StringWriter
    {
        public override void WriteLine(string? value)
        {
            if (value?.Contains("CLOSE:") == true) throw new IOException("Simulated failure inside CLOSE dispatch.");
            base.WriteLine(value);
        }
    }

    [TestMethod]
    public async Task PlaybackStopsRestartsAndReturnsCompletedGuestShutdown()
    {
        byte[] file = ReadModule();
        for (int run = 0; run < 3; run++)
        {
            using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var mailbox = new LatestFrameMailbox(64 * 48 * 3);
            var playing = MondrianPlayback.RunAsync(file, new(64, 48, 100), mailbox, stop.Token);
            byte[] pixels = new byte[64 * 48 * 3];
            try
            {
                while (!mailbox.TryCopyTo(pixels, out var frame) || frame!.ChangedFrames < 3)
                {
                    if (playing.IsCompleted) Assert.Fail("Playback ended before presenting three changed images.");
                    await Task.Delay(10, stop.Token);
                }
            }
            finally { stop.Cancel(); }
            var result = await playing.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.AreEqual("WEP", result.Phases[^1].Name);
            Assert.AreEqual("CLOSE", result.Phases[^2].Name);
            Assert.AreEqual((ushort)1, result.Phases[^1].StoredAx);
            Assert.AreEqual(0, result.OutstandingLocks);
            Assert.IsLessThanOrEqualTo(256, result.Calls.Count);
        }
    }

    [TestMethod]
    public async Task CancelledPlaybackDoesNotCreateAGuestAndInvalidInputFaultsPromptly()
    {
        using var stop = new CancellationTokenSource(); stop.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            MondrianPlayback.RunAsync([], new(), new(3), stop.Token));
        await Assert.ThrowsAsync<NotSupportedException>(() =>
            MondrianPlayback.RunAsync([], new(), new(3), CancellationToken.None));
    }
}
