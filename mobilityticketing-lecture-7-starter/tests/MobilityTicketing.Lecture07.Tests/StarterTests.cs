using Microsoft.EntityFrameworkCore;
using Npgsql;
using MobilityTicketing.Lecture07.Contracts;
using MobilityTicketing.Lecture07.Data;
using MobilityTicketing.Lecture07.Diagnostics;
using Xunit;

namespace MobilityTicketing.Lecture07.Tests;

[Trait("Category", "Starter")]
public class StarterTests : Fixture
{
    [Fact]
    public async Task Fixture_contains_four_routes_nine_trips_and_no_bookings()
    {
        await using var db = Db();
        Assert.Equal(4, await db.Routes.CountAsync());
        Assert.Equal(9, await db.Trips.CountAsync());
        Assert.Empty(await db.Bookings.ToListAsync());
        Assert.Equal("mobility_l07_test", db.Database.GetDbConnection().Database);
    }
    [Fact]
    public async Task Baseline_exposes_five_reads_and_entity_loading()
    {
        var run = await RunLog.ExecuteAsync(Connection, "baseline");
        AssertRows(Baseline, run.Rows);
        Assert.Equal(5, run.ExecutedReadCommands);
        Assert.Equal(4, run.MaterializedRoutes);
        Assert.Equal(9, run.MaterializedTrips);
    }
    [Fact]
    public async Task Test_reset_refuses_the_application_database_and_unrelated_databases()
    {
        var app = new NpgsqlConnectionStringBuilder(Connection) { Database = "mobility_l07" }.ConnectionString;
        var unrelated = new NpgsqlConnectionStringBuilder(Connection) { Database = "postgres" }.ConnectionString;
        await Assert.ThrowsAsync<InvalidOperationException>(() => FixtureReset.ExecuteAsync(app, true));
        await Assert.ThrowsAsync<InvalidOperationException>(() => FixtureReset.ExecuteAsync(unrelated));
    }
    [Fact]
    public async Task Reset_preserves_tables_outside_lab07()
    {
        await Sql("create table public.lecture07_reset_sentinel(id integer); insert into public.lecture07_reset_sentinel values (1)");
        try
        {
            await FixtureReset.ExecuteAsync(Connection, true);
            await Sql("do $$ begin if not exists(select from public.lecture07_reset_sentinel where id=1) then raise exception 'sentinel lost'; end if; end $$");
        }
        finally { await Sql("drop table public.lecture07_reset_sentinel"); }
    }
    [Fact]
    public void Windows_accept_other_UTC_intervals_and_reject_invalid_bounds()
    {
        new TimeWindow(Utc(7, 0), Utc(8, 0)).Validate();
        Assert.Throws<ArgumentException>(() => new TimeWindow(Utc(7, 0), Utc(6, 0)).Validate());
        Assert.Throws<ArgumentException>(() => new TimeWindow(DateTime.SpecifyKind(Utc(6, 0), DateTimeKind.Local), Utc(7, 0)).Validate());
        Assert.Throws<ArgumentException>(() => new TimeWindow(Utc(6, 0), Utc(6, 0).AddDays(2)).Validate());
    }
    [Theory]
    [InlineData(7, 0)]
    [InlineData(0, 7)]
    [InlineData(7, 7)]
    public void Windows_reject_submicrosecond_endpoints(int fromTicks, int toTicks)
    {
        var window = new TimeWindow(Utc(6, 0).AddTicks(fromTicks), Utc(7, 0).AddTicks(toTicks));
        var error = Assert.Throws<ArgumentException>(window.Validate);
        Assert.Contains("whole microseconds", error.Message);
        new TimeWindow(Utc(6, 0).AddTicks(10), Utc(7, 0).AddTicks(20)).Validate();
    }
    [Fact]
    public async Task Measurements_do_not_accumulate_between_runs()
    {
        var a = await RunLog.ExecuteAsync(Connection, "baseline");
        var b = await RunLog.ExecuteAsync(Connection, "baseline");
        Assert.Equal(5, a.ExecutedReadCommands);
        Assert.Equal(5, b.ExecutedReadCommands);
        Assert.Equal(9, b.MaterializedTrips);
    }
}
