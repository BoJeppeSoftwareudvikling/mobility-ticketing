using Microsoft.EntityFrameworkCore;
using MobilityTicketing.Lecture07.Data;
using MobilityTicketing.Lecture07.Contracts;

namespace MobilityTicketing.Lecture07.Queries;

public static class LazyRouteOverviewQuery
{
    public static async Task<List<RouteOverviewDto>> ExecuteAsync(LectureDbContext db, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
    {
        LabWindow.Validate(fromUtc, toUtc);
        var routes = await db.Routes.OrderBy(r => r.Id).ToListAsync(ct);
        return RouteOverviewMapper.Map(routes, fromUtc, toUtc);
    }
}
