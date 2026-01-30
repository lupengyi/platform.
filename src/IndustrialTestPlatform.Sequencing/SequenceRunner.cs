using IndustrialTestPlatform.Contracts;
using IndustrialTestPlatform.Core;
using IndustrialTestPlatform.Instruments;
using System.Linq;

namespace IndustrialTestPlatform.Sequencing;

public sealed class SequenceRunner
{
    private readonly InstrumentManager _instrumentManager;
    private readonly IReadOnlyList<ISequenceStep> _steps;

    public SequenceRunner(InstrumentManager instrumentManager)
    {
        _instrumentManager = instrumentManager;
        _steps = new ISequenceStep[]
        {
            new SafetyCheckStep(),
            new PowerUpStep(),
            new CommUpStep(),
            new MeasureVoltagesStep(),
            new EvaluateLimitsStep(),
            new TearDownStep()
        };
    }

    public async Task<SlotRunResult> RunAsync(int slotId, string runId, IStructuredLogger logger, IReadOnlyDictionary<string, LimitDefinition> limits, CancellationToken cancellationToken)
    {
        var start = DateTimeOffset.UtcNow;
        var context = new SequenceContext(slotId, runId, logger, limits);
        var finalStatus = SlotStatus.Running;

        await using var lease = await _instrumentManager.AcquireAsync(slotId, cancellationToken).ConfigureAwait(false);

        foreach (var step in _steps)
        {
            cancellationToken.ThrowIfCancellationRequested();
            logger.Log(new SlotLogEntry(DateTimeOffset.UtcNow, runId, slotId, "StepStart", step.Name, new Dictionary<string, object?>()));
            var result = await step.ExecuteAsync(context, lease, cancellationToken).ConfigureAwait(false);
            logger.Log(new SlotLogEntry(DateTimeOffset.UtcNow, runId, slotId, "StepComplete", result.StepName, new Dictionary<string, object?>
            {
                ["Status"] = result.Status.ToString(),
                ["Message"] = result.Message
            }));

            if (result.Status == StepStatus.Failed)
            {
                finalStatus = SlotStatus.Failed;
                break;
            }
        }

        if (finalStatus == SlotStatus.Running)
        {
            finalStatus = context.LimitResults.Any(r => !r.Passed) ? SlotStatus.Failed : SlotStatus.Passed;
        }

        var end = DateTimeOffset.UtcNow;
        return new SlotRunResult(slotId, runId, finalStatus, context.Measurements, context.LimitResults, start, end);
    }
}
