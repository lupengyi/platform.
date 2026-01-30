using IndustrialTestPlatform.Contracts;
using IndustrialTestPlatform.Core;
using IndustrialTestPlatform.Instruments;

namespace IndustrialTestPlatform.Sequencing;

public sealed record StepResult(string StepName, StepStatus Status, string Message);

public sealed record SlotRunResult(
    int SlotId,
    string RunId,
    SlotStatus FinalStatus,
    IReadOnlyList<Measurement> Measurements,
    IReadOnlyList<LimitResult> LimitResults,
    DateTimeOffset StartedAt,
    DateTimeOffset FinishedAt);

public sealed class SequenceContext
{
    public SequenceContext(int slotId, string runId, IStructuredLogger logger, IReadOnlyDictionary<string, LimitDefinition> limits)
    {
        SlotId = slotId;
        RunId = runId;
        Logger = logger;
        Limits = limits;
        Measurements = new List<Measurement>();
        LimitResults = new List<LimitResult>();
    }

    public int SlotId { get; }
    public string RunId { get; }
    public IStructuredLogger Logger { get; }
    public IReadOnlyDictionary<string, LimitDefinition> Limits { get; }
    public List<Measurement> Measurements { get; }
    public List<LimitResult> LimitResults { get; }
}

public interface ISequenceStep
{
    string Name { get; }
    Task<StepResult> ExecuteAsync(SequenceContext context, InstrumentLease lease, CancellationToken cancellationToken);
}
