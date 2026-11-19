using System.Security.Cryptography;

namespace MobilityTicketing.Reports.Application;

public sealed class ReportWorkflow(
    IReportObjectStore storage, IReportCatalogue catalogue, IUploadReceipts receipts, ExportLocation destination)
{
    private readonly IReportObjectStore _storage = storage;
    private readonly IReportCatalogue _catalogue = catalogue;
    private readonly IUploadReceipts _receipts = receipts;
    private readonly ExportLocation _destination = destination;

    public async Task<string> StageAsync(string reportId, byte[] bytes)
    {
        var (sha256, rowCount) = Measure(bytes);
        _receipts.Reserve(reportId);

        // Keep the reservation if PUT or receipt writing fails: the upload may already exist.
        var versionId = await _storage.UploadAsync(_destination, bytes);
        if (string.IsNullOrWhiteSpace(versionId) || versionId is "null" or "None")
            throw new InvalidOperationException("Upload returned an invalid version ID. Investigate the reserved upload before retrying.");

        await _receipts.WriteAsync(new ReportInput(
            reportId, _destination.Bucket, _destination.Key, versionId, sha256, rowCount));
        return versionId;
    }

    public async Task<string> PublishAsync(string reportId, byte[] bytes, string? supersedes)
    {
        await StageAsync(reportId, bytes);
        return await RecoverAsync(reportId, supersedes);
    }

    public async Task<string> RecoverAsync(string reportId, string? supersedes)
    {
        var input = await _receipts.ReadAsync(reportId);
        var result = await CheckAsync(input);
        if (result != Verification.Matches)
            throw new InvalidOperationException($"Report {reportId} verification failed: {result}.");

        await _catalogue.SaveAsync(input, supersedes);
        return $"Saved {input.ReportId}";
    }

    public async Task<Verification> VerifyAsync(string reportId)
    {
        ReportInput? input;
        try
        {
            input = await _catalogue.FindAsync(reportId);
        }
        catch (CatalogueException)
        {
            return Verification.CatalogueReadFailed;
        }

        return input is null ? Verification.UnknownReport : await CheckAsync(input);
    }

    private async Task<Verification> CheckAsync(ReportInput input)
    {
        byte[] bytes;
        try
        {
            bytes = await _storage.ReadVersionAsync(new ExportLocation(input.Bucket, input.Key), input.VersionId);
        }
        catch (ObjectStoreException)
        {
            return Verification.ReadFailed;
        }

        var (sha256, rowCount) = Measure(bytes);
        return sha256 == input.Sha256 && rowCount == input.RowCount
            ? Verification.Matches
            : Verification.Mismatch;
    }

    private static (string Sha256, int RowCount) Measure(byte[] bytes) =>
        (Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(), bytes.Count(value => value == (byte)'\n'));
}
