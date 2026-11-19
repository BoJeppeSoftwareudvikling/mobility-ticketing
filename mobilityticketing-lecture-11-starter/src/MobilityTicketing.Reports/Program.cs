using System.Text.RegularExpressions;
using MobilityTicketing.Reports.Application;
using MobilityTicketing.Reports.Infrastructure;

const string usage = "Usage: publish ID FILE [SUPERSEDES] | stage ID FILE | recover ID [SUPERSEDES] | verify ID";
if (args.Length == 0 || args is ["--help"])
{
    Console.WriteLine(usage);
    return 0;
}
if (args.Length < 2 || !IsReportId(args[1]))
{
    Console.Error.WriteLine(usage);
    return 2;
}

await using var services = new LocalReportServices();
var workflow = services.Workflow;

try
{
    switch (args)
    {
        case ["stage", var id, var file]:
            Console.WriteLine(await workflow.StageAsync(id, await File.ReadAllBytesAsync(file)));
            return 0;
        case ["publish", var id, var file]:
            Console.WriteLine(await workflow.PublishAsync(id, await File.ReadAllBytesAsync(file), null));
            return 0;
        case ["publish", var id, var file, var previous] when IsReportId(previous):
            Console.WriteLine(await workflow.PublishAsync(id, await File.ReadAllBytesAsync(file), previous));
            return 0;
        case ["recover", var id]:
            Console.WriteLine(await workflow.RecoverAsync(id, null));
            return 0;
        case ["recover", var id, var previous] when IsReportId(previous):
            Console.WriteLine(await workflow.RecoverAsync(id, previous));
            return 0;
        case ["verify", var id]:
            var result = await workflow.VerifyAsync(id);
            Console.WriteLine(result);
            return (int)result;
        default:
            Console.Error.WriteLine(usage);
            return 2;
    }
}
catch (Exception ex) when (ex is NotImplementedException or IOException or InvalidOperationException
                           or ObjectStoreException or CatalogueException or ReceiptException)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}

static bool IsReportId(string value) => Regex.IsMatch(value, @"\A[A-Za-z0-9][A-Za-z0-9_-]*\z");
