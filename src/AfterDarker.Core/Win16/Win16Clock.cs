namespace AfterDarker.Core.Win16;

public interface IWin16Clock
{
    uint GetTickCount();
}

/// <summary>Repeatable experiment time: each request advances by a fixed amount.</summary>
public sealed class SteppingWin16Clock(uint initialTick = Win16ApiState.DefaultInitialTick,
    uint step = Win16ApiState.DefaultTickStep) : IWin16Clock
{
    private uint next = initialTick;
    public uint GetTickCount()
    {
        uint result = next;
        next = unchecked(next + step);
        return result;
    }
}

/// <summary>Live session milliseconds from a monotonic timestamp, wrapping like a Windows DWORD.</summary>
public sealed class ElapsedWin16Clock : IWin16Clock
{
    private readonly TimeProvider time;
    private readonly long started;
    private readonly uint initialTick;
    public ElapsedWin16Clock(TimeProvider? time = null, uint initialTick = 0)
    {
        this.time = time ?? TimeProvider.System;
        this.initialTick = initialTick;
        started = this.time.GetTimestamp();
    }
    public uint GetTickCount() => unchecked(initialTick +
        (uint)(time.GetElapsedTime(started).Ticks / TimeSpan.TicksPerMillisecond));
}
