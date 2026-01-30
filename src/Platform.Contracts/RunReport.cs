namespace Platform.Contracts;

public sealed record RunReport(
    string RunId,
    string Serial,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt,
    RunSnapshot Snapshot);
