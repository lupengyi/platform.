using System.Reflection;
using System.Text.Json;
using Platform.Contracts;

namespace Platform.TestSteps;

public sealed class StepRegistry
{
    private readonly Dictionary<string, StepDefinition> _definitions;
    private readonly JsonSerializerOptions _serializerOptions;

    private StepRegistry(
        Dictionary<string, StepDefinition> definitions,
        JsonSerializerOptions? serializerOptions)
    {
        _definitions = definitions;
        _serializerOptions = serializerOptions ?? new JsonSerializerOptions(JsonSerializerDefaults.Web);
    }

    public static StepRegistry Discover(
        PluginLoadOptions pluginOptions,
        JsonSerializerOptions? serializerOptions = null,
        params Assembly[] builtInAssemblies)
    {
        var definitions = new Dictionary<string, StepDefinition>(StringComparer.OrdinalIgnoreCase);
        var assemblies = new List<Assembly>();
        assemblies.AddRange(builtInAssemblies.Length == 0
            ? new[] { typeof(StepRegistry).Assembly }
            : builtInAssemblies);

        var loader = new PluginLoader();
        assemblies.AddRange(loader.LoadAssemblies(pluginOptions));

        foreach (var assembly in assemblies.Distinct())
        {
            foreach (var type in assembly.GetTypes())
            {
                if (!typeof(ITestStepPlugin).IsAssignableFrom(type) || type.IsAbstract)
                {
                    continue;
                }

                if (Activator.CreateInstance(type) is not ITestStepPlugin instance)
                {
                    continue;
                }

                var definition = new StepDefinition(instance);
                definitions[definition.Metadata.StepId] = definition;
            }
        }

        return new StepRegistry(definitions, serializerOptions);
    }

    public StepDefinition GetById(string stepId)
    {
        if (!_definitions.TryGetValue(stepId, out var definition))
        {
            throw new KeyNotFoundException($"Step '{stepId}' was not found.");
        }

        return definition;
    }

    public IReadOnlyCollection<StepDefinition> GetAll() => _definitions.Values.ToArray();

    public object BindParameters(Type parametersType, JsonElement parameters)
    {
        var result = JsonSerializer.Deserialize(parameters, parametersType, _serializerOptions);
        return result ?? Activator.CreateInstance(parametersType)
            ?? throw new InvalidOperationException("Unable to bind parameters.");
    }
}
