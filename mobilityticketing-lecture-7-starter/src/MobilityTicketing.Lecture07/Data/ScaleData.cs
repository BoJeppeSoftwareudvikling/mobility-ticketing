using Npgsql;

namespace MobilityTicketing.Lecture07.Data;

public static class ScaleData
{
    public static async Task ExecuteAsync(string connection, CancellationToken ct = default)
    {
        LabDatabase.Validate(connection);
        await using var c = new NpgsqlConnection(connection);
        await c.OpenAsync(ct);
        await using var tx = await c.BeginTransactionAsync(ct);
        await using var cmd = new NpgsqlCommand(LabDatabase.ReadSql("04-scale.sql"), c, tx);
        await cmd.ExecuteNonQueryAsync(ct);
        await tx.CommitAsync(ct);
    }
}
