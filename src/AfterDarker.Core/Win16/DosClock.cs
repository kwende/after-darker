namespace AfterDarker.Core.Win16;

/// <summary>Only the DOS date/time register contract; no interrupt or CPU dependency.</summary>
public static class DosClock
{
    public const int InterruptNumber = 0x21;
    public const byte GetDate = 0x2A, GetTime = 0x2C;
    public sealed record Registers(ushort Ax, ushort Cx, ushort Dx);

    // A civil time is an explicit input, not the workstation's clock/timezone.
    // See DOS 4.0 TIME.ASM $GET_DATE/$GET_TIME and docs/research/dos-source-reference.md.
    public static Registers Respond(byte service, ushort ax, DateTime civilTime)
    {
        if (civilTime.Year is < 1980 or > 2099) throw new ArgumentOutOfRangeException(nameof(civilTime));
        return service switch
        {
            GetDate => new((ushort)((ax & 0xFF00) | (int)civilTime.DayOfWeek),
                (ushort)civilTime.Year, Pair(civilTime.Month, civilTime.Day)),
            // DOS 4.0 also returns AL=0. Preserve AH; it is not a promised result.
            GetTime => new((ushort)(ax & 0xFF00), Pair(civilTime.Hour, civilTime.Minute),
                Pair(civilTime.Second, civilTime.Millisecond / 10)),
            _ => throw new NotSupportedException($"Unsupported INT 21h AH={service:X2}; no DOS service simulated.")
        };
    }

    private static ushort Pair(int high, int low) => (ushort)((high << 8) | low);
}
