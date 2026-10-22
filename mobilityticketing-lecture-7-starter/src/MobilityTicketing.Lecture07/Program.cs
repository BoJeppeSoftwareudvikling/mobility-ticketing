using System.Globalization;
using System.Text.Json;
using MobilityTicketing.Lecture07;
using MobilityTicketing.Lecture07.Contracts;
using MobilityTicketing.Lecture07.Data;
using MobilityTicketing.Lecture07.Diagnostics;

try
{
    var connection = LabDatabase.AppConnection;
    var app = new StudentApplication(connection);
    switch (args)
    {
        case ["reset"]:
            await FixtureReset.ExecuteAsync(connection);
            Console.WriteLine("Restored lab07: four routes, nine trips, no bookings or snapshots.");
            break;
        case ["baseline"]:
            await RunLog.SaveAsync(await RunLog.ExecuteAsync(connection, "baseline"));
            break;
        case ["scale"]:
            await ScaleData.ExecuteAsync(connection);
            Console.WriteLine("Loaded 200 routes and 100,000 trips after a fresh reset.");
            break;
        case ["overview"]:
            await RunLog.SaveAsync(await RunLog.ExecuteAsync(connection, "live"));
            break;
        case ["overview", var from, var to]:
            await RunLog.SaveAsync(await RunLog.ExecuteAsync(connection, "live", Window(from, to)));
            break;
        case ["cancel", var id]:
            var cancelled = await app.CancelTripAsync(id);
            Console.WriteLine(cancelled);
            return cancelled == CancelResult.NotFound ? 2 : 0;
        case ["rename", var id, var name]:
            var renamed = await app.RenameRouteAsync(id, name);
            Console.WriteLine(renamed);
            return renamed == RenameResult.NotFound ? 2 : 0;
        case ["book", var id, var request]:
            var booked = await app.BookTripAsync(id, request);
            Console.WriteLine(JsonSerializer.Serialize(booked, new JsonSerializerOptions
                { WriteIndented = true, Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } }));
            return booked.Status is BookingStatus.Booked or BookingStatus.AlreadyBooked ? 0 : 2;
        case ["publish", var id]:
            Console.WriteLine(await app.PublishSnapshotAsync(id, TimeWindow.Morning));
            break;
        case ["publish", var id, var from, var to]:
            Console.WriteLine(await app.PublishSnapshotAsync(id, Window(from, to)));
            break;
        case ["snapshot", var id]:
            await RunLog.SaveAsync(await RunLog.ExecuteAsync(connection, "snapshot", snapshotId: id));
            break;
        default:
            Console.Error.WriteLine("Usage: reset | scale | baseline | overview [FROM TO] | cancel TRIP | rename ROUTE NAME | book TRIP REQUEST | publish ID [FROM TO] | snapshot ID");
            return 2;
    }
    return 0;
}
catch (Exception e) when (e is not OutOfMemoryException)
{
    Console.Error.WriteLine(e.Message);
    return 1;
}

static TimeWindow Window(string from, string to)
{
    static DateTime Utc(string s)
    {
        if (!s.EndsWith('Z') && !System.Text.RegularExpressions.Regex.IsMatch(s, @"[+-]\d\d:\d\d$"))
            throw new ArgumentException("Include Z or a UTC offset in each timestamp.");
        return DateTimeOffset.Parse(s, CultureInfo.InvariantCulture).UtcDateTime;
    }
    var window = new TimeWindow(Utc(from), Utc(to));
    window.Validate();
    return window;
}
