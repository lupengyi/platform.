using Platform.Contracts;

namespace Platform.TestSteps;

public sealed class StepDefinition
{
    public StepDefinition(ITestStepPlugin instance)
    {
        Instance = instance;
        Metadata = instance.Metadata;
        ParametersType = instance.ParametersType;
    }

    public StepMetadata Metadata { get; }
    public ITestStepPlugin Instance { get; }
    public Type ParametersType { get; }
}
