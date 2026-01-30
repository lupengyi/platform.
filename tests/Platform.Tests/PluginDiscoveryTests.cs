using System.Security.Cryptography;
using System.Text.Json;
using Platform.TestSteps;
using Platform.TestSteps.Steps;
using SamplePlugin.Steps;
using Xunit;

namespace Platform.Tests;

public sealed class PluginDiscoveryTests
{
    [Fact]
    public void Discovers_BuiltIn_And_Plugin_Steps()
    {
        var pluginPath = typeof(PluginGreetingStep).Assembly.Location;
        var pluginDirectory = CreatePluginDirectoryWith(pluginPath, out var pluginHash);

        var registry = StepRegistry.Discover(new PluginLoadOptions
        {
            PluginDirectory = pluginDirectory,
            Security = { AllowedSha256 = { pluginHash } }
        });

        var ids = registry.GetAll().Select(definition => definition.Metadata.StepId).ToArray();

        Assert.Contains("builtin.echo", ids);
        Assert.Contains("sample.greeting", ids);
    }

    [Fact]
    public async Task Binds_Parameters_For_Sequence_Items()
    {
        var pluginPath = typeof(PluginGreetingStep).Assembly.Location;
        var pluginDirectory = CreatePluginDirectoryWith(pluginPath, out var pluginHash);

        var registry = StepRegistry.Discover(new PluginLoadOptions
        {
            PluginDirectory = pluginDirectory,
            Security = { AllowedSha256 = { pluginHash } }
        });

        var sequence = new StepSequence
        {
            Steps =
            {
                new StepSequenceItem
                {
                    StepId = "builtin.echo",
                    Parameters = JsonDocument.Parse("{\"message\":\"Hello\"}").RootElement
                },
                new StepSequenceItem
                {
                    StepId = "sample.greeting",
                    Parameters = JsonDocument.Parse("{\"name\":\"Ada\"}").RootElement
                }
            }
        };

        var context = new StepContext();
        var executor = new StepSequenceExecutor(registry);

        await executor.ExecuteAsync(sequence, context, CancellationToken.None);

        Assert.Equal("Hello", context.Items["echo"]);
        Assert.Equal("Hello, Ada!", context.Items["greeting"]);
    }

    private static string CreatePluginDirectoryWith(string pluginPath, out string hash)
    {
        var pluginDirectory = Path.Combine(Path.GetTempPath(), "platform-plugins", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(pluginDirectory);
        var destinationPath = Path.Combine(pluginDirectory, Path.GetFileName(pluginPath));
        File.Copy(pluginPath, destinationPath, true);

        hash = ComputeSha256(destinationPath);
        return pluginDirectory;
    }

    private static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(stream);
        return Convert.ToHexString(hash);
    }
}
