using Microsoft.EntityFrameworkCore;
using MobilityTicketing.Lecture07.Contracts;
using MobilityTicketing.Lecture07.Data;
using MobilityTicketing.Lecture07.Diagnostics;
using Xunit;

namespace MobilityTicketing.Lecture07.Tests;

[Trait("Category", "Acceptance")]
public class LiveBoardTests : Fixture
{
    [Fact]
    public async Task Morning_board_matches_the_four_literal_expected_rows() =>
        AssertRows(Baseline, await App.ReadOverviewAsync(TimeWindow.Morning));

    [Theory]
    [InlineData(6, 20, 7, 0, 1, 6, 20, 1, 6, 40, 0)]
    [InlineData(5, 45, 6, 0, 1, 5, 59, 0, 0, 0, 1)]
    [InlineData(7, 0, 8, 0, 1, 7, 0, 0, 0, 0, 0)]
    public async Task Other_windows_include_the_lower_bound_and_exclude_the_upper_bound(
        int fh, int fm, int th, int tm, int r1, int h1, int m1, int r2, int h2, int m2, int r3)
    {
        var rows = await App.ReadOverviewAsync(new(Utc(fh, fm), Utc(th, tm)));
        Assert.Equal(4, rows.Count);
        Assert.Equal(r1, rows[0].ScheduledTripCount);
        Assert.Equal(r1 == 0 ? null : Utc(h1, m1), rows[0].NextDepartureUtc);
        Assert.Equal(r2, rows[1].ScheduledTripCount);
        Assert.Equal(r2 == 0 ? null : Utc(h2, m2), rows[1].NextDepartureUtc);
        Assert.Equal(r3, rows[2].ScheduledTripCount);
        Assert.Equal(r3 == 0 ? null : Utc(5, 50), rows[2].NextDepartureUtc);
        Assert.Equal(Baseline[3], rows[3]);
    }
    [Fact]
    public async Task A_day_without_departures_still_returns_every_route()
    {
        var rows = await App.ReadOverviewAsync(new(Utc(6, 0).AddDays(1), Utc(7, 0).AddDays(1)));
        AssertRows(Baseline.Select(r => r with { ScheduledTripCount = 0, NextDepartureUtc = null }), rows);
    }
    [Fact]
    public async Task Live_board_reduces_roundtrips_and_reports_actual_reads()
    {
        var run = await RunLog.ExecuteAsync(Connection, "live");
        AssertRows(Baseline, run.Rows);
        Assert.InRange(run.ExecutedReadCommands, 1, 2);
    }
    [Fact]
    public async Task Reading_the_board_changes_no_tables()
    {
        var before = await State();
        await App.ReadOverviewAsync(TimeWindow.Morning);
        Assert.Equal(before, await State());
    }
    [Fact]
    public async Task Cancellation_changes_the_live_board_but_preserves_the_trip_and_other_routes()
    {
        Assert.Equal(CancelResult.Cancelled, await App.CancelTripAsync("T102"));
        var rows = await App.ReadOverviewAsync(TimeWindow.Morning);
        AssertRows([Baseline[0] with { ScheduledTripCount = 1, NextDepartureUtc = Utc(6, 20) }, ..Baseline.Skip(1)], rows);
        await using var db = Db();
        Assert.Equal(9, await db.Trips.CountAsync());
        Assert.Equal("Cancelled", (await db.Trips.SingleAsync(t => t.Id == "T102")).Status);
    }
    [Fact]
    public async Task Repeated_cancellation_and_missing_ID_do_not_change_anything_else()
    {
        await App.CancelTripAsync("T102");
        var before = await State();
        Assert.Equal(CancelResult.AlreadyCancelled, await App.CancelTripAsync("T102"));
        Assert.Equal(CancelResult.NotFound, await App.CancelTripAsync("missing"));
        Assert.Equal(before, await State());
    }
    [Fact]
    public async Task Rename_is_visible_on_the_next_live_read()
    {
        Assert.Equal(RenameResult.Renamed, await App.RenameRouteAsync("R1", "City Ring"));
        AssertRows([Baseline[0] with { RouteName = "City Ring" }, ..Baseline.Skip(1)], await App.ReadOverviewAsync(TimeWindow.Morning));
        Assert.Equal(RenameResult.NotFound, await App.RenameRouteAsync("missing", "Other"));
    }
    [Fact]
    public async Task Invalid_input_is_rejected_without_changing_data()
    {
        var before = await State();
        await Assert.ThrowsAsync<ArgumentException>(() => App.RenameRouteAsync("R1", " "));
        await Assert.ThrowsAsync<ArgumentException>(() => App.CancelTripAsync(""));
        await Assert.ThrowsAsync<ArgumentException>(() => App.ReadOverviewAsync(new(Utc(7, 0), Utc(6, 0))));
        Assert.Equal(before, await State());
    }

    [Fact]
    public async Task Large_fixture_keeps_every_route_and_a_bounded_number_of_reads()
    {
        await ScaleData.ExecuteAsync(Connection);
        await ScaleData.ExecuteAsync(Connection);
        await using var db = Db();
        Assert.Equal(200, await db.Routes.CountAsync());
        Assert.Equal(100000, await db.Trips.CountAsync());
        var baseline = await RunLog.ExecuteAsync(Connection, "baseline");
        var live = await RunLog.ExecuteAsync(Connection, "live");
        AssertRows(baseline.Rows, live.Rows);
        Assert.Equal(201, baseline.ExecutedReadCommands);
        Assert.Equal(200, baseline.MaterializedRoutes);
        Assert.Equal(100000, baseline.MaterializedTrips);
        Assert.InRange(live.ExecutedReadCommands, 1, 2);
    }
}
