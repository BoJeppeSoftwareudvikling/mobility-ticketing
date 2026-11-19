using System.Text;
using MobilityTicketing.Reports.Application;
using Xunit;

namespace MobilityTicketing.Reports.Tests;

public sealed class ReportWorkflowTests
{
    [Fact]
    public async Task StageHashesTheUploadedBytesAndCountsNewlinesRatherThanRecords()
    {
        var lab = new Lab();
        var bytes = Encoding.UTF8.GetBytes("abc");

        Assert.Equal("v1", await lab.Workflow.StageAsync("R-1", bytes));

        Assert.Same(bytes, lab.UploadedBytes);
        Assert.Equal(new ReportInput("R-1", "bucket", "key", "v1",
            "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", 0), lab.Receipt);
        Assert.Null(lab.Published);
    }

    [Theory]
    [InlineData("", 0)]
    [InlineData("a\nb\n", 2)]
    [InlineData("a\r\nb\r\n", 2)]
    [InlineData("a\nb", 1)]
    public async Task StageUsesTheLabNewlineConvention(string text, int expected)
    {
        var lab = new Lab();
        await lab.Workflow.StageAsync("R-1", Encoding.UTF8.GetBytes(text));
        Assert.Equal(expected, lab.Receipt!.RowCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t")]
    [InlineData("null")]
    [InlineData("None")]
    public async Task InvalidUploadVersionKeepsReservationAndCannotBeStagedAgain(string? version)
    {
        var lab = new Lab { UploadVersion = version! };
        await Assert.ThrowsAsync<InvalidOperationException>(() => lab.Workflow.StageAsync("R-1", []));
        Assert.Null(lab.Receipt);
        await Assert.ThrowsAsync<IOException>(() => lab.Workflow.StageAsync("R-1", []));
        Assert.Equal(1, lab.Uploads);
    }

    [Fact]
    public async Task FailedPutKeepsReservationAndBlocksAnotherUpload()
    {
        var lab = new Lab { UploadFailure = new ObjectStoreException("PUT failed", new IOException()) };
        await Assert.ThrowsAsync<ObjectStoreException>(() => lab.Workflow.StageAsync("R-1", []));
        await Assert.ThrowsAsync<IOException>(() => lab.Workflow.StageAsync("R-1", []));
        Assert.Equal(1, lab.Uploads);
        Assert.Null(lab.Receipt);
    }

    [Fact]
    public async Task ReceiptWriteFailureLeavesAnUnresolvedUploadAndBlocksAnotherPut()
    {
        var lab = new Lab { ReceiptWriteFailure = new IOException("Disk unavailable") };
        await Assert.ThrowsAsync<IOException>(() => lab.Workflow.StageAsync("R-1", []));
        await Assert.ThrowsAsync<IOException>(() => lab.Workflow.StageAsync("R-1", []));
        Assert.Equal(1, lab.Uploads);
        Assert.Null(lab.Receipt);
        Assert.Null(lab.Published);
    }

    [Fact]
    public async Task VerifyUsesCatalogueLocationAndVersionInsteadOfCurrentDestination()
    {
        var lab = new Lab();
        await lab.Workflow.StageAsync("R-1", Encoding.UTF8.GetBytes("abc"));
        lab.Published = lab.Receipt! with { Bucket = "old-bucket", Key = "old-key", VersionId = "old-version" };
        lab.Receipt = null;

        Assert.Equal(Verification.Matches, await lab.Workflow.VerifyAsync("R-1"));
        Assert.Equal((new ExportLocation("old-bucket", "old-key"), "old-version"), lab.LastRead);
    }

    [Fact]
    public async Task UnknownReportDoesNotReadStorage()
    {
        var lab = new Lab();
        Assert.Equal(Verification.UnknownReport, await lab.Workflow.VerifyAsync("missing"));
        Assert.Equal(0, lab.Reads);
    }

    [Fact]
    public async Task CatalogueFailureDoesNotReadStorage()
    {
        var lab = new Lab { FindFailure = new CatalogueException("Database unavailable", new IOException()) };
        Assert.Equal(Verification.CatalogueReadFailed, await lab.Workflow.VerifyAsync("R-1"));
        Assert.Equal(0, lab.Reads);
    }

    [Theory]
    [InlineData("checksum", Verification.Mismatch)]
    [InlineData("rows", Verification.Mismatch)]
    [InlineData("read", Verification.ReadFailed)]
    public async Task FailedVerificationPreventsPublication(string failure, Verification expected)
    {
        var lab = new Lab();
        await lab.Workflow.StageAsync("R-1", Encoding.UTF8.GetBytes("abc"));
        if (failure == "checksum") lab.Receipt = lab.Receipt! with { Sha256 = new string('0', 64) };
        if (failure == "rows") lab.Receipt = lab.Receipt! with { RowCount = 1 };
        if (failure == "read") lab.ReadFailure = new ObjectStoreException("GET failed", new IOException());

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => lab.Workflow.RecoverAsync("R-1", null));
        Assert.Contains(expected.ToString(), error.Message);
        Assert.Equal(0, lab.Saves);
        Assert.Null(lab.Published);

        lab.Published = lab.Receipt;
        Assert.Equal(expected, await lab.Workflow.VerifyAsync("R-1"));
    }

    [Fact]
    public async Task RecoveryAfterCatalogueFailureAndRepeatedRecoveryDoNotUploadAgain()
    {
        var lab = new Lab { SaveFailure = new CatalogueException("Commit failed", new IOException()) };
        await Assert.ThrowsAsync<CatalogueException>(() => lab.Workflow.PublishAsync("R-2", [], "R-1"));
        Assert.NotNull(lab.Receipt);
        Assert.Null(lab.Published);

        lab.SaveFailure = null;
        Assert.Equal("Saved R-2", await lab.Workflow.RecoverAsync("R-2", "R-1"));
        Assert.Equal("Saved R-2", await lab.Workflow.RecoverAsync("R-2", "R-1"));
        Assert.Equal("R-1", lab.Predecessor);
        Assert.Equal(lab.Receipt, lab.Published);
        Assert.Equal(1, lab.Uploads);
    }

    [Fact]
    public async Task PublishVerifiesBeforeSaving()
    {
        var lab = new Lab { ReadFailure = new ObjectStoreException("GET failed", new IOException()) };
        await Assert.ThrowsAsync<InvalidOperationException>(() => lab.Workflow.PublishAsync("R-1", [], null));
        Assert.NotNull(lab.Receipt);
        Assert.Equal(0, lab.Saves);
    }

    [Fact]
    public async Task MissingReceiptDoesNotReadStorageOrSaveCatalogue()
    {
        var lab = new Lab();
        await Assert.ThrowsAsync<IOException>(() => lab.Workflow.RecoverAsync("R-1", null));
        Assert.Equal(0, lab.Reads);
        Assert.Equal(0, lab.Saves);
    }

    private sealed class Lab : IReportObjectStore, IReportCatalogue, IUploadReceipts
    {
        public ReportWorkflow Workflow => new(this, this, this, new ExportLocation("bucket", "key"));
        public string UploadVersion { get; init; } = "v1";
        public byte[]? UploadedBytes { get; private set; }
        public ReportInput? Receipt { get; set; }
        public ReportInput? Published { get; set; }
        public string? Predecessor { get; private set; }
        public (ExportLocation Location, string Version)? LastRead { get; private set; }
        public int Uploads { get; private set; }
        public int Reads { get; private set; }
        public int Saves { get; private set; }
        public Exception? UploadFailure { get; init; }
        public Exception? ReceiptWriteFailure { get; init; }
        public Exception? FindFailure { get; init; }
        public Exception? ReadFailure { get; set; }
        public Exception? SaveFailure { get; set; }
        private bool _reserved;

        public void Reserve(string reportId)
        {
            if (_reserved) throw new IOException("Reservation already exists");
            _reserved = true;
        }

        public Task<string> UploadAsync(ExportLocation location, byte[] bytes)
        {
            Assert.True(_reserved);
            Uploads++;
            if (UploadFailure is not null) throw UploadFailure;
            UploadedBytes = bytes;
            return Task.FromResult(UploadVersion);
        }

        public Task WriteAsync(ReportInput input)
        {
            if (ReceiptWriteFailure is not null) throw ReceiptWriteFailure;
            Receipt = input;
            return Task.CompletedTask;
        }

        public Task<ReportInput> ReadAsync(string reportId) =>
            Task.FromResult(Receipt ?? throw new IOException("No completed receipt"));

        public Task<byte[]> ReadVersionAsync(ExportLocation location, string versionId)
        {
            Reads++;
            LastRead = (location, versionId);
            if (ReadFailure is not null) throw ReadFailure;
            return Task.FromResult(UploadedBytes!);
        }

        public Task<ReportInput?> FindAsync(string reportId)
        {
            if (FindFailure is not null) throw FindFailure;
            return Task.FromResult(Published);
        }

        public Task SaveAsync(ReportInput input, string? supersedes)
        {
            Saves++;
            if (SaveFailure is not null) throw SaveFailure;
            Published = input;
            Predecessor = supersedes;
            return Task.CompletedTask;
        }
    }
}
