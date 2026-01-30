# platform.

This repository provides a minimal plugin-based step system for test sequences.

## Build

```bash
# Build all projects (copies SamplePlugin.Steps into ./plugins)
dotnet build
```

## Run (example usage)

```csharp
using System.Text.Json;
using Platform.TestSteps;

var pluginOptions = new PluginLoadOptions
{
    PluginDirectory = Path.Combine(AppContext.BaseDirectory, "plugins"),
    Security =
    {
        AllowedSha256 =
        {
            // Fill with the SHA256 from your plugin DLL (see examples/plugin-settings.json).
        }
    }
};

var registry = StepRegistry.Discover(pluginOptions);
var sequence = JsonSerializer.Deserialize<StepSequence>(
    File.ReadAllText("examples/sequence.json"),
    new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

var executor = new StepSequenceExecutor(registry);
await executor.ExecuteAsync(sequence, new StepContext(), CancellationToken.None);
```

## Tests

```bash
dotnet test
```

## Plugins

Plugins are discovered from:
- Built-in step assemblies (the `Platform.TestSteps` assembly by default).
- External assemblies in the local `./plugins` folder.

Security rules:
- Plugins must be strong-name signed **or** their SHA256 hash must be in the allowlist.
- The allowlist can be stored in configuration; see `examples/plugin-settings.json` for a sample.

## Examples

- `examples/sequence.json` shows a sequence that references both built-in and plugin step IDs.
