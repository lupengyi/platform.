using System.Text.Json.Serialization;
using IndustrialTestPlatform.Contracts;

namespace IndustrialTestPlatform.Core;

public sealed record SlotConfig
{
    public int SlotId { get; init; }
    public string Name { get; init; } = string.Empty;
}

public sealed record RetryOptions
{
    public int MaxAttempts { get; init; } = 3;
    public int DelayMs { get; init; } = 200;
}

public sealed record PlatformConfig
{
    public string RunName { get; init; } = "Demo";
    public string ReportsRoot { get; init; } = "Run";
    public string LimitsFile { get; init; } = string.Empty;
    public IReadOnlyList<SlotConfig> Slots { get; init; } = Array.Empty<SlotConfig>();
    public RetryOptions Retry { get; init; } = new();
}

public sealed record LimitDefinition
{
    public string Name { get; init; } = string.Empty;
    public double? Min { get; init; }
    public double? Max { get; init; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public MeasurementUnit Unit { get; init; } = MeasurementUnit.Unknown;
}

public sealed record LimitResult(string Name, double Value, MeasurementUnit Unit, bool Passed, string Message);
