namespace IndustrialTestPlatform.Contracts;

public enum SlotStatus
{
    Idle,
    Running,
    Passed,
    Failed,
    Stopped,
    Error
}

public enum StepStatus
{
    Pending,
    Running,
    Passed,
    Failed,
    Skipped
}

public enum MeasurementUnit
{
    Unknown,
    Volt,
    Ampere
}

public sealed record Measurement(string Name, double Value, MeasurementUnit Unit);

public sealed record StepLog(string StepName, StepStatus Status, DateTimeOffset Timestamp);

public sealed record SlotLogEntry(
    DateTimeOffset Timestamp,
    string RunId,
    int SlotId,
    string Event,
    string Message,
    IReadOnlyDictionary<string, object?> Metadata);
