using Platform.Contracts;
using Platform.Mes;

var options = CommandLineOptions.Parse(args);

if (!options.UploadMes)
{
    Console.WriteLine("Usage: --upload-mes [--run-folder <path>] [--serial <serial>] [--operator-id <id>] [--config-file <path>] [--limits-file <path>] [--plugin <path>] [--instrument <identity>]");
    return;
}

var runId = options.RunId ?? Guid.NewGuid().ToString("N");
var serial = options.Serial ?? $"DEMO-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
var builder = new RunManifestBuilder();
var manifest = builder.Build(
    runId,
    serial,
    options.ConfigFile,
    options.LimitsFile,
    options.PluginFiles,
    options.InstrumentIdentities,
    options.OperatorId);

var runFolder = options.RunFolder ?? Path.Combine(Environment.CurrentDirectory, $"run-{runId}");
await RunManifestWriter.WriteAsync(runFolder, manifest);

IMesClient mesClient = new OfflineMesClient();
var startedAt = manifest.CreatedAt;
var report = new RunReport(
    runId,
    serial,
    startedAt,
    DateTimeOffset.UtcNow,
    manifest.Snapshot);

await mesClient.UploadResultsAsync(report);

Console.WriteLine($"Uploaded run {runId} for serial {serial}. Manifest written to {runFolder}.");

internal sealed class CommandLineOptions
{
    public bool UploadMes { get; private set; }
    public string? RunId { get; private set; }
    public string? RunFolder { get; private set; }
    public string? Serial { get; private set; }
    public string? OperatorId { get; private set; }
    public string? ConfigFile { get; private set; }
    public string? LimitsFile { get; private set; }
    public List<string> PluginFiles { get; } = new();
    public List<string> InstrumentIdentities { get; } = new();

    public static CommandLineOptions Parse(string[] args)
    {
        var options = new CommandLineOptions();
        for (var index = 0; index < args.Length; index++)
        {
            var arg = args[index];
            switch (arg)
            {
                case "--upload-mes":
                    options.UploadMes = true;
                    break;
                case "--run-id":
                    options.RunId = ReadValue(args, ref index);
                    break;
                case "--run-folder":
                    options.RunFolder = ReadValue(args, ref index);
                    break;
                case "--serial":
                    options.Serial = ReadValue(args, ref index);
                    break;
                case "--operator-id":
                    options.OperatorId = ReadValue(args, ref index);
                    break;
                case "--config-file":
                    options.ConfigFile = ReadValue(args, ref index);
                    break;
                case "--limits-file":
                    options.LimitsFile = ReadValue(args, ref index);
                    break;
                case "--plugin":
                    options.PluginFiles.Add(ReadValue(args, ref index));
                    break;
                case "--instrument":
                    options.InstrumentIdentities.Add(ReadValue(args, ref index));
                    break;
                default:
                    throw new ArgumentException($"Unknown argument: {arg}");
            }
        }

        return options;
    }

    private static string ReadValue(string[] args, ref int index)
    {
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException($"Missing value for {args[index]}.");
        }

        index++;
        return args[index];
    }
}
