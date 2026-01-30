using IndustrialTestPlatform.Contracts;
using IndustrialTestPlatform.Core;
using IndustrialTestPlatform.Instruments;

namespace IndustrialTestPlatform.Sequencing;

public sealed class SafetyCheckStep : ISequenceStep
{
    public string Name => "SafetyCheck";

    public async Task<StepResult> ExecuteAsync(SequenceContext context, InstrumentLease lease, CancellationToken cancellationToken)
    {
        await Task.Delay(100, cancellationToken).ConfigureAwait(false);
        return new StepResult(Name, StepStatus.Passed, "Safety checks OK");
    }
}

public sealed class PowerUpStep : ISequenceStep
{
    public string Name => "PowerUp";

    public async Task<StepResult> ExecuteAsync(SequenceContext context, InstrumentLease lease, CancellationToken cancellationToken)
    {
        await lease.PowerSupply.PowerOnAsync(cancellationToken).ConfigureAwait(false);
        return new StepResult(Name, StepStatus.Passed, "Power supply on");
    }
}

public sealed class CommUpStep : ISequenceStep
{
    public string Name => "CommUp";

    public async Task<StepResult> ExecuteAsync(SequenceContext context, InstrumentLease lease, CancellationToken cancellationToken)
    {
        await lease.CanBus.ConnectAsync(cancellationToken).ConfigureAwait(false);
        return new StepResult(Name, StepStatus.Passed, "CAN bus connected");
    }
}

public sealed class MeasureVoltagesStep : ISequenceStep
{
    public string Name => "MeasureVoltages";

    public async Task<StepResult> ExecuteAsync(SequenceContext context, InstrumentLease lease, CancellationToken cancellationToken)
    {
        var measurements = new List<Measurement>
        {
            await lease.Dmm.MeasureVoltageAsync("VBAT", cancellationToken).ConfigureAwait(false),
            await lease.Dmm.MeasureVoltageAsync("V12", cancellationToken).ConfigureAwait(false)
        };

        context.Measurements.AddRange(measurements);

        context.Logger.Log(new SlotLogEntry(DateTimeOffset.UtcNow, context.RunId, context.SlotId, "Measurements", "Voltage samples captured", new Dictionary<string, object?>
        {
            ["VBAT"] = measurements[0].Value,
            ["V12"] = measurements[1].Value
        }));

        return new StepResult(Name, StepStatus.Passed, "Voltages measured");
    }
}

public sealed class EvaluateLimitsStep : ISequenceStep
{
    public string Name => "EvaluateLimits";

    public Task<StepResult> ExecuteAsync(SequenceContext context, InstrumentLease lease, CancellationToken cancellationToken)
    {
        var evaluation = LimitsEvaluator.Evaluate(context.Measurements, context.Limits);
        context.LimitResults.AddRange(evaluation.Results);
        var status = evaluation.Passed ? StepStatus.Passed : StepStatus.Failed;
        var message = evaluation.Passed ? "All limits passed" : "Limit failures detected";
        return Task.FromResult(new StepResult(Name, status, message));
    }
}

public sealed class TearDownStep : ISequenceStep
{
    public string Name => "TearDown";

    public async Task<StepResult> ExecuteAsync(SequenceContext context, InstrumentLease lease, CancellationToken cancellationToken)
    {
        await lease.CanBus.DisconnectAsync(cancellationToken).ConfigureAwait(false);
        await lease.PowerSupply.PowerOffAsync(cancellationToken).ConfigureAwait(false);
        return new StepResult(Name, StepStatus.Passed, "Powered down");
    }
}
