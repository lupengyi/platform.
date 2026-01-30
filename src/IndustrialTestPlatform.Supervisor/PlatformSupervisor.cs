using IndustrialTestPlatform.Contracts;
using IndustrialTestPlatform.Core;
using IndustrialTestPlatform.Instruments;
using IndustrialTestPlatform.Sequencing;

namespace IndustrialTestPlatform.Supervisor;

public sealed class SlotStatusChangedEventArgs : EventArgs
{
    public SlotStatusChangedEventArgs(int slotId, SlotStatus status, string message)
    {
        SlotId = slotId;
        Status = status;
        Message = message;
    }

    public int SlotId { get; }
    public SlotStatus Status { get; }
    public string Message { get; }
}

public sealed class LogEventArgs : EventArgs
{
    public LogEventArgs(SlotLogEntry entry)
    {
        Entry = entry;
    }

    public SlotLogEntry Entry { get; }
}

public sealed class PlatformSupervisor
{
    private readonly ConfigBundle _bundle;
    private readonly InstrumentManager _instrumentManager;
    private readonly SequenceRunner _sequenceRunner;
    private CancellationTokenSource? _cts;

    public PlatformSupervisor(ConfigBundle bundle)
    {
        _bundle = bundle;
        _instrumentManager = new InstrumentManager(bundle.Config.Slots.Select(slot => slot.SlotId));
        _sequenceRunner = new SequenceRunner(_instrumentManager);
    }

    public event EventHandler<SlotStatusChangedEventArgs>? SlotStatusChanged;
    public event EventHandler<LogEventArgs>? LogReceived;

    public bool IsRunning => _cts is not null;

    public async Task<string> StartAsync(CancellationToken cancellationToken = default)
    {
        if (_cts is not null)
        {
            throw new InvalidOperationException("Run already active.");
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var runId = Guid.NewGuid().ToString("N");
        var start = DateTimeOffset.UtcNow;
        var runFolder = Path.Combine(_bundle.Config.ReportsRoot, start.ToString("yyyyMMdd"), start.ToString("HHmmss") + "_" + runId);

        var slotTasks = _bundle.Config.Slots.Select(slot => RunSlotAsync(slot, runId, runFolder, _cts.Token)).ToArray();
        var results = await Task.WhenAll(slotTasks).ConfigureAwait(false);

        var summary = new SummaryReport(runId, start, DateTimeOffset.UtcNow, results.Select(result => new SlotSummary(result.SlotId, result.FinalStatus, result.FinalStatus == SlotStatus.Passed)).ToList());
        ReportWriter.WriteSummaryCsv(Path.Combine(runFolder, "summary.csv"), summary);

        _cts.Dispose();
        _cts = null;
        return runId;
    }

    public void Stop()
    {
        _cts?.Cancel();
    }

    private async Task<SlotRunResult> RunSlotAsync(SlotConfig slot, string runId, string runFolder, CancellationToken cancellationToken)
    {
        var inMemoryLogger = new InMemoryStructuredLogger();
        var logger = new CompositeStructuredLogger(inMemoryLogger, new RelayStructuredLogger(entry => LogReceived?.Invoke(this, new LogEventArgs(entry))));

        RaiseSlotStatus(slot.SlotId, SlotStatus.Running, "Run started");
        try
        {
            var result = await _sequenceRunner.RunAsync(slot.SlotId, runId, logger, _bundle.Limits, cancellationToken).ConfigureAwait(false);
            RaiseSlotStatus(slot.SlotId, result.FinalStatus, "Run completed");

            var report = new SlotReport(
                result.SlotId,
                result.RunId,
                result.StartedAt,
                result.FinishedAt,
                result.FinalStatus,
                result.Measurements,
                result.LimitResults,
                inMemoryLogger.Entries);

            var reportPath = Path.Combine(runFolder, $"slot_{slot.SlotId}.json");
            ReportWriter.WriteSlotReport(reportPath, report);

            return result;
        }
        catch (OperationCanceledException)
        {
            RaiseSlotStatus(slot.SlotId, SlotStatus.Stopped, "Run stopped");
            return new SlotRunResult(slot.SlotId, runId, SlotStatus.Stopped, Array.Empty<Measurement>(), Array.Empty<LimitResult>(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        }
        catch (Exception ex)
        {
            RaiseSlotStatus(slot.SlotId, SlotStatus.Error, ex.Message);
            return new SlotRunResult(slot.SlotId, runId, SlotStatus.Error, Array.Empty<Measurement>(), Array.Empty<LimitResult>(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        }
    }

    private void RaiseSlotStatus(int slotId, SlotStatus status, string message)
    {
        SlotStatusChanged?.Invoke(this, new SlotStatusChangedEventArgs(slotId, status, message));
    }

    private sealed class RelayStructuredLogger : IStructuredLogger
    {
        private readonly Action<SlotLogEntry> _onLog;

        public RelayStructuredLogger(Action<SlotLogEntry> onLog)
        {
            _onLog = onLog;
        }

        public void Log(SlotLogEntry entry)
        {
            _onLog(entry);
        }
    }
}
