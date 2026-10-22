using MobilityTicketing.Lecture07.Data;
using MobilityTicketing.Lecture07.Contracts;

namespace MobilityTicketing.Lecture07.Queries;

public static class RouteOverviewMapper
{
    public static List<RouteOverviewDto> Map(
        IEnumerable<Route> routes, DateTime fromUtc, DateTime toUtc)
    {
        return routes.Select(route =>
        {
            var matching = route.Trips
                .Where(trip =>
                    trip.Status == "Scheduled" &&
                    trip.DepartureUtc >= fromUtc &&
                    trip.DepartureUtc < toUtc)
                .ToList();

            return new RouteOverviewDto(
                route.Id,
                route.Name,
                matching.Count,
                matching.Select(trip => (DateTime?)trip.DepartureUtc).Min());
        }).ToList();
    }
}
