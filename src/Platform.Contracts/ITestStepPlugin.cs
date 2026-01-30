namespace Platform.Contracts;

public interface ITestStepPlugin
{
    StepMetadata Metadata { get; }
    Type ParametersType { get; }
    Task ExecuteAsync(IStepContext context, object parameters, CancellationToken cancellationToken);
}
