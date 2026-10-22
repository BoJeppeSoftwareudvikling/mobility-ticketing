using Microsoft.EntityFrameworkCore;

namespace MobilityTicketing.Lecture07.Data;

public static class DatabaseSession
{
    // A failed callback or hook rolls back the whole write. Keys serialize retries.
    public static async Task<T> WriteAsync<T>(string connection, string key,
        Func<LectureDbContext, Task<T>> work, CancellationToken ct = default, Action? beforeCommit = null)
    {
        LabDatabase.Validate(connection);
        await using var db = LectureDbContext.Create(connection);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"select pg_advisory_xact_lock(hashtextextended({key}, 0))", ct);
        var result = await work(db);
        await db.SaveChangesAsync(ct);
        beforeCommit?.Invoke();
        await tx.CommitAsync(ct);
        return result;
    }

    // Booking and cancellation lock the same authoritative trip row.
    public static Task<Trip?> LockTripAsync(LectureDbContext db, string id, CancellationToken ct = default) =>
        db.Trips.FromSqlInterpolated($"select * from lab07.trips where id={id} for update")
            .SingleOrDefaultAsync(ct);

    public static void RequireText(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException($"{name} must not be empty.");
    }
}
