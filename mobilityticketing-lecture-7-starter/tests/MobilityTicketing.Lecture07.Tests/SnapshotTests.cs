using MobilityTicketing.Lecture07.Contracts;
using MobilityTicketing.Lecture07.Diagnostics;
using Xunit;

namespace MobilityTicketing.Lecture07.Tests;

[Trait("Category", "Acceptance")]
public class SnapshotTests : Fixture
{
    [Fact]
    public async Task Published_snapshot_survives_a_new_application_instance()
    {
        Assert.Equal(PublishResult.Published, await App.PublishSnapshotAsync("dispatch", TimeWindow.Morning));
        var saved = await App.ReadSnapshotAsync("dispatch");
        Assert.Equal("dispatch", saved.Id);
        Assert.Equal(TimeWindow.Morning, saved.Window);
        Assert.Equal(Clock.Now.UtcDateTime, saved.PublishedAtUtc);
        AssertRows(Baseline, saved.Routes);
    }
    [Fact]
    public async Task Cancellation_and_rename_leave_the_published_values_unchanged()
    {
        await App.PublishSnapshotAsync("before", TimeWindow.Morning);
        var before = await App.ReadSnapshotAsync("before");
        await App.CancelTripAsync("T102");
        await App.RenameRouteAsync("R1", "City Ring");
        var after = await App.ReadSnapshotAsync("before");
        Assert.Equal(before.PublishedAtUtc, after.PublishedAtUtc);
        AssertRows(before.Routes, after.Routes);
        var live = await App.ReadOverviewAsync(TimeWindow.Morning);
        Assert.Equal("City Ring", live[0].RouteName);
        Assert.Equal(1, live[0].ScheduledTripCount);
    }
    [Fact]
    public async Task Republishing_an_ID_is_a_retry_and_does_not_replace_history()
    {
        await App.PublishSnapshotAsync("same", TimeWindow.Morning);
        await App.CancelTripAsync("T102");
        Clock.Now = Clock.Now.AddHours(1);
        var before = await State();
        Assert.Equal(PublishResult.AlreadyPublished, await App.PublishSnapshotAsync("same", TimeWindow.Morning));
        Assert.Equal(before, await State());
    }
    [Fact]
    public async Task Reusing_an_ID_with_another_window_is_an_explicit_conflict()
    {
        await App.PublishSnapshotAsync("same", TimeWindow.Morning);
        var before = await State();
        await Assert.ThrowsAsync<SnapshotConflictException>(() => App.PublishSnapshotAsync("same", new(Utc(7, 0), Utc(8, 0))));
        Assert.Equal(before, await State());
    }
    [Fact]
    public async Task Multiple_IDs_and_windows_do_not_overwrite_each_other()
    {
        await App.PublishSnapshotAsync("morning", TimeWindow.Morning);
        await App.PublishSnapshotAsync("later", new(Utc(7, 0), Utc(8, 0)));
        AssertRows(Baseline, (await App.ReadSnapshotAsync("morning")).Routes);
        var later = await App.ReadSnapshotAsync("later");
        Assert.Equal(new TimeWindow(Utc(7, 0), Utc(8, 0)), later.Window);
        Assert.Equal(1, later.Routes[0].ScheduledTripCount);
        Assert.Equal(Utc(7, 0), later.Routes[0].NextDepartureUtc);
        Assert.All(later.Routes.Skip(1), r => Assert.Equal(0, r.ScheduledTripCount));
    }
    [Fact]
    public async Task Missing_snapshot_is_explicit_and_a_read_never_publishes()
    {
        var before = await State();
        await Assert.ThrowsAsync<SnapshotNotFoundException>(() => App.ReadSnapshotAsync("missing"));
        Assert.Equal(before, await State());
    }
    [Fact]
    public async Task Stored_read_uses_one_query_without_loading_source_entities_or_writing()
    {
        await App.PublishSnapshotAsync("saved", TimeWindow.Morning);
        var before = await State();
        var run = await RunLog.ExecuteAsync(Connection, "snapshot", snapshotId: "saved");
        AssertRows(Baseline, run.Rows);
        Assert.Equal(1, run.ExecutedReadCommands);
        Assert.Equal(0, run.MaterializedRoutes);
        Assert.Equal(0, run.MaterializedTrips);
        Assert.Equal(before, await State());
    }
    [Fact]
    public async Task Failed_publication_leaves_no_partial_snapshot_and_preserves_previous_ones()
    {
        await App.PublishSnapshotAsync("existing", TimeWindow.Morning);
        var before = await State();
        var app = new StudentApplication(Connection, Clock, new ApplicationHooks
        {
            BeforePublishCommit = () => throw new InvalidOperationException("test publication failure")
        });
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => app.PublishSnapshotAsync("broken", TimeWindow.Morning));
        Assert.Contains("test publication failure", ex.Message);
        Assert.Equal(before, await State());
        await Assert.ThrowsAsync<SnapshotNotFoundException>(() => App.ReadSnapshotAsync("broken"));
    }
    [Fact]
    public async Task Concurrent_publication_retries_produce_one_stable_snapshot()
    {
        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => App.PublishSnapshotAsync("same", TimeWindow.Morning)));
        Assert.Equal(1, results.Count(r => r == PublishResult.Published));
        Assert.Equal(7, results.Count(r => r == PublishResult.AlreadyPublished));
        AssertRows(Baseline, (await App.ReadSnapshotAsync("same")).Routes);
    }
    [Fact]
    public async Task Invalid_publication_input_writes_nothing()
    {
        var before = await State();
        await Assert.ThrowsAsync<ArgumentException>(() => App.PublishSnapshotAsync("", TimeWindow.Morning));
        await Assert.ThrowsAsync<ArgumentException>(() => App.PublishSnapshotAsync("bad", new(Utc(7, 0), Utc(6, 0))));
        Assert.Equal(before, await State());
    }
}
