using Npgsql;

namespace MobilityTicketing.Lecture07.Data;

public static class FixtureReset
{
    public static async Task ExecuteAsync(string connection, bool testOnly = false, CancellationToken ct = default)
    {
        LabDatabase.Validate(connection, testOnly);
        var sql = "drop schema if exists lab07 cascade;" + LabDatabase.ReadSql("01-schema.sql")
            + LabDatabase.ReadSql("03-read-model.sql") + LabDatabase.ReadSql("02-seed.sql");
        await using var c = new NpgsqlConnection(connection);
        await c.OpenAsync(ct);
        await using var tx = await c.BeginTransactionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, c, tx);
        await cmd.ExecuteNonQueryAsync(ct);
        await tx.CommitAsync(ct);
    }
}
