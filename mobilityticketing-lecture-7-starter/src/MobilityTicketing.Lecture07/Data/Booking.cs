namespace MobilityTicketing.Lecture07.Data;

public class Booking
{
    public string RequestId { get; set; } = "";
    public string TripId { get; set; } = "";
    public DateTime BookedAtUtc { get; set; }
}
