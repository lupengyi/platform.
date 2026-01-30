using Platform.Contracts;

namespace SamplePlugin.Steps;

[StepMetadata(
    stepId: "sample.greeting",
    name: "Greeting",
    category: "SamplePlugin",
    parametersSchema: "{\"type\":\"object\",\"properties\":{\"name\":{\"type\":\"string\"}},\"required\":[\"name\"]}")]
public sealed class PluginGreetingStep : TestStepPluginBase<PluginGreetingStep.Parameters>
{
    public sealed class Parameters
    {
        public string Name { get; set; } = string.Empty;
    }

    protected override Task ExecuteAsync(
        IStepContext context,
        Parameters parameters,
        CancellationToken cancellationToken)
    {
        context.Items["greeting"] = $"Hello, {parameters.Name}!";
        return Task.CompletedTask;
    }
}
