using Platform.Contracts;

namespace Platform.TestSteps.Steps;

[StepMetadata(
    stepId: "builtin.echo",
    name: "Echo",
    category: "BuiltIn",
    parametersSchema: "{\"type\":\"object\",\"properties\":{\"message\":{\"type\":\"string\"}},\"required\":[\"message\"]}")]
public sealed class EchoStep : TestStepPluginBase<EchoStep.Parameters>
{
    public sealed class Parameters
    {
        public string Message { get; set; } = string.Empty;
    }

    protected override Task ExecuteAsync(
        IStepContext context,
        Parameters parameters,
        CancellationToken cancellationToken)
    {
        context.Items["echo"] = parameters.Message;
        return Task.CompletedTask;
    }
}
