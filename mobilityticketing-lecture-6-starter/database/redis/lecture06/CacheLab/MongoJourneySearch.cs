using System.Globalization;
using System.Text.Json.Serialization;
using MongoDB.Bson;
using MongoDB.Driver;

public record SearchRequest(string CityId, string FromStopId, string ToStopId,
    DateTimeOffset Start, DateTimeOffset End);

public record Journey(
    [property: JsonRequired] string CityId,
    [property: JsonRequired] string RouteId,
    [property: JsonRequired] string FromStopId,
    [property: JsonRequired] string ToStopId,
    [property: JsonRequired] string TripId,
    [property: JsonRequired] DateTimeOffset DepartureUtc,
    [property: JsonRequired] DateTimeOffset ArrivalUtc,
    [property: JsonRequired] int AvailableSeats,
    [property: JsonRequired] decimal Price,
    [property: JsonRequired] string Currency);

public sealed class MongoJourneySearch
{
    private readonly IMongoCollection<BsonDocument> collection;
    private int calls;
    public int Calls => Volatile.Read(ref calls);
    public int DelayMilliseconds { get; set; } // Only for section 7.

    public MongoJourneySearch()
    {
        var settings = MongoClientSettings.FromConnectionString("mongodb://127.0.0.1:27017");
        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(2);
        settings.ConnectTimeout = TimeSpan.FromSeconds(1);
        collection = new MongoClient(settings).GetDatabase("mobility")
            .GetCollection<BsonDocument>("journey_search");
    }

    public async Task<List<Journey>> Fetch(SearchRequest q)
    {
        if (DelayMilliseconds > 0) await Task.Delay(DelayMilliseconds);
        Interlocked.Increment(ref calls);
        var range = new BsonDocument("$gte", new BsonDateTime(q.Start.UtcDateTime))
            .Add("$lt", new BsonDateTime(q.End.UtcDateTime));
        var pipeline = new[] {
            new BsonDocument("$match", new BsonDocument {
                {"_id", new BsonRegularExpression("^LAB05:")},
                {"cityId", q.CityId}, {"fromStopId", q.FromStopId}, {"toStopId", q.ToStopId}
            }),
            new BsonDocument("$unwind", "$departures"),
            new BsonDocument("$match", new BsonDocument {
                {"departures.status", "Scheduled"}, {"departures.departureUtc", range}
            }),
            new BsonDocument("$sort", new BsonDocument("departures.departureUtc", 1))
        };
        var documents = await collection.Aggregate<BsonDocument>(pipeline).ToListAsync();
        return documents.Select(d => {
            var t = d["departures"].AsBsonDocument;
            return new Journey(d["cityId"].AsString, d["routeId"].AsString,
                d["fromStopId"].AsString, d["toStopId"].AsString, t["tripId"].AsString,
                t["departureUtc"].AsBsonDateTime.ToUniversalTime(),
                t["arrivalUtc"].AsBsonDateTime.ToUniversalTime(),
                t["availableSeats"].ToInt32(),
                decimal.Parse(d["price"].ToString()!, CultureInfo.InvariantCulture),
                d["currency"].AsString);
        }).ToList();
    }
}
