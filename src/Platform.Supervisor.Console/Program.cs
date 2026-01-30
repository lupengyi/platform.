using Platform.Core;
using Platform.Libraries;
using Platform.Tester;

var logger = new ConsoleLogger();
var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, args) =>
{
    args.Cancel = true;
    cancellation.Cancel();
};

var basePath = AppContext.BaseDirectory;
var appConfigPath = Path.Combine(basePath, "config", "appsettings.json");
var appConfig = await ConfigLoader.LoadAppConfigAsync(appConfigPath, cancellation.Token);
var stationConfigPath = Path.Combine(basePath, "config", appConfig.StationConfigPath);
var limitsPath = Path.Combine(basePath, "config", appConfig.LimitsCsvPath);

var stationConfig = await ConfigLoader.LoadStationConfigAsync(stationConfigPath, cancellation.Token);
var configValidation = ConfigLoader.ValidateStationConfig(stationConfig);
if (!configValidation.Success)
{
    Console.WriteLine($"Invalid config: {configValidation.Error}");
    return;
}

var limits = await LimitCsvParser.LoadAsync(limitsPath, cancellation.Token);
var reportRoot = Path.Combine(basePath, "reports");
var station = new StationController(stationConfig, limits, logger, reportRoot);

Console.WriteLine("Starting run...");
var summary = await station.RunAsync(cancellation.Token);
Console.WriteLine($"Run complete. Passed {summary.Passed} Failed {summary.Failed} Stopped {summary.Stopped}");
