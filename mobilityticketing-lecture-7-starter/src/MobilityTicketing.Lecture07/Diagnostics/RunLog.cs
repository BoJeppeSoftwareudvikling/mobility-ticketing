using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MobilityTicketing.Lecture07.Contracts;
using MobilityTicketing.Lecture07.Data;
using MobilityTicketing.Lecture07.Queries;

namespace MobilityTicketing.Lecture07.Diagnostics;

public record OverviewRun(string Strategy, DateTime FromUtc, DateTime ToUtc, List<RouteOverviewDto> Rows,
    int ExecutedReadCommands, int MaterializedRoutes, int MaterializedTrips, List<SqlCommandLog> Commands);

public static class RunLog
{
    public static async Task<OverviewRun> ExecuteAsync(string connection, string strategy, TimeWindow? window = null,
        string? snapshotId = null, CancellationToken ct = default)
    {
        var w = window ?? TimeWindow.Morning;
        var m = new RunMeasurement();
        List<RouteOverviewDto> rows;
        m.Active = true;
        try
        {
            if (strategy == "baseline")
            {
                await using var db = LectureDbContext.Create(connection, lazy: true, measurement: m);
                await db.Database.OpenConnectionAsync(ct);
                rows = await LazyRouteOverviewQuery.ExecuteAsync(db, w.FromUtc, w.ToUtc, ct);
            }
            else
            {
                var app = new StudentApplication(connection, measurement: m);
                if (strategy == "live") rows = await app.ReadOverviewAsync(w, ct);
                else if (strategy == "snapshot" && snapshotId is not null)
                {
                    var saved = await app.ReadSnapshotAsync(snapshotId, ct);
                    w = saved.Window; rows = saved.Routes;
                    Console.WriteLine($"Snapshot {saved.Id}, published {saved.PublishedAtUtc:O}");
                }
                else throw new ArgumentException("Use baseline, overview or snapshot ID.");
            }
        }
        finally { m.Active = false; }
        return new(strategy, w.FromUtc, w.ToUtc, rows, m.ExecutedReadCommands,
            m.MaterializedRoutes, m.MaterializedTrips, m.Commands);
    }

    public static async Task SaveAsync(OverviewRun run)
    {
        var root = Environment.GetEnvironmentVariable("LAB_EVIDENCE") ?? "docs/evidence/lecture07";
        Directory.CreateDirectory(Path.Combine(root, "logs"));
        var id = DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ") + "-" + run.Strategy + "-" + Guid.NewGuid().ToString("N")[..8];
        var prefix = Path.Combine(root, "logs", id);
        var json = JsonSerializer.Serialize(run, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(prefix + ".json", json);
        var sql = string.Join("\n\n", run.Commands.Select(c => "-- Parameters: " + JsonSerializer.Serialize(c.Parameters) + "\n" + c.Sql));
        await File.WriteAllTextAsync(prefix + ".sql", sql);
        Console.WriteLine(json);
        Console.WriteLine($"SQL: docs/evidence/lecture07/logs/{id}.sql");
    }
}
