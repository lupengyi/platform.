using Platform.Contracts;

namespace Platform.TestSteps;

public sealed class StepContext : IStepContext
{
    public IDictionary<string, object?> Items { get; } = new Dictionary<string, object?>();
}
