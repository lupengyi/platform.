namespace Platform.Contracts;

public abstract class TestStepPluginBase<TParameters> : ITestStepPlugin
    where TParameters : class, new()
{
    protected TestStepPluginBase()
    {
        Metadata = StepMetadataAttribute.FromType(GetType())
            ?? throw new InvalidOperationException(
                $"Step metadata is required for {GetType().FullName}.");
    }

    public StepMetadata Metadata { get; }

    public Type ParametersType => typeof(TParameters);

    public Task ExecuteAsync(IStepContext context, object parameters, CancellationToken cancellationToken)
        => ExecuteAsync(context, (TParameters)parameters, cancellationToken);

    protected abstract Task ExecuteAsync(
        IStepContext context,
        TParameters parameters,
        CancellationToken cancellationToken);
}
