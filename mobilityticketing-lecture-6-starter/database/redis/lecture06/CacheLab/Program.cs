using System.Text.Json;

var source = new MongoJourneySearch();
var request = new SearchRequest("CPH", "STOP-NORREPORT", "STOP-AIRPORT",
    DateTimeOffset.Parse("2026-10-02T06:00:00Z"),
    DateTimeOffset.Parse("2026-10-02T07:00:00Z"));

var result = await source.Fetch(request);
Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
Console.WriteLine($"MongoDB calls: {source.Calls}");
// Next: replace the direct Fetch call with your cached search.
