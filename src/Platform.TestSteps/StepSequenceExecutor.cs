using Platform.Contracts;

namespace Platform.TestSteps;

public sealed class StepSequenceExecutor
{
    private readonly StepRegistry _registry;

    public StepSequenceExecutor(StepRegistry registry)
    {
        _registry = registry;
    }

    public async Task ExecuteAsync(StepSequence sequence, IStepContext context, CancellationToken cancellationToken)
    {
        foreach (var item in sequence.Steps)
        {
            var definition = _registry.GetById(item.StepId);
            var parameters = _registry.BindParameters(definition.ParametersType, item.Parameters);
            await definition.Instance.ExecuteAsync(context, parameters, cancellationToken).ConfigureAwait(false);
        }
    }
}
