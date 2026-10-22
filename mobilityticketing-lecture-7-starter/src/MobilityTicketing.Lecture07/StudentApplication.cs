using MobilityTicketing.Lecture07.Contracts;
using MobilityTicketing.Lecture07.Data;
using MobilityTicketing.Lecture07.Diagnostics;

namespace MobilityTicketing.Lecture07;

public sealed class StudentApplication(string connection, TimeProvider? clock = null,
    ApplicationHooks? hooks = null, RunMeasurement? measurement = null) : ILectureApplication
{
    public string Connection { get; } = connection;
    public TimeProvider Clock { get; } = clock ?? TimeProvider.System;
    public ApplicationHooks Hooks { get; } = hooks ?? new();
    public RunMeasurement Measurement { get; } = measurement ?? new();
    public LectureDbContext CreateContext() => LectureDbContext.Create(Connection, measurement: Measurement);

    // Connect your command and query classes here.
    public Task<List<RouteOverviewDto>> ReadOverviewAsync(TimeWindow window, CancellationToken ct = default) =>
        throw new NotImplementedException("Implement the live route-board query.");
    public Task<CancelResult> CancelTripAsync(string tripId, CancellationToken ct = default) =>
        throw new NotImplementedException("Implement trip cancellation.");
    public Task<RenameResult> RenameRouteAsync(string routeId, string name, CancellationToken ct = default) =>
        throw new NotImplementedException("Implement route renaming.");
    public Task<BookingResult> BookTripAsync(string tripId, string requestId, CancellationToken ct = default) =>
        throw new NotImplementedException("Implement booking and request retries.");
    public Task<PublishResult> PublishSnapshotAsync(string snapshotId, TimeWindow window, CancellationToken ct = default) =>
        throw new NotImplementedException("Design the snapshot storage and implement publication.");
    public Task<PublishedSnapshot> ReadSnapshotAsync(string snapshotId, CancellationToken ct = default) =>
        throw new NotImplementedException("Implement the published-plan query.");
}
