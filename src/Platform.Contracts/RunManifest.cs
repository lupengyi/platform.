namespace Platform.Contracts;

public sealed record RunManifest(
    string RunId,
    string Serial,
    DateTimeOffset CreatedAt,
    RunSnapshot Snapshot);
