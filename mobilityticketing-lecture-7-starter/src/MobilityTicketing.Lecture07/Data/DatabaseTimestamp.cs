namespace MobilityTicketing.Lecture07.Data;

public static class DatabaseTimestamp
{
    public static DateTime Now(TimeProvider clock)
    {
        var utc = clock.GetUtcNow().UtcDateTime;
        return new DateTime(
            utc.Ticks - utc.Ticks % TimeSpan.TicksPerMicrosecond,
            DateTimeKind.Utc);
    }
}
