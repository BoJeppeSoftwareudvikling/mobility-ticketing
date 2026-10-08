using System.Globalization;

public static class CacheKey
{
    public static string Build(SearchRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.CityId) ||
            string.IsNullOrWhiteSpace(request.FromStopId) ||
            string.IsNullOrWhiteSpace(request.ToStopId))
        {
            throw new ArgumentException("City, origin, and destination IDs are required.");
        }

        if (request.End <= request.Start)
        {
            throw new ArgumentException("End time must be after start time.");
        }

        static string Part(string value) => Uri.EscapeDataString(value);

        static string Instant(DateTimeOffset value) =>
            value.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);

        return string.Join(':',
            "search",
            "LAB06",
            "v2",
            Part(request.CityId),
            Part(request.FromStopId),
            Part(request.ToStopId),
            Part(Instant(request.Start)),
            Part(Instant(request.End)));
    }
}
