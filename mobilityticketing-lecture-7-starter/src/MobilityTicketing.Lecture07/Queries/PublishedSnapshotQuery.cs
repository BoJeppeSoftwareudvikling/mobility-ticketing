using Microsoft.EntityFrameworkCore;
using MobilityTicketing.Lecture07.Contracts;
using MobilityTicketing.Lecture07.Data;

namespace MobilityTicketing.Lecture07.Queries;

public static class PublishedSnapshotQuery
{
    public static async Task<PublishedSnapshot> ExecuteAsync(
        LectureDbContext db,
        string snapshotId,
        CancellationToken ct = default)
    {
        DatabaseSession.RequireText(snapshotId, nameof(snapshotId));

        var plan = await db.PublishedPlans
            .AsNoTracking()
            .Include(p => p.Routes)
            .AsSingleQuery()
            .SingleOrDefaultAsync(p => p.Id == snapshotId, ct);

        if (plan is null) throw new SnapshotNotFoundException(snapshotId);

        var routes = plan.Routes
            .OrderBy(route => route.RouteId)
            .Select(route => new RouteOverviewDto(
                route.RouteId,
                route.RouteName,
                route.ScheduledTripCount,
                route.NextDepartureUtc))
            .ToList();

        return new PublishedSnapshot(
            plan.Id,
            new TimeWindow(plan.FromUtc, plan.ToUtc),
            plan.PublishedAtUtc,
            routes);
    }
}
