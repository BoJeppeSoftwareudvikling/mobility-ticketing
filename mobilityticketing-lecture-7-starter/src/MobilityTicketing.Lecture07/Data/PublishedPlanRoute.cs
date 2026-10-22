namespace MobilityTicketing.Lecture07.Data;

public class PublishedPlanRoute
{
    public string PlanId { get; set; } = "";
    public string RouteId { get; set; } = "";
    public string RouteName { get; set; } = "";
    public int ScheduledTripCount { get; set; }
    public DateTime? NextDepartureUtc { get; set; }
    public virtual PublishedPlan Plan { get; set; } = null!;
}
