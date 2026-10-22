namespace MobilityTicketing.Lecture07.Contracts;

public record RouteOverviewDto(string RouteId, string RouteName, int ScheduledTripCount, DateTime? NextDepartureUtc);
