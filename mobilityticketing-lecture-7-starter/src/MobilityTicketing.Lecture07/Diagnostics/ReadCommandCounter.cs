using System.Data.Common;
using System.Globalization;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace MobilityTicketing.Lecture07.Diagnostics;

public record SqlCommandLog(string Sql, Dictionary<string, string?> Parameters);

public class RunMeasurement
{
    public bool Active { get; set; }
    public int ExecutedReadCommands { get; set; }
    public int MaterializedRoutes { get; set; }
    public int MaterializedTrips { get; set; }
    public List<SqlCommandLog> Commands { get; } = [];
}

public class ReadCommandCounter(RunMeasurement measurement) : DbCommandInterceptor
{
    private void Count(DbCommand command)
    {
        if (!measurement.Active)
            return;

        var parameters = command.Parameters.Cast<DbParameter>().ToDictionary(
            parameter => parameter.ParameterName,
            parameter => parameter.Value is DateTime timestamp
                ? timestamp.ToString("O")
                : Convert.ToString(parameter.Value, CultureInfo.InvariantCulture));

        measurement.ExecutedReadCommands++;
        measurement.Commands.Add(new SqlCommandLog(command.CommandText, parameters));
    }

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
    {
        Count(command);
        return result;
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        Count(command);
        return ValueTask.FromResult(result);
    }
}
