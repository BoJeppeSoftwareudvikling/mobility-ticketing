using Microsoft.EntityFrameworkCore;
using MobilityTicketing.Lecture07.Contracts;
using Xunit;

namespace MobilityTicketing.Lecture07.Tests;

[Trait("Category", "Acceptance")]
public class BookingTests : Fixture
{
    [Fact]
    public async Task Retry_returns_the_identical_receipt_with_a_submicrosecond_clock()
    {
        Clock.Now = Clock.Now.AddTicks(7);
        var first = await App.BookTripAsync("T201", "precise-clock");
        var retry = await App.BookTripAsync("T201", "precise-clock");
        Assert.Equal(first.Receipt, retry.Receipt);
    }
    [Fact]
    public async Task Booking_a_scheduled_trip_persists_a_receipt()
    {
        var result = await App.BookTripAsync("T102", "request-1");
        Assert.Equal(BookingStatus.Booked, result.Status);
        Assert.Equal(new BookingReceipt("request-1", "T102", Clock.Now.UtcDateTime), result.Receipt);
        await using var db = Db();
        var saved = await db.Bookings.SingleAsync();
        Assert.Equal("request-1", saved.RequestId);
        Assert.Equal("T102", saved.TripId);
    }
    [Fact]
    public async Task Cancelled_and_missing_trips_cannot_be_booked()
    {
        var before = await State();
        Assert.Equal(BookingStatus.Cancelled, (await App.BookTripAsync("T104", "cancelled")).Status);
        Assert.Equal(BookingStatus.NotFound, (await App.BookTripAsync("missing", "missing")).Status);
        Assert.Equal(before, await State());
    }
    [Fact]
    public async Task Retrying_a_request_returns_its_original_receipt_even_after_cancellation()
    {
        var first = await App.BookTripAsync("T102", "same");
        await App.CancelTripAsync("T102");
        Clock.Now = Clock.Now.AddMinutes(10);
        var retry = await App.BookTripAsync("T102", "same");
        Assert.Equal(BookingStatus.AlreadyBooked, retry.Status);
        Assert.Equal(first.Receipt, retry.Receipt);
        Assert.Equal(BookingStatus.Cancelled, (await App.BookTripAsync("T102", "new")).Status);
        await using var db = Db();
        Assert.Equal(1, await db.Bookings.CountAsync());
    }
    [Fact]
    public async Task A_request_ID_cannot_be_reused_for_another_trip()
    {
        await App.BookTripAsync("T102", "same");
        var before = await State();
        Assert.Equal(BookingStatus.RequestConflict, (await App.BookTripAsync("T201", "same")).Status);
        Assert.Equal(before, await State());
    }
    [Fact]
    public async Task An_old_snapshot_does_not_authorize_a_new_booking()
    {
        await App.PublishSnapshotAsync("before", TimeWindow.Morning);
        await App.CancelTripAsync("T102");
        var old = await App.ReadSnapshotAsync("before");
        Assert.Equal(2, old.Routes[0].ScheduledTripCount);
        Assert.Equal(BookingStatus.Cancelled, (await App.BookTripAsync("T102", "from-old-screen")).Status);
        await using var db = Db();
        Assert.Empty(await db.Bookings.ToListAsync());
    }
    [Fact]
    public async Task Concurrent_retries_create_only_one_booking()
    {
        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => App.BookTripAsync("T102", "retry")));
        Assert.Equal(1, results.Count(r => r.Status == BookingStatus.Booked));
        Assert.Equal(7, results.Count(r => r.Status == BookingStatus.AlreadyBooked));
        Assert.Single(results.Select(r => r.Receipt).Distinct());
        await using var db = Db();
        Assert.Equal(1, await db.Bookings.CountAsync());
    }
    [Fact]
    public async Task Booking_and_cancellation_share_the_authoritative_trip_boundary()
    {
        var book = App.BookTripAsync("T102", "race");
        var cancel = App.CancelTripAsync("T102");
        await Task.WhenAll(book, cancel);
        Assert.Contains((await book).Status, new[] { BookingStatus.Booked, BookingStatus.Cancelled });
        Assert.Equal(CancelResult.Cancelled, await cancel);
        Assert.Equal(BookingStatus.Cancelled, (await App.BookTripAsync("T102", "after-race")).Status);
        await using var db = Db();
        Assert.Equal((await book).Status == BookingStatus.Booked ? 1 : 0, await db.Bookings.CountAsync());
    }
    [Fact]
    public async Task Empty_request_ID_is_rejected_before_writing()
    {
        var before = await State();
        await Assert.ThrowsAsync<ArgumentException>(() => App.BookTripAsync("T102", " "));
        Assert.Equal(before, await State());
    }
}
