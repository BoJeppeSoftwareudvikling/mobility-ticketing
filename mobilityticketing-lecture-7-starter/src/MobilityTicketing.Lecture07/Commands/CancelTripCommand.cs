using MobilityTicketing.Lecture07.Contracts;
using MobilityTicketing.Lecture07.Data;

namespace MobilityTicketing.Lecture07.Commands;

public static class CancelTripCommand
{
    public static Task<CancelResult> ExecuteAsync(
        string connection,
        string tripId,
        CancellationToken ct = default)
    {
        DatabaseSession.RequireText(tripId, nameof(tripId));

        return DatabaseSession.WriteAsync(
            connection,
            $"trip:{tripId}",
            async db =>
            {
                var trip = await DatabaseSession.LockTripAsync(db, tripId, ct);
                if (trip is null) return CancelResult.NotFound;
                if (trip.Status == "Cancelled") return CancelResult.AlreadyCancelled;

                trip.Status = "Cancelled";
                return CancelResult.Cancelled;
            },
            ct);
    }
}
