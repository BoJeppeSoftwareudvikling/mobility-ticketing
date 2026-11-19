using Amazon.S3;
using MobilityTicketing.Reports.Application;
using Npgsql;

namespace MobilityTicketing.Reports.Infrastructure;

public sealed class LocalReportServices : IAsyncDisposable
{
    private readonly IAmazonS3 _s3 = new AmazonS3Client("mobilityadmin", "mobility-lab-only-change-me",
        new AmazonS3Config
        {
            ServiceURL = "http://127.0.0.1:19011", ForcePathStyle = true,
            AuthenticationRegion = "us-east-1", MaxErrorRetry = 0
        });

    private readonly NpgsqlDataSource _db = NpgsqlDataSource.Create(
        "Host=127.0.0.1;Port=25411;Database=mobility_l11;Username=mobility;Password=mobility-lab-only-change-me");

    public ReportWorkflow Workflow => new(
        new S3ReportObjectStore(_s3), new ReportCatalogue(_db), new UploadReceipts("receipts"),
        new ExportLocation("mobility-archive-lab", "validation-exports/2026-11-20/validations.jsonl"));

    public async ValueTask DisposeAsync()
    {
        _s3.Dispose();
        await _db.DisposeAsync();
    }
}
