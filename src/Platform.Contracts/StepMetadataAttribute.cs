using System.Reflection;

namespace Platform.Contracts;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class StepMetadataAttribute : Attribute
{
    public StepMetadataAttribute(string stepId, string name, string category, string parametersSchema)
    {
        StepId = stepId;
        Name = name;
        Category = category;
        ParametersSchema = parametersSchema;
    }

    public string StepId { get; }
    public string Name { get; }
    public string Category { get; }
    public string ParametersSchema { get; }

    public StepMetadata ToMetadata() => new(StepId, Name, Category, ParametersSchema);

    public static StepMetadata? FromType(Type type)
        => type.GetCustomAttribute<StepMetadataAttribute>()?.ToMetadata();
}
