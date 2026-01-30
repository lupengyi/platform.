namespace Platform.Libraries;

public sealed record InstrumentTimeouts(
    TimeSpan Dmm,
    TimeSpan Psu,
    TimeSpan Can);

public sealed record StationConfig(
    string StationName,
    int SlotCount,
    int Columns,
    InstrumentTimeouts Timeouts,
    IReadOnlyList<string> StepPlan);

public sealed record AppConfig(
    string StationConfigPath,
    string LimitsCsvPath);
