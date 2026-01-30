namespace Platform.TestSteps;

public sealed class PluginLoadOptions
{
    public string PluginDirectory { get; set; } = Path.Combine(AppContext.BaseDirectory, "plugins");
    public PluginSecurityOptions Security { get; } = new();
}
