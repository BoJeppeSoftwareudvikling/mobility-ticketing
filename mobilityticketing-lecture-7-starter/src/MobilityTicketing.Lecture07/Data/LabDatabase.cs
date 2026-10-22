using Npgsql;

namespace MobilityTicketing.Lecture07.Data;

public static class LabDatabase
{
    public static string AppConnection => Environment.GetEnvironmentVariable("LAB_CONNECTION") ?? "Host=localhost;Database=mobility_l07;Username=mobility;Password=mobility";
    public static string TestConnection => Environment.GetEnvironmentVariable("LAB_TEST_CONNECTION") ?? "Host=localhost;Database=mobility_l07_test;Username=mobility;Password=mobility";
    public static void Validate(string connection, bool testOnly = false)
    {
        var database = new NpgsqlConnectionStringBuilder(connection).Database;
        if (database != "mobility_l07_test" && (testOnly || database != "mobility_l07"))
            throw new InvalidOperationException("Refusing to modify a database outside this lab" + (testOnly ? "; tests require mobility_l07_test." : "."));
    }
    public static string ReadSql(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "database", "postgres", "lecture07", name));
}
