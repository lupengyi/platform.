namespace Platform.Contracts;

public sealed record RunSnapshot(
    string ConfigVersionsHash,
    string LimitsFileHash,
    IReadOnlyDictionary<string, string> PluginHashes,
    IReadOnlyList<string> InstrumentIdentities,
    string? OperatorId);
