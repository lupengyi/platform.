namespace Platform.Contracts;

public sealed record UnitInfo(
    string Serial,
    string Status,
    IReadOnlyDictionary<string, string> Attributes);
