using System.Collections.Concurrent;
using System.Text.Json;
using Platform.Automation;
using Platform.Contracts;
using Platform.Core;
using Platform.InstrumentManager;
using Platform.Libraries;
using Platform.TestSteps;

namespace Platform.Tester;

public sealed class StationController
{
    private readonly StationConfig _config;
    private readonly IReadOnlyDictionary<string, LimitDefinition> _limits;
    private readonly ILogger _logger;
    private readonly string _reportRoot;

    public StationController(StationConfig config, IReadOnlyList<LimitDefinition> limits, ILogger logger, string reportRoot)
    {
        _config = config;
        _limits = limits.ToDictionary(l => l.Name, StringComparer.OrdinalIgnoreCase);
        _logger = logger;
        _reportRoot = reportRoot;
    }

    public async Task<RunSummary> RunAsync(CancellationToken cancellationToken)
    {
        var runId = Guid.NewGuid();
        var runStart = DateTimeOffset.UtcNow;
        var tasks = new List<Task<SlotReport>>();
        var results = new ConcurrentBag<SlotReport>();

        for (var slot = 1; slot <= _config.SlotCount; slot++)
        {
            var slotId = slot;
            tasks.Add(Task.Run(async () =>
            {
                var report = await RunSlotAsync(runId, slotId, cancellationToken);
                results.Add(report);
                return report;
            }, cancellationToken));
        }

        await Task.WhenAll(tasks);
        var finished = DateTimeOffset.UtcNow;
        var summary = new RunSummary(runId, runStart, finished, _config.SlotCount,
            results.Count(r => r.FinalState == SlotState.Pass),
            results.Count(r => r.FinalState == SlotState.Fail),
            results.Count(r => r.FinalState == SlotState.Stopped));

        await ReportWriter.WriteSummaryAsync(_reportRoot, summary, results.OrderBy(r => r.SlotId).ToList(), cancellationToken);
        return summary;
    }

    private async Task<SlotReport> RunSlotAsync(Guid runId, int slotId, CancellationToken cancellationToken)
    {
        var correlationId = Guid.NewGuid();
        var logProgress = new Progress<LogEntry>(entry => _logger.Log(entry));
        var policyOptions = new InstrumentPolicyOptions(
            new RetryOptions(3, TimeSpan.FromMilliseconds(200), true),
            new CircuitBreakerOptions(3, TimeSpan.FromSeconds(2)));
        var instruments = new InstrumentManager.InstrumentManager(
            _config.Timeouts,
            policyOptions,
            _logger,
            correlationId,
            slotId,
            slotId * 11,
            HealthCheckOptions.Disabled);
        var context = new TestContext(runId, correlationId, slotId, $"SN{slotId:0000}", DateTimeOffset.UtcNow, instruments, logProgress, _limits);

        var steps = BuildSteps();
        var runner = new SequenceRunner(_logger);
        var sequenceResult = await runner.RunAsync(steps, context, FailurePolicy.StopOnFail, cancellationToken);
        var finalState = sequenceResult.Passed ? SlotState.Pass : SlotState.Fail;

        var report = new SlotReport(slotId, context.SerialNumber, finalState, sequenceResult.Steps, sequenceResult.StartedUtc, sequenceResult.FinishedUtc, runId);
        await ReportWriter.WriteSlotReportAsync(_reportRoot, report, cancellationToken);
        return report;
    }

    private IReadOnlyList<ITestStep> BuildSteps()
    {
        var stepFactories = StepCatalog.DiscoverBuiltInSteps()
            .Concat(StepCatalog.DiscoverPlugins(Path.Combine(AppContext.BaseDirectory, "plugins")))
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);

        var steps = new List<ITestStep>();
        foreach (var stepName in _config.StepPlan)
        {
            if (stepFactories.TryGetValue(stepName, out var factory))
            {
                steps.Add(factory());
            }
        }
        return steps;
    }
}

internal sealed class TestContext : ITestContext
{
    public TestContext(Guid runId, Guid correlationId, int slotId, string serialNumber, DateTimeOffset startUtc, IInstrumentServices instruments, IProgress<LogEntry> log, IReadOnlyDictionary<string, LimitDefinition> limits)
    {
        RunId = runId;
        CorrelationId = correlationId;
        SlotId = slotId;
        SerialNumber = serialNumber;
        StartUtc = startUtc;
        Instruments = instruments;
        Log = log;
        Limits = limits;
        State = new Dictionary<string, object>();
    }

    public Guid RunId { get; }
    public Guid CorrelationId { get; }
    public int SlotId { get; }
    public string SerialNumber { get; }
    public DateTimeOffset StartUtc { get; }
    public IInstrumentServices Instruments { get; }
    public IProgress<LogEntry> Log { get; }
    public IReadOnlyDictionary<string, LimitDefinition> Limits { get; }
    public IDictionary<string, object> State { get; }
}

public static class ReportWriter
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static async Task WriteSlotReportAsync(string root, SlotReport report, CancellationToken cancellationToken)
    {
        var runPath = GetRunPath(root, report.RunId, report.StartedUtc);
        Directory.CreateDirectory(runPath);
        var filePath = Path.Combine(runPath, $"slot_{report.SlotId:00}.json");
        await using var stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(stream, report, Options, cancellationToken);
    }

    public static async Task WriteSummaryAsync(string root, RunSummary summary, IReadOnlyList<SlotReport> reports, CancellationToken cancellationToken)
    {
        var runPath = GetRunPath(root, summary.RunId, summary.StartedUtc);
        Directory.CreateDirectory(runPath);
        var summaryPath = Path.Combine(runPath, "summary.json");
        await using (var stream = File.Create(summaryPath))
        {
            await JsonSerializer.SerializeAsync(stream, summary, Options, cancellationToken);
        }

        var csvPath = Path.Combine(runPath, "summary.csv");
        await using var writer = new StreamWriter(csvPath);
        await writer.WriteLineAsync("SlotId,SerialNumber,FinalState,StartUtc,FinishedUtc");
        foreach (var report in reports)
        {
            await writer.WriteLineAsync($"{report.SlotId},{report.SerialNumber},{report.FinalState},{report.StartedUtc:O},{report.FinishedUtc:O}");
        }
        await writer.FlushAsync();
    }

    private static string GetRunPath(string root, Guid runId, DateTimeOffset startUtc)
    {
        var date = startUtc.ToString("yyyyMMdd");
        var time = startUtc.ToString("HHmmss");
        return Path.Combine(root, "Run", date, $"{time}_{runId:N}");
    }
}
