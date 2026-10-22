namespace MobilityTicketing.Lecture07.Data;

public class Route
{
    public string Id { get; set; } = ""; public string Name { get; set; } = ""; public virtual ICollection<Trip> Trips { get; set; } = new List<Trip>();
}
