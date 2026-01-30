namespace Platform.Contracts;

public sealed record StepMetadata(
    string StepId,
    string Name,
    string Category,
    string ParametersSchema);
