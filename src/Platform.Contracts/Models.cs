namespace Platform.Contracts;

public enum SlotState
{
    Idle,
    Running,
    Pass,
    Fail,
    Stopped
}

public enum StepOutcome
{
    Pass,
    Fail,
    Error,
    Skipped
}

public sealed record Measurement(
    string Name,
    double Value,
    string Unit,
    DateTimeOffset TimestampUtc);

public sealed record LimitDefinition(
    string Name,
    string Unit,
    double? Lsl,
    double? Usl,
    double? Target);

public sealed record LimitResult(
    string Name,
    string Unit,
    double Value,
    double? Lsl,
    double? Usl,
    double? Target,
    bool Pass,
    string Message);

public sealed record StepResult(
    string StepName,
    StepOutcome Outcome,
    TimeSpan Duration,
    IReadOnlyList<Measurement> Measurements,
    IReadOnlyList<LimitResult> Limits,
    string? Error);

public sealed record SlotReport(
    int SlotId,
    string SerialNumber,
    SlotState FinalState,
    IReadOnlyList<StepResult> Steps,
    DateTimeOffset StartedUtc,
    DateTimeOffset FinishedUtc,
    Guid RunId);

public sealed record RunSummary(
    Guid RunId,
    DateTimeOffset StartedUtc,
    DateTimeOffset FinishedUtc,
    int SlotCount,
    int Passed,
    int Failed,
    int Stopped);
