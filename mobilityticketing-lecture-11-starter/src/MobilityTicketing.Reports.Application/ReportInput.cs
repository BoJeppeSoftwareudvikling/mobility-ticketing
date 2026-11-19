namespace MobilityTicketing.Reports.Application;

public sealed record ReportInput(
    string ReportId, string Bucket, string Key, string VersionId, string Sha256, int RowCount);

public enum Verification
{
    Matches = 0,
    Mismatch = 1,
    UnknownReport = 3,
    CatalogueReadFailed = 4,
    ReadFailed = 5
}
