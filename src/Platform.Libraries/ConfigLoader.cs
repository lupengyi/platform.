using System.Text.Json;
using Platform.Core;

namespace Platform.Libraries;

public static class ConfigLoader
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static async Task<StationConfig> LoadStationConfigAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var config = await JsonSerializer.DeserializeAsync<StationConfig>(stream, Options, cancellationToken);
        if (config is null)
        {
            throw new InvalidOperationException($"Unable to read station config from {path}.");
        }
        return config;
    }

    public static async Task<AppConfig> LoadAppConfigAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var config = await JsonSerializer.DeserializeAsync<AppConfig>(stream, Options, cancellationToken);
        if (config is null)
        {
            throw new InvalidOperationException($"Unable to read app config from {path}.");
        }
        return config;
    }

    public static async Task SaveStationConfigAsync(string path, StationConfig config, CancellationToken cancellationToken)
    {
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, config, Options, cancellationToken);
    }

    public static OperationResult ValidateStationConfig(StationConfig config)
    {
        if (config.SlotCount <= 0)
        {
            return OperationResult.Fail("SlotCount must be greater than zero.");
        }

        if (config.Columns <= 0)
        {
            return OperationResult.Fail("Columns must be greater than zero.");
        }

        if (config.StepPlan.Count == 0)
        {
            return OperationResult.Fail("StepPlan must include at least one step.");
        }

        return OperationResult.Ok();
    }
}
