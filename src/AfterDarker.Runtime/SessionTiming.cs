using AfterDarker.Core.Win16;

namespace AfterDarker.Runtime;

/// <summary>Each session owns its clock. Civil time is captured once so startup's date/time/date agrees.</summary>
public sealed record SessionTiming(IWin16Clock Clock, DateTime CivilTime)
{
    public static SessionTiming Deterministic() => new(new SteppingWin16Clock(), MondrianSession.CivilTime);
    public static SessionTiming Live(TimeProvider? time = null)
    {
        time ??= TimeProvider.System;
        return new(new ElapsedWin16Clock(time), time.GetLocalNow().DateTime);
    }
}

/// <summary>One sequential host loop. Late frames skip catch-up bursts; waiting is cancellable.</summary>
public sealed class FramePacer
{
    private readonly TimeProvider time;
    private readonly TimeSpan interval;
    private long lastFrame;
    public FramePacer(TimeSpan interval, TimeProvider? time = null)
    {
        if (interval < TimeSpan.FromMilliseconds(1) || interval > TimeSpan.FromSeconds(1))
            throw new ArgumentOutOfRangeException(nameof(interval));
        this.interval = interval;
        this.time = time ?? TimeProvider.System;
        lastFrame = this.time.GetTimestamp();
    }
    public TimeSpan RemainingDelay => TimeSpan.FromTicks(Math.Max(0,
        (interval - time.GetElapsedTime(lastFrame)).Ticks));

    public async ValueTask WaitForNextFrameAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        TimeSpan delay = RemainingDelay;
        if (delay > TimeSpan.Zero) await Task.Delay(delay, time, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        lastFrame = time.GetTimestamp();
    }
}
