namespace AfterDarker.Tests.Fixtures;

public sealed class ManualTimeProvider : TimeProvider
{
    private long timestamp;
    public DateTimeOffset UtcNow { get; set; } = new(1993, 6, 15, 12, 34, 56, TimeSpan.Zero);
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;
    public override long GetTimestamp() => timestamp;
    public override DateTimeOffset GetUtcNow() => UtcNow;
    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    public void Advance(TimeSpan elapsed) { timestamp += elapsed.Ticks; UtcNow += elapsed; }
}
