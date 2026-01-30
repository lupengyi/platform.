using IndustrialTestPlatform.Core;
using IndustrialTestPlatform.Supervisor;

var configPath = args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "config", "sample-config.json");
configPath = Path.GetFullPath(configPath);

Console.WriteLine($"Loading config from {configPath}");
var bundle = ConfigLoader.Load(configPath);

var supervisor = new PlatformSupervisor(bundle);
supervisor.LogReceived += (_, args) =>
{
    Console.WriteLine($"[{args.Entry.Timestamp:HH:mm:ss}] Slot {args.Entry.SlotId} {args.Entry.Event}: {args.Entry.Message}");
};

supervisor.SlotStatusChanged += (_, args) =>
{
    Console.WriteLine($"Slot {args.SlotId} status -> {args.Status} ({args.Message})");
};

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cts.Cancel();
    supervisor.Stop();
};

try
{
    var runId = await supervisor.StartAsync(cts.Token);
    Console.WriteLine($"Run {runId} complete.");
}
catch (OperationCanceledException)
{
    Console.WriteLine("Run canceled.");
}
catch (Exception ex)
{
    Console.WriteLine($"Run failed: {ex.Message}");
}
