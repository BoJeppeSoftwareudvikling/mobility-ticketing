using System.Text.Json;
using MobilityTicketing.Reports.Application;

namespace MobilityTicketing.Reports.Infrastructure;

internal sealed class UploadReceipts(string directory) : IUploadReceipts
{
    public void Reserve(string reportId)
    {
        Directory.CreateDirectory(directory);
        using var stream = File.Open(PathFor(reportId), FileMode.CreateNew, FileAccess.Write, FileShare.None);
    }

    public async Task WriteAsync(ReportInput input)
    {
        await using var stream = File.Open(PathFor(input.ReportId), FileMode.Truncate, FileAccess.Write, FileShare.None);
        await JsonSerializer.SerializeAsync(stream, input);
    }

    public async Task<ReportInput> ReadAsync(string reportId)
    {
        await using var stream = File.OpenRead(PathFor(reportId));
        ReportInput? receipt;
        try { receipt = await JsonSerializer.DeserializeAsync<ReportInput>(stream); }
        catch (JsonException ex) { throw new ReceiptException("InvalidReceipt", ex); }

        if (receipt is null || receipt.ReportId != reportId || string.IsNullOrWhiteSpace(receipt.Bucket)
            || string.IsNullOrWhiteSpace(receipt.Key) || string.IsNullOrWhiteSpace(receipt.VersionId)
            || receipt.VersionId is "null" or "None" || receipt.RowCount < 0
            || !System.Text.RegularExpressions.Regex.IsMatch(receipt.Sha256 ?? "", @"\A[0-9a-f]{64}\z"))
            throw new InvalidOperationException("InvalidReceipt");
        return receipt;
    }

    private string PathFor(string reportId) => Path.Combine(directory, reportId + ".json");
}
