using Microsoft.EntityFrameworkCore;
using MobilityTicketing.Lecture07.Contracts;
using MobilityTicketing.Lecture07.Data;

namespace MobilityTicketing.Lecture07.Queries;

public static class RouteOverviewQuery
{
    public static Task<List<RouteOverviewDto>> ExecuteAsync(
        LectureDbContext db,
        TimeWindow window,
        CancellationToken ct = default)
    {
        window.Validate();

        return db.Routes
            .AsNoTracking()
            .OrderBy(route => route.Id)
            .Select(route => new RouteOverviewDto(
                route.Id,
                route.Name,
                route.Trips.Count(trip =>
                    trip.Status == "Scheduled" &&
                    trip.DepartureUtc >= window.FromUtc &&
                    trip.DepartureUtc < window.ToUtc),
                route.Trips
                    .Where(trip =>
                        trip.Status == "Scheduled" &&
                        trip.DepartureUtc >= window.FromUtc &&
                        trip.DepartureUtc < window.ToUtc)
                    .Min(trip => (DateTime?)trip.DepartureUtc)))
            .ToListAsync(ct);
    }
}
