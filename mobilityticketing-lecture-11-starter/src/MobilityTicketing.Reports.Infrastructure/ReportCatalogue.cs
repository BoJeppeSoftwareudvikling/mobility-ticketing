using MobilityTicketing.Reports.Application;
using Npgsql;
using NpgsqlTypes;

namespace MobilityTicketing.Reports.Infrastructure;

internal sealed class ReportCatalogue(NpgsqlDataSource database) : IReportCatalogue
{
    public async Task<ReportInput?> FindAsync(string id)
    {
        try { return await ReadAsync(id); }
        catch (NpgsqlException ex) { throw new CatalogueException(ex.Message, ex); }
    }

    public async Task SaveAsync(ReportInput input, string? supersedes)
    {
        try { await InsertAsync(input, supersedes); }
        catch (NpgsqlException ex) { throw new CatalogueException(ex.Message, ex); }
    }

    private async Task<ReportInput?> ReadAsync(string id)
    {
        await using var cmd = database.CreateCommand("""
            SELECT bucket_name, object_key, version_id, sha256, row_count
            FROM reports WHERE report_id = $1
            """);
        cmd.Parameters.AddWithValue(id);
        await using var reader = await cmd.ExecuteReaderAsync();
        return await reader.ReadAsync()
            ? new ReportInput(id, reader.GetString(0), reader.GetString(1), reader.GetString(2),
                reader.GetString(3), reader.GetInt32(4))
            : null;
    }

    private async Task InsertAsync(ReportInput input, string? supersedes)
    {
        await using var connection = await database.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using var insert = new NpgsqlCommand("""
            INSERT INTO reports (report_id, bucket_name, object_key, version_id, sha256, row_count, supersedes)
            VALUES ($1, $2, $3, $4, $5, $6, $7) ON CONFLICT (report_id) DO NOTHING
            """, connection, transaction);
        insert.Parameters.AddWithValue(input.ReportId);
        insert.Parameters.AddWithValue(input.Bucket);
        insert.Parameters.AddWithValue(input.Key);
        insert.Parameters.AddWithValue(input.VersionId);
        insert.Parameters.AddWithValue(input.Sha256);
        insert.Parameters.AddWithValue(input.RowCount);
        insert.Parameters.AddWithValue(NpgsqlDbType.Text, (object?)supersedes ?? DBNull.Value);
        if (await insert.ExecuteNonQueryAsync() == 0)
        {
            await using var existing = new NpgsqlCommand("""
                SELECT EXISTS (
                    SELECT 1 FROM reports WHERE report_id = $1 AND bucket_name = $2 AND object_key = $3
                    AND version_id = $4 AND sha256 = $5 AND row_count = $6 AND supersedes IS NOT DISTINCT FROM $7)
                """, connection, transaction);
            foreach (NpgsqlParameter parameter in insert.Parameters)
                existing.Parameters.AddWithValue(parameter.NpgsqlDbType, parameter.Value!);
            if (await existing.ExecuteScalarAsync() is not true)
                throw new InvalidOperationException("ReportConflict");
        }
        await transaction.CommitAsync();
    }
}
