using Platform.Contracts;
using Platform.Core;
using Platform.Libraries;

namespace Platform.TestSteps;

public abstract class StepBase : ITestStep
{
    public abstract string Name { get; }

    public async Task<StepResult> ExecuteAsync(ITestContext context, CancellationToken cancellationToken)
    {
        var start = DateTimeOffset.UtcNow;
        try
        {
            var result = await ExecuteCoreAsync(context, cancellationToken);
            return result with { Duration = DateTimeOffset.UtcNow - start };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            context.Log.Report(new LogEntry(DateTimeOffset.UtcNow, context.CorrelationId, context.SlotId, LogLevel.Error, ex.Message));
            return new StepResult(Name, StepOutcome.Error, DateTimeOffset.UtcNow - start, Array.Empty<Measurement>(), Array.Empty<LimitResult>(), ex.Message);
        }
    }

    protected abstract Task<StepResult> ExecuteCoreAsync(ITestContext context, CancellationToken cancellationToken);
}

public sealed class SafetyStep : StepBase
{
    public override string Name => "Safety";

    protected override Task<StepResult> ExecuteCoreAsync(ITestContext context, CancellationToken cancellationToken)
    {
        context.Log.Report(new LogEntry(DateTimeOffset.UtcNow, context.CorrelationId, context.SlotId, LogLevel.Info, "Safety checks passed."));
        return Task.FromResult(new StepResult(Name, StepOutcome.Pass, TimeSpan.Zero, Array.Empty<Measurement>(), Array.Empty<LimitResult>(), null));
    }
}

public sealed class PowerUpStep : StepBase
{
    public override string Name => "PowerUp";

    protected override async Task<StepResult> ExecuteCoreAsync(ITestContext context, CancellationToken cancellationToken)
    {
        await context.Instruments.Psu.SetOutputAsync(true, 3.3, 1.0, cancellationToken);
        return new StepResult(Name, StepOutcome.Pass, TimeSpan.Zero, Array.Empty<Measurement>(), Array.Empty<LimitResult>(), null);
    }
}

public sealed class CommUpStep : StepBase
{
    public override string Name => "CommUp";

    protected override async Task<StepResult> ExecuteCoreAsync(ITestContext context, CancellationToken cancellationToken)
    {
        var response = await context.Instruments.Can.SendAsync("PING", cancellationToken);
        var outcome = response.StartsWith("ACK", StringComparison.OrdinalIgnoreCase) ? StepOutcome.Pass : StepOutcome.Fail;
        return new StepResult(Name, outcome, TimeSpan.Zero, Array.Empty<Measurement>(), Array.Empty<LimitResult>(), outcome == StepOutcome.Pass ? null : "CAN response invalid");
    }
}

public sealed class MeasureStep : StepBase
{
    public override string Name => "Measure";

    protected override async Task<StepResult> ExecuteCoreAsync(ITestContext context, CancellationToken cancellationToken)
    {
        var value = await context.Instruments.Dmm.MeasureVoltageAsync(cancellationToken);
        var measurement = new Measurement("Voltage", value, "V", DateTimeOffset.UtcNow);
        context.State["LastMeasurement"] = measurement;
        return new StepResult(Name, StepOutcome.Pass, TimeSpan.Zero, new[] { measurement }, Array.Empty<LimitResult>(), null);
    }
}

public sealed class EvaluateStep : StepBase
{
    public override string Name => "Evaluate";

    protected override Task<StepResult> ExecuteCoreAsync(ITestContext context, CancellationToken cancellationToken)
    {
        if (!context.State.TryGetValue("LastMeasurement", out var measurementObj) || measurementObj is not Measurement measurement)
        {
            return Task.FromResult(new StepResult(Name, StepOutcome.Fail, TimeSpan.Zero, Array.Empty<Measurement>(), Array.Empty<LimitResult>(), "No measurement available"));
        }

        if (!context.Limits.TryGetValue(measurement.Name, out var limit))
        {
            return Task.FromResult(new StepResult(Name, StepOutcome.Fail, TimeSpan.Zero, new[] { measurement }, Array.Empty<LimitResult>(), "No limit defined"));
        }

        var limitResult = LimitsEvaluator.Evaluate(limit, measurement);
        var outcome = limitResult.Pass ? StepOutcome.Pass : StepOutcome.Fail;
        return Task.FromResult(new StepResult(Name, outcome, TimeSpan.Zero, new[] { measurement }, new[] { limitResult }, limitResult.Pass ? null : limitResult.Message));
    }
}
