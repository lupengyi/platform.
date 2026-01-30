using System.Globalization;
using System.Text.Json;
using IndustrialTestPlatform.Contracts;

namespace IndustrialTestPlatform.Core;

public sealed class ConfigException : Exception
{
    public ConfigException(string message) : base(message)
    {
    }
}

public sealed record ConfigBundle(PlatformConfig Config, IReadOnlyDictionary<string, LimitDefinition> Limits);

public static class ConfigLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true
    };

    public static ConfigBundle Load(string jsonPath)
    {
        if (!File.Exists(jsonPath))
        {
            throw new ConfigException($"Config file not found: {jsonPath}");
        }

        var json = File.ReadAllText(jsonPath);
        var config = JsonSerializer.Deserialize<PlatformConfig>(json, JsonOptions)
            ?? throw new ConfigException("Unable to parse configuration.");

        ValidateConfig(config);

        var limitsPath = Path.IsPathRooted(config.LimitsFile)
            ? config.LimitsFile
            : Path.GetFullPath(Path.Combine(Path.GetDirectoryName(jsonPath) ?? string.Empty, config.LimitsFile));

        var limits = LoadLimitsCsv(limitsPath);

        return new ConfigBundle(config, limits);
    }

    public static IReadOnlyDictionary<string, LimitDefinition> LoadLimitsCsv(string csvPath)
    {
        if (!File.Exists(csvPath))
        {
            throw new ConfigException($"Limits file not found: {csvPath}");
        }

        var lines = File.ReadAllLines(csvPath)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();

        if (lines.Length == 0)
        {
            throw new ConfigException("Limits CSV file is empty.");
        }

        var limits = new Dictionary<string, LimitDefinition>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (line.StartsWith('#'))
            {
                continue;
            }

            var parts = line.Split(',');
            if (parts.Length < 4)
            {
                throw new ConfigException($"Limits CSV format error at line {i + 1}: '{line}'");
            }

            var name = parts[0].Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ConfigException($"Limit name missing at line {i + 1}.");
            }

            var min = ParseNullable(parts[1]);
            var max = ParseNullable(parts[2]);
            if (!Enum.TryParse<MeasurementUnit>(parts[3].Trim(), true, out var unit))
            {
                throw new ConfigException($"Invalid unit '{parts[3]}' at line {i + 1}.");
            }

            limits[name] = new LimitDefinition
            {
                Name = name,
                Min = min,
                Max = max,
                Unit = unit
            };
        }

        return limits;
    }

    private static void ValidateConfig(PlatformConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.ReportsRoot))
        {
            throw new ConfigException("ReportsRoot is required.");
        }

        if (string.IsNullOrWhiteSpace(config.LimitsFile))
        {
            throw new ConfigException("LimitsFile is required.");
        }

        if (config.Slots is null || config.Slots.Count == 0)
        {
            throw new ConfigException("At least one slot must be configured.");
        }
    }

    private static double? ParseNullable(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!double.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            throw new ConfigException($"Invalid numeric value '{value}'.");
        }

        return parsed;
    }
}
