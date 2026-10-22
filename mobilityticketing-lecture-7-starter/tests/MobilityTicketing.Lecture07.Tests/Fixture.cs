using Microsoft.EntityFrameworkCore;
using Npgsql;
using MobilityTicketing.Lecture07.Contracts;
using MobilityTicketing.Lecture07.Data;
using MobilityTicketing.Lecture07.Diagnostics;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]
namespace MobilityTicketing.Lecture07.Tests;

public sealed class FixedClock : TimeProvider
{
    public DateTimeOffset Now { get; set; } = new(2026, 10, 23, 5, 55, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => Now;
}

public abstract class Fixture : IAsyncLifetime
{
    protected string Connection => LabDatabase.TestConnection;
    protected FixedClock Clock { get; } = new();
    protected StudentApplication App => new(Connection, Clock);
    public async Task InitializeAsync() => await FixtureReset.ExecuteAsync(Connection, testOnly: true);
    public Task DisposeAsync() => Task.CompletedTask;
    protected LectureDbContext Db() => LectureDbContext.Create(Connection);
    protected static readonly RouteOverviewDto[] Baseline = [
        new("R1", "City Loop", 2, Utc(6, 0)),
        new("R2", "Campus Line", 2, Utc(6, 10)),
        new("R3", "Airport Express", 0, null),
        new("R4", "Harbour Shuttle", 0, null)
    ];
    protected static DateTime Utc(int hour, int minute) => new(2026, 10, 23, hour, minute, 0, DateTimeKind.Utc);
    protected static void AssertRows(IEnumerable<RouteOverviewDto> expected, IEnumerable<RouteOverviewDto> actual) =>
        Assert.Equal(expected.ToArray(), actual.ToArray());
    protected async Task Sql(string sql)
    {
        await using var c = new NpgsqlConnection(Connection);
        await c.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, c);
        await cmd.ExecuteNonQueryAsync();
    }

    // Read all lab tables without assuming how students store snapshots.
    protected async Task<string> State()
    {
        await using var c = new NpgsqlConnection(Connection);
        await c.OpenAsync();
        var names = new List<string>();
        await using (var cmd = new NpgsqlCommand("select tablename from pg_tables where schemaname='lab07' order by tablename", c))
        await using (var reader = await cmd.ExecuteReaderAsync())
            while (await reader.ReadAsync()) names.Add(reader.GetString(0));
        var parts = new List<string>();
        foreach (var name in names)
        {
            var quoted = '"' + name.Replace("\"", "\"\"") + '"';
            await using var cmd = new NpgsqlCommand($"select coalesce(jsonb_agg(to_jsonb(t) order by to_jsonb(t)::text),'[]'::jsonb)::text from lab07.{quoted} t", c);
            parts.Add(name + ":" + (string)(await cmd.ExecuteScalarAsync())!);
        }
        return string.Join("\n", parts);
    }
}
