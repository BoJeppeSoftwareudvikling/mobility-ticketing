using MobilityTicketing.Lecture07.Contracts;
using MobilityTicketing.Lecture07.Commands;
using MobilityTicketing.Lecture07.Data;
using MobilityTicketing.Lecture07.Diagnostics;
using MobilityTicketing.Lecture07.Queries;

namespace MobilityTicketing.Lecture07;

public sealed class StudentApplication(string connection, TimeProvider? clock = null,
    ApplicationHooks? hooks = null, RunMeasurement? measurement = null) : ILectureApplication
{
    public string Connection { get; } = connection;
    public TimeProvider Clock { get; } = clock ?? TimeProvider.System;
    public ApplicationHooks Hooks { get; } = hooks ?? new();
    public RunMeasurement Measurement { get; } = measurement ?? new();
    public LectureDbContext CreateContext() => LectureDbContext.Create(Connection, measurement: Measurement);

    public async Task<List<RouteOverviewDto>> ReadOverviewAsync(TimeWindow window, CancellationToken ct = default)
    {
        window.Validate();
        await using var db = CreateContext();
        return await RouteOverviewQuery.ExecuteAsync(db, window, ct);
    }

    public Task<CancelResult> CancelTripAsync(string tripId, CancellationToken ct = default) =>
        CancelTripCommand.ExecuteAsync(Connection, tripId, ct);

    public Task<RenameResult> RenameRouteAsync(string routeId, string name, CancellationToken ct = default) =>
        RenameRouteCommand.ExecuteAsync(Connection, routeId, name, ct);

    public Task<BookingResult> BookTripAsync(string tripId, string requestId, CancellationToken ct = default) =>
        BookTripCommand.ExecuteAsync(Connection, Clock, tripId, requestId, ct);

    public Task<PublishResult> PublishSnapshotAsync(string snapshotId, TimeWindow window, CancellationToken ct = default) =>
        PublishSnapshotCommand.ExecuteAsync(Connection, Clock, Hooks, snapshotId, window, ct);

    public async Task<PublishedSnapshot> ReadSnapshotAsync(string snapshotId, CancellationToken ct = default)
    {
        DatabaseSession.RequireText(snapshotId, nameof(snapshotId));
        await using var db = CreateContext();
        return await PublishedSnapshotQuery.ExecuteAsync(db, snapshotId, ct);
    }
}
