using System.Text.Json;

namespace Platform.TestSteps;

public sealed class StepSequence
{
    public List<StepSequenceItem> Steps { get; set; } = new();
}

public sealed class StepSequenceItem
{
    public string StepId { get; set; } = string.Empty;
    public JsonElement Parameters { get; set; }
}
