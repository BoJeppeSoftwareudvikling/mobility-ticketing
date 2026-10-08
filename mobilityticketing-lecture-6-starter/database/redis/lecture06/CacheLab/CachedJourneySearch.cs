using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using StackExchange.Redis;

public sealed class CachedJourneySearch
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly IJourneySource source;
    private readonly IJourneyCache cache;
    private readonly TimeSpan expiry;
    private readonly Action<string> log;
    private readonly ConcurrentDictionary<string, Lazy<Task<List<Journey>>>> inFlight = new();

    public CachedJourneySearch(
        IJourneySource source,
        IJourneyCache cache,
        TimeSpan expiry,
        Action<string>? log = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(cache);

        if (expiry <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(expiry),
                "Cache expiry must be greater than zero.");
        }

        this.source = source;
        this.cache = cache;
        this.expiry = expiry;
        this.log = log ?? Console.WriteLine;
    }

    public async Task<List<Journey>> Fetch(SearchRequest request)
    {
        // Building the key validates the request before Redis is accessed.
        var key = CacheKey.Build(request);
        var cacheAvailable = true;

        try
        {
            var cachedJson = await cache.Get(key);
            var cacheState = ReadEnvelope(cachedJson, out var cachedItems);

            if (cacheState == CacheState.Hit)
            {
                log("hit");
                return cachedItems;
            }

            log(cacheState == CacheState.Invalid ? "invalid" : "miss");
        }
        catch (Exception exception) when (IsCacheFailure(exception))
        {
            log("cache unavailable");
            cacheAvailable = false;
        }

        var candidate = new Lazy<Task<List<Journey>>>(
            () => FetchAndCache(request, key, cacheAvailable),
            LazyThreadSafetyMode.ExecutionAndPublication);
        var pending = inFlight.GetOrAdd(key, candidate);

        if (!ReferenceEquals(candidate, pending))
        {
            log("coalesced");
        }

        try
        {
            return (await pending.Value).ToList();
        }
        finally
        {
            var entry = new KeyValuePair<string, Lazy<Task<List<Journey>>>>(key, pending);
            ((ICollection<KeyValuePair<string, Lazy<Task<List<Journey>>>>>)inFlight)
                .Remove(entry);
        }
    }

    private async Task<List<Journey>> FetchAndCache(
        SearchRequest request,
        string key,
        bool cacheAvailable)
    {
        var items = await source.Fetch(request);

        if (!cacheAvailable)
        {
            return items;
        }

        var envelope = new CacheEnvelope(2, DateTime.UtcNow, items);
        var json = JsonSerializer.Serialize(envelope, JsonOptions);

        try
        {
            await cache.Set(key, json, expiry);
        }
        catch (Exception exception) when (IsCacheFailure(exception))
        {
            log("cache unavailable");
        }

        return items;
    }

    private static CacheState ReadEnvelope(string? cachedJson, out List<Journey> items)
    {
        items = [];

        if (cachedJson is null)
        {
            return CacheState.Miss;
        }

        try
        {
            var envelope = JsonSerializer.Deserialize<CacheEnvelope>(
                cachedJson,
                JsonOptions);

            if (envelope is null ||
                envelope.SchemaVersion != 2 ||
                envelope.CachedAtUtc == default ||
                envelope.CachedAtUtc.Kind != DateTimeKind.Utc ||
                envelope.Items is null ||
                envelope.Items.Any(item => !IsValid(item)))
            {
                return CacheState.Invalid;
            }

            items = envelope.Items;
            return CacheState.Hit;
        }
        catch (JsonException)
        {
            return CacheState.Invalid;
        }
    }

    private static bool IsValid(Journey? item) =>
        item is not null &&
        !string.IsNullOrWhiteSpace(item.CityId) &&
        !string.IsNullOrWhiteSpace(item.RouteId) &&
        !string.IsNullOrWhiteSpace(item.FromStopId) &&
        !string.IsNullOrWhiteSpace(item.ToStopId) &&
        !string.IsNullOrWhiteSpace(item.TripId) &&
        !string.IsNullOrWhiteSpace(item.Currency) &&
        item.DepartureUtc != default &&
        item.ArrivalUtc > item.DepartureUtc &&
        item.AvailableSeats >= 0 &&
        item.Price >= 0;

    private static bool IsCacheFailure(Exception exception) =>
        exception is RedisException or TimeoutException;

    private sealed record CacheEnvelope(
        [property: JsonRequired] int SchemaVersion,
        [property: JsonRequired] DateTime CachedAtUtc,
        [property: JsonRequired] List<Journey> Items);

    private enum CacheState
    {
        Miss,
        Invalid,
        Hit
    }
}
