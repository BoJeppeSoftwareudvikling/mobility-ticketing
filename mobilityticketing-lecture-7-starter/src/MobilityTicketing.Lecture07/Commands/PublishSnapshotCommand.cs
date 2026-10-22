using Microsoft.EntityFrameworkCore;
using MobilityTicketing.Lecture07.Contracts;
using MobilityTicketing.Lecture07.Data;
using MobilityTicketing.Lecture07.Queries;

namespace MobilityTicketing.Lecture07.Commands;

public static class PublishSnapshotCommand
{
    public static Task<PublishResult> ExecuteAsync(
        string connection,
        TimeProvider clock,
        ApplicationHooks hooks,
        string snapshotId,
        TimeWindow window,
        CancellationToken ct = default)
    {
        DatabaseSession.RequireText(snapshotId, nameof(snapshotId));
        window.Validate();

        return DatabaseSession.WriteAsync(
            connection,
            $"snapshot:{snapshotId}",
            async db =>
            {
                var existing = await db.PublishedPlans
                    .AsNoTracking()
                    .SingleOrDefaultAsync(plan => plan.Id == snapshotId, ct);

                if (existing is not null)
                {
                    if (existing.FromUtc != window.FromUtc || existing.ToUtc != window.ToUtc)
                        throw new SnapshotConflictException(snapshotId);

                    return PublishResult.AlreadyPublished;
                }

                var rows = await RouteOverviewQuery.ExecuteAsync(db, window, ct);
                var plan = new PublishedPlan
                {
                    Id = snapshotId,
                    FromUtc = window.FromUtc,
                    ToUtc = window.ToUtc,
                    PublishedAtUtc = DatabaseTimestamp.Now(clock),
                    Routes = rows.Select(row => new PublishedPlanRoute
                    {
                        PlanId = snapshotId,
                        RouteId = row.RouteId,
                        RouteName = row.RouteName,
                        ScheduledTripCount = row.ScheduledTripCount,
                        NextDepartureUtc = row.NextDepartureUtc
                    }).ToList()
                };

                db.PublishedPlans.Add(plan);
                return PublishResult.Published;
            },
            ct,
            hooks.BeforePublishCommit);
    }
}
