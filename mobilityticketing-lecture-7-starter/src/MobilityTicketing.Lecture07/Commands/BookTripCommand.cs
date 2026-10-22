using Microsoft.EntityFrameworkCore;
using MobilityTicketing.Lecture07.Contracts;
using MobilityTicketing.Lecture07.Data;

namespace MobilityTicketing.Lecture07.Commands;

public static class BookTripCommand
{
    public static Task<BookingResult> ExecuteAsync(
        string connection,
        TimeProvider clock,
        string tripId,
        string requestId,
        CancellationToken ct = default)
    {
        DatabaseSession.RequireText(tripId, nameof(tripId));
        DatabaseSession.RequireText(requestId, nameof(requestId));

        return DatabaseSession.WriteAsync(
            connection,
            $"booking:{requestId}",
            async db =>
            {
                var existing = await db.Bookings
                    .AsNoTracking()
                    .SingleOrDefaultAsync(booking => booking.RequestId == requestId, ct);

                if (existing is not null)
                {
                    if (existing.TripId != tripId)
                        return new BookingResult(BookingStatus.RequestConflict);

                    return new BookingResult(
                        BookingStatus.AlreadyBooked,
                        new BookingReceipt(existing.RequestId, existing.TripId, existing.BookedAtUtc));
                }

                var trip = await DatabaseSession.LockTripAsync(db, tripId, ct);
                if (trip is null) return new BookingResult(BookingStatus.NotFound);
                if (trip.Status == "Cancelled") return new BookingResult(BookingStatus.Cancelled);

                var bookedAtUtc = DatabaseTimestamp.Now(clock);
                db.Bookings.Add(new Booking
                {
                    RequestId = requestId,
                    TripId = tripId,
                    BookedAtUtc = bookedAtUtc
                });

                return new BookingResult(
                    BookingStatus.Booked,
                    new BookingReceipt(requestId, tripId, bookedAtUtc));
            },
            ct);
    }
}
