using System.Diagnostics;
using System.Text.Json;
using StackExchange.Redis;

var stopwatch = Stopwatch.StartNew();
var source = new MongoJourneySearch();
var mongoDelayText = Environment.GetEnvironmentVariable("MONGO_DELAY_MS");
var mongoDelayMilliseconds = 0;

if (mongoDelayText is not null &&
    (!int.TryParse(mongoDelayText, out mongoDelayMilliseconds) || mongoDelayMilliseconds < 0))
{
    throw new ArgumentException("MONGO_DELAY_MS must be a non-negative integer.");
}

source.DelayMilliseconds = mongoDelayMilliseconds;
var options = ConfigurationOptions.Parse("127.0.0.1:6379");
options.AbortOnConnectFail = false;
options.ConnectTimeout = 500;
options.AsyncTimeout = 500;
options.ConnectRetry = 1;

using var redis = await ConnectionMultiplexer.ConnectAsync(options);
var expirySecondsText = Environment.GetEnvironmentVariable("CACHE_EXPIRY_SECONDS");
var expirySeconds = 20;

if (expirySecondsText is not null &&
    (!int.TryParse(expirySecondsText, out expirySeconds) || expirySeconds <= 0))
{
    throw new ArgumentException("CACHE_EXPIRY_SECONDS must be a positive integer.");
}

var search = new CachedJourneySearch(
    source,
    new RedisJourneyCache(redis.GetDatabase()),
    TimeSpan.FromSeconds(expirySeconds));
var searchPeriod = args.Length == 0 ? "morning" : args[0].ToLowerInvariant();
var runBatch = args.Length > 1 && args[1].Equals("batch", StringComparison.OrdinalIgnoreCase);

if (args.Length > 2 || (args.Length > 1 && !runBatch))
{
    throw new ArgumentException("Usage: CacheLab [morning|evening] [batch]");
}

var (start, end) = searchPeriod switch
{
    "morning" => (
        DateTimeOffset.Parse("2026-10-02T06:00:00Z"),
        DateTimeOffset.Parse("2026-10-02T07:00:00Z")),
    "evening" => (
        DateTimeOffset.Parse("2026-10-02T18:00:00Z"),
        DateTimeOffset.Parse("2026-10-02T19:00:00Z")),
    _ => throw new ArgumentException("Search period must be 'morning' or 'evening'.")
};
var request = new SearchRequest(
    "CPH",
    "STOP-NORREPORT",
    "STOP-AIRPORT",
    start,
    end);

Console.WriteLine($"Search: {searchPeriod}");
Console.WriteLine($"Cache expiry: {expirySeconds} seconds");
Console.WriteLine($"MongoDB delay: {mongoDelayMilliseconds} ms");
Console.WriteLine($"Cache key: {CacheKey.Build(request)}");

try
{
    var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);

    if (runBatch)
    {
        var results = await Task.WhenAll(
            Enumerable.Range(0, 10).Select(_ => search.Fetch(request)));
        var tripIds = results
            .Select(items => items.Select(item => item.TripId).ToArray())
            .ToArray();

        Console.WriteLine($"Batch requests: {results.Length}");
        Console.WriteLine($"Returned trip IDs: {JsonSerializer.Serialize(tripIds, jsonOptions)}");
    }
    else
    {
        var result = await search.Fetch(request);
        Console.WriteLine(JsonSerializer.Serialize(result, jsonOptions));
    }
}
catch (Exception exception)
{
    Console.WriteLine($"search error: {exception.Message}");
    Environment.ExitCode = 1;
}
finally
{
    stopwatch.Stop();
    Console.WriteLine($"MongoDB calls: {source.Calls}");
    Console.WriteLine($"Search elapsed: {stopwatch.Elapsed.TotalMilliseconds:F0} ms");
}
