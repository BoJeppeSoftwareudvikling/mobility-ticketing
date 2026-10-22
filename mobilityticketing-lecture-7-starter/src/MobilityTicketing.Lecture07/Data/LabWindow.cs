using MobilityTicketing.Lecture07.Contracts;

namespace MobilityTicketing.Lecture07.Data;

public static class LabWindow
{
    public static readonly DateTime FromUtc = new(2026, 10, 23, 6, 0, 0, DateTimeKind.Utc);
    public static readonly DateTime ToUtc = new(2026, 10, 23, 7, 0, 0, DateTimeKind.Utc);
    public static void Validate(DateTime fromUtc, DateTime toUtc) => new TimeWindow(fromUtc, toUtc).Validate();
}
