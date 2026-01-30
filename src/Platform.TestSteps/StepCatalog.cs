using System.Reflection;
using Platform.Contracts;

namespace Platform.TestSteps;

public static class StepCatalog
{
    public static IReadOnlyDictionary<string, Func<ITestStep>> DiscoverBuiltInSteps()
    {
        return new Dictionary<string, Func<ITestStep>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Safety"] = () => new SafetyStep(),
            ["PowerUp"] = () => new PowerUpStep(),
            ["CommUp"] = () => new CommUpStep(),
            ["Measure"] = () => new MeasureStep(),
            ["Evaluate"] = () => new EvaluateStep()
        };
    }

    public static IReadOnlyDictionary<string, Func<ITestStep>> DiscoverPlugins(string pluginsPath)
    {
        var steps = new Dictionary<string, Func<ITestStep>>(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(pluginsPath))
        {
            return steps;
        }

        foreach (var dll in Directory.EnumerateFiles(pluginsPath, "*.dll"))
        {
            var assembly = Assembly.LoadFrom(dll);
            foreach (var type in assembly.GetTypes().Where(t => typeof(ITestStep).IsAssignableFrom(t) && !t.IsAbstract))
            {
                steps[type.Name] = () => (ITestStep)Activator.CreateInstance(type)!;
            }
        }

        return steps;
    }
}
