using Microsoft.EntityFrameworkCore;
using MobilityTicketing.Lecture07.Contracts;
using MobilityTicketing.Lecture07.Data;

namespace MobilityTicketing.Lecture07.Commands;

public static class RenameRouteCommand
{
    public static Task<RenameResult> ExecuteAsync(
        string connection,
        string routeId,
        string name,
        CancellationToken ct = default)
    {
        DatabaseSession.RequireText(routeId, nameof(routeId));
        DatabaseSession.RequireText(name, nameof(name));
        var trimmedName = name.Trim();

        return DatabaseSession.WriteAsync(
            connection,
            $"route:{routeId}",
            async db =>
            {
                var route = await db.Routes.SingleOrDefaultAsync(route => route.Id == routeId, ct);
                if (route is null) return RenameResult.NotFound;

                route.Name = trimmedName;
                return RenameResult.Renamed;
            },
            ct);
    }
}
