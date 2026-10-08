using System.Text.Json;
using Xunit;

public sealed class CachedJourneySearchTests
{
    private static readonly SearchRequest Morning = new(
        "CPH",
        "STOP-NORREPORT",
        "STOP-AIRPORT",
        DateTimeOffset.Parse("2026-10-02T06:00:00Z"),
        DateTimeOffset.Parse("2026-10-02T07:00:00Z"));

    private static readonly Journey MorningJourney = new(
        "CPH",
        "LINE-M2",
        "STOP-NORREPORT",
        "STOP-AIRPORT",
        "LAB05-T-OK",
        DateTimeOffset.Parse("2026-10-02T06:20:00Z"),
        DateTimeOffset.Parse("2026-10-02T06:40:00Z"),
        10,
        36.00m,
        "DKK");

    [Fact]
    public async Task RepeatedSearchUsesCachedResultWithoutAnotherSourceCall()
    {
        var source = new FakeJourneySource([MorningJourney]);
        var cache = new FakeJourneyCache();
        var messages = new List<string>();
        var search = new CachedJourneySearch(
            source,
            cache,
            TimeSpan.FromSeconds(20),
            messages.Add);

        var first = await search.Fetch(Morning);
        var second = await search.Fetch(Morning);

        Assert.Equal(["LAB05-T-OK"], first.Select(item => item.TripId));
        Assert.Equal(["LAB05-T-OK"], second.Select(item => item.TripId));
        Assert.Equal(1, source.Calls);
        Assert.Equal(1, cache.SetCalls);
        Assert.Equal(TimeSpan.FromSeconds(20), cache.LastExpiry);
        Assert.Equal(["miss", "hit"], messages);
    }

    [Fact]
    public async Task EmptyResultIsCachedAsAHit()
    {
        var source = new FakeJourneySource([]);
        var cache = new FakeJourneyCache();
        var messages = new List<string>();
        var search = new CachedJourneySearch(
            source,
            cache,
            TimeSpan.FromSeconds(20),
            messages.Add);

        var first = await search.Fetch(Morning);
        var second = await search.Fetch(Morning);

        Assert.Empty(first);
        Assert.Empty(second);
        Assert.Equal(1, source.Calls);
        Assert.Equal(1, cache.SetCalls);
        Assert.Equal(["miss", "hit"], messages);
    }

    [Fact]
    public async Task ExpiredResultIsFetchedAndCachedAgain()
    {
        var source = new FakeJourneySource([MorningJourney]);
        var cache = new FakeJourneyCache();
        var messages = new List<string>();
        var search = new CachedJourneySearch(
            source,
            cache,
            TimeSpan.FromSeconds(20),
            messages.Add);

        await search.Fetch(Morning);
        cache.Remove(CacheKey.Build(Morning));
        var result = await search.Fetch(Morning);

        Assert.Equal(["LAB05-T-OK"], result.Select(item => item.TripId));
        Assert.Equal(2, source.Calls);
        Assert.Equal(2, cache.SetCalls);
        Assert.Equal(["miss", "miss"], messages);
    }

    [Fact]
    public async Task ConcurrentMissesShareOneSourceLookup()
    {
        var source = new FakeJourneySource([MorningJourney], delayMilliseconds: 200);
        var cache = new FakeJourneyCache();
        var messages = new List<string>();
        var search = new CachedJourneySearch(
            source,
            cache,
            TimeSpan.FromSeconds(20),
            messages.Add);

        var results = await Task.WhenAll(
            Enumerable.Range(0, 10).Select(_ => search.Fetch(Morning)));

        Assert.All(
            results,
            result => Assert.Equal(["LAB05-T-OK"], result.Select(item => item.TripId)));
        Assert.Equal(1, source.Calls);
        Assert.Equal(1, cache.SetCalls);
        Assert.Equal(10, messages.Count(message => message == "miss"));
        Assert.Equal(9, messages.Count(message => message == "coalesced"));
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("{\"schemaVersion\":1,\"cachedAtUtc\":\"2026-10-08T08:30:00Z\",\"items\":[]}")]
    [InlineData("{\"schemaVersion\":2,\"cachedAtUtc\":\"2026-10-08T08:30:00Z\"}")]
    public async Task InvalidValueIsReplacedFromSource(string invalidJson)
    {
        var source = new FakeJourneySource([MorningJourney]);
        var cache = new FakeJourneyCache();
        var messages = new List<string>();
        var key = CacheKey.Build(Morning);
        cache.Values[key] = invalidJson;
        var search = new CachedJourneySearch(
            source,
            cache,
            TimeSpan.FromSeconds(20),
            messages.Add);

        var result = await search.Fetch(Morning);

        Assert.Equal(["LAB05-T-OK"], result.Select(item => item.TripId));
        Assert.Equal(1, source.Calls);
        Assert.Equal(1, cache.SetCalls);
        Assert.Equal(["invalid"], messages);

        using var replacement = JsonDocument.Parse(cache.Values[key]);
        Assert.Equal(2, replacement.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(
            "LAB05-T-OK",
            replacement.RootElement.GetProperty("items")[0].GetProperty("tripId").GetString());
    }

    private sealed class FakeJourneySource : IJourneySource
    {
        private readonly List<Journey> items;
        private readonly int delayMilliseconds;
        private int calls;

        public FakeJourneySource(List<Journey> items, int delayMilliseconds = 0)
        {
            this.items = items;
            this.delayMilliseconds = delayMilliseconds;
        }

        public int Calls => Volatile.Read(ref calls);

        public async Task<List<Journey>> Fetch(SearchRequest request)
        {
            Interlocked.Increment(ref calls);

            if (delayMilliseconds > 0)
            {
                await Task.Delay(delayMilliseconds);
            }

            return items.ToList();
        }
    }

    private sealed class FakeJourneyCache : IJourneyCache
    {
        public Dictionary<string, string> Values { get; } = [];
        public int SetCalls { get; private set; }
        public TimeSpan? LastExpiry { get; private set; }

        public Task<string?> Get(string key)
        {
            Values.TryGetValue(key, out var value);
            return Task.FromResult(value);
        }

        public Task Set(string key, string value, TimeSpan expiry)
        {
            Values[key] = value;
            SetCalls++;
            LastExpiry = expiry;
            return Task.CompletedTask;
        }

        public void Remove(string key)
        {
            Values.Remove(key);
        }
    }
}
