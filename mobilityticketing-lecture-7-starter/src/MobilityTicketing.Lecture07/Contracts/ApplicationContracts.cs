using MobilityTicketing.Lecture07.Data;

namespace MobilityTicketing.Lecture07.Contracts;

public interface ILectureApplication
{
    Task<List<RouteOverviewDto>> ReadOverviewAsync(TimeWindow window, CancellationToken ct = default);
    Task<CancelResult> CancelTripAsync(string tripId, CancellationToken ct = default);
    Task<RenameResult> RenameRouteAsync(string routeId, string name, CancellationToken ct = default);
    Task<BookingResult> BookTripAsync(string tripId, string requestId, CancellationToken ct = default);
    Task<PublishResult> PublishSnapshotAsync(string snapshotId, TimeWindow window, CancellationToken ct = default);
    Task<PublishedSnapshot> ReadSnapshotAsync(string snapshotId, CancellationToken ct = default);
}

public record TimeWindow(DateTime FromUtc, DateTime ToUtc)
{
    public void Validate()
    {
        if (FromUtc.Kind != DateTimeKind.Utc || ToUtc.Kind != DateTimeKind.Utc ||
            ToUtc <= FromUtc || ToUtc - FromUtc > TimeSpan.FromDays(1))
            throw new ArgumentException("Use UTC endpoints with a positive interval of at most 24 hours.");
        if (FromUtc.Ticks % TimeSpan.TicksPerMicrosecond != 0 ||
            ToUtc.Ticks % TimeSpan.TicksPerMicrosecond != 0)
            throw new ArgumentException("Window endpoints must use whole microseconds (at most six fractional-second digits).");
    }
    public static TimeWindow Morning => new(LabWindow.FromUtc, LabWindow.ToUtc);
}

public enum CancelResult { Cancelled, AlreadyCancelled, NotFound }
public enum RenameResult { Renamed, NotFound }
public enum BookingStatus { Booked, AlreadyBooked, Cancelled, NotFound, RequestConflict }
public enum PublishResult { Published, AlreadyPublished }
public record BookingReceipt(string RequestId, string TripId, DateTime BookedAtUtc);
public record BookingResult(BookingStatus Status, BookingReceipt? Receipt = null);
public record PublishedSnapshot(string Id, TimeWindow Window, DateTime PublishedAtUtc, List<RouteOverviewDto> Routes);
public sealed class SnapshotNotFoundException(string id) : KeyNotFoundException($"Snapshot '{id}' does not exist.");
public sealed class SnapshotConflictException(string id) : InvalidOperationException($"Snapshot '{id}' belongs to another time window.");
public sealed class ApplicationHooks
{
    public Action? BeforePublishCommit { get; init; }
}
