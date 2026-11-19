namespace MobilityTicketing.Reports.Application;

public sealed record ExportLocation(string Bucket, string Key);

public interface IReportObjectStore
{
    Task<string> UploadAsync(ExportLocation location, byte[] bytes);
    Task<byte[]> ReadVersionAsync(ExportLocation location, string versionId);
}

public interface IReportCatalogue
{
    Task<ReportInput?> FindAsync(string reportId);
    Task SaveAsync(ReportInput input, string? supersedes);
}

public interface IUploadReceipts
{
    void Reserve(string reportId);
    Task WriteAsync(ReportInput input);
    Task<ReportInput> ReadAsync(string reportId);
}

public sealed class ObjectStoreException(string message, Exception inner) : Exception(message, inner);
public sealed class CatalogueException(string message, Exception inner) : Exception(message, inner);
public sealed class ReceiptException(string message, Exception inner) : Exception(message, inner);
