using Platform.Contracts;
using Platform.Core;

namespace Platform.Automation;

public enum FailurePolicy
{
    StopOnFail,
    ContinueOnFail
}

public sealed record SequenceResult(
    IReadOnlyList<StepResult> Steps,
    bool Passed,
    DateTimeOffset StartedUtc,
    DateTimeOffset FinishedUtc);

public sealed class SequenceRunner
{
    private readonly ILogger _logger;

    public SequenceRunner(ILogger logger)
    {
        _logger = logger;
    }

    public async Task<SequenceResult> RunAsync(
        IReadOnlyList<ITestStep> steps,
        ITestContext context,
        FailurePolicy policy,
        CancellationToken cancellationToken)
    {
        var results = new List<StepResult>();
        var start = DateTimeOffset.UtcNow;
        var passed = true;

        foreach (var step in steps)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Log.Write(_logger, context.CorrelationId, context.SlotId, LogLevel.Info, $"Starting step {step.Name}.");
            var result = await step.ExecuteAsync(context, cancellationToken);
            results.Add(result);
            Log.Write(_logger, context.CorrelationId, context.SlotId, LogLevel.Info, $"Finished step {step.Name} with {result.Outcome}.");

            if (result.Outcome != StepOutcome.Pass)
            {
                passed = false;
                if (policy == FailurePolicy.StopOnFail)
                {
                    break;
                }
            }
        }

        return new SequenceResult(results, passed, start, DateTimeOffset.UtcNow);
    }
}
