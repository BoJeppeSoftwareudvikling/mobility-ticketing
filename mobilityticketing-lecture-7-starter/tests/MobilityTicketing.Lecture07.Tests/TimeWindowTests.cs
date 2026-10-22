using MobilityTicketing.Lecture07.Contracts;
using MobilityTicketing.Lecture07.Diagnostics;
using Xunit;

namespace MobilityTicketing.Lecture07.Tests;

[Trait("Category", "Acceptance")]
public class TimeWindowTests : Fixture
{
    [Theory]
    [InlineData(7, 0)]
    [InlineData(0, 7)]
    [InlineData(7, 7)]
    public async Task Finer_precision_is_rejected_before_queries_or_writes(int fromTicks, int toTicks)
    {
        await App.PublishSnapshotAsync("existing", TimeWindow.Morning);
        var before = await State();
        var measurement = new RunMeasurement { Active = true };
        var app = new StudentApplication(Connection, Clock, measurement: measurement);
        var invalid = new TimeWindow(Utc(6, 0).AddTicks(fromTicks), Utc(7, 0).AddTicks(toTicks));
        await Assert.ThrowsAsync<ArgumentException>(() => app.ReadOverviewAsync(invalid));
        await Assert.ThrowsAsync<ArgumentException>(() => app.PublishSnapshotAsync("invalid", invalid));
        await Assert.ThrowsAsync<ArgumentException>(() => app.PublishSnapshotAsync("existing", invalid));
        Assert.Equal(0, measurement.ExecutedReadCommands);
        Assert.Equal(before, await State());
    }

    [Theory]
    [InlineData(10, 0, 1, 6, 20)]
    [InlineData(0, 10, 3, 6, 0)]
    [InlineData(10, 10, 2, 6, 20)]
    public async Task Whole_microsecond_boundaries_are_applied_exactly(
        int fromTicks, int toTicks, int count, int nextHour, int nextMinute)
    {
        var window = new TimeWindow(Utc(6, 0).AddTicks(fromTicks), Utc(7, 0).AddTicks(toTicks));
        var rows = await App.ReadOverviewAsync(window);
        Assert.Equal(count, rows[0].ScheduledTripCount);
        Assert.Equal(Utc(nextHour, nextMinute), rows[0].NextDepartureUtc);
    }

    [Fact]
    public async Task A_microsecond_window_survives_storage_and_identical_publication_retries()
    {
        var window = new TimeWindow(Utc(6, 0).AddTicks(10), Utc(7, 0).AddTicks(20));
        var rows = await App.ReadOverviewAsync(window);
        Assert.Equal(PublishResult.Published, await App.PublishSnapshotAsync("precise", window));
        var saved = await App.ReadSnapshotAsync("precise");
        await App.CancelTripAsync("T103");
        Clock.Now = Clock.Now.AddMinutes(1);
        Assert.Equal(PublishResult.AlreadyPublished, await App.PublishSnapshotAsync("precise", window));
        var retry = await App.ReadSnapshotAsync("precise");
        Assert.Equal(window, retry.Window);
        Assert.Equal(saved.PublishedAtUtc, retry.PublishedAtUtc);
        AssertRows(rows, retry.Routes);
    }
}
