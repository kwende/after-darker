using AfterDarker.Core.Win16;
using AfterDarker.Runtime;
using AfterDarker.Tests.Fixtures;

namespace AfterDarker.Tests.Unit;

[TestClass]
[TestCategory("Unit")]
public sealed class SessionTimingTests
{
    [TestMethod]
    public void LiveTicksFollowElapsedTimeNotRequestsOrWallClockChanges()
    {
        var time = new ManualTimeProvider();
        var clock = new ElapsedWin16Clock(time, uint.MaxValue - 7);
        Assert.AreEqual(uint.MaxValue - 7, clock.GetTickCount());
        Assert.AreEqual(uint.MaxValue - 7, clock.GetTickCount());
        time.Advance(TimeSpan.FromMilliseconds(16));
        Assert.AreEqual(8u, clock.GetTickCount());
        time.UtcNow -= TimeSpan.FromDays(1);
        Assert.AreEqual(8u, clock.GetTickCount());
    }

    [TestMethod]
    public void DeterministicTimingIsIndependentPerSessionAndLiveCivilTimeIsCapturedOnce()
    {
        var first = SessionTiming.Deterministic(); var second = SessionTiming.Deterministic();
        Assert.AreEqual(first.Clock.GetTickCount(), second.Clock.GetTickCount());
        first.Clock.GetTickCount();
        Assert.AreEqual(Win16ApiState.DefaultInitialTick + 16, second.Clock.GetTickCount());
        var time = new ManualTimeProvider();
        var live = SessionTiming.Live(time);
        time.Advance(TimeSpan.FromDays(1));
        Assert.AreEqual(new DateTime(1993, 6, 15, 12, 34, 56), live.CivilTime);
    }

    [TestMethod]
    public async Task PacerAccountsForWorkAndDropsMissedDeadlines()
    {
        var time = new ManualTimeProvider();
        var pacer = new FramePacer(TimeSpan.FromMilliseconds(16), time);
        time.Advance(TimeSpan.FromMilliseconds(6));
        Assert.AreEqual(TimeSpan.FromMilliseconds(10), pacer.RemainingDelay);
        time.Advance(TimeSpan.FromMilliseconds(100));
        Assert.AreEqual(TimeSpan.Zero, pacer.RemainingDelay);
        await pacer.WaitForNextFrameAsync(CancellationToken.None);
        Assert.AreEqual(TimeSpan.FromMilliseconds(16), pacer.RemainingDelay);
    }

    [TestMethod]
    public async Task PacingWaitCanBeCancelled()
    {
        var pacer = new FramePacer(TimeSpan.FromSeconds(1));
        using var stop = new CancellationTokenSource();
        var wait = pacer.WaitForNextFrameAsync(stop.Token).AsTask();
        stop.Cancel();
        await Assert.ThrowsAsync<TaskCanceledException>(() => wait);
    }
}
