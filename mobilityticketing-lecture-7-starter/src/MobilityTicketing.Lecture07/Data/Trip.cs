namespace MobilityTicketing.Lecture07.Data;

public class Trip
{
    public string Id { get; set; } = ""; public string RouteId { get; set; } = ""; public DateTime DepartureUtc
    {
        get; set;
    }
    public string Status { get; set; } = "Scheduled"; public virtual Route Route { get; set; } = null!;
}
