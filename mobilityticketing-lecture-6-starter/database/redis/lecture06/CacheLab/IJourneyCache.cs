using StackExchange.Redis;

public interface IJourneyCache
{
    Task<string?> Get(string key);
    Task Set(string key, string value, TimeSpan expiry);
}

public sealed class RedisJourneyCache : IJourneyCache
{
    private readonly IDatabase database;

    public RedisJourneyCache(IDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);
        this.database = database;
    }

    public async Task<string?> Get(string key)
    {
        var value = await database.StringGetAsync(key);
        return value.HasValue ? value.ToString() : null;
    }

    public async Task Set(string key, string value, TimeSpan expiry)
    {
        await database.StringSetAsync(key, value, expiry);
    }
}
