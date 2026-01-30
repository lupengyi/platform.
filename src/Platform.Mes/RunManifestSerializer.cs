using System.Text.Json;
using Platform.Contracts;

namespace Platform.Mes;

public static class RunManifestSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true
    };

    public static string Serialize(RunManifest manifest)
        => JsonSerializer.Serialize(manifest, Options);

    public static RunManifest Deserialize(string json)
        => JsonSerializer.Deserialize<RunManifest>(json, Options)
           ?? throw new InvalidOperationException("Failed to deserialize run manifest.");
}
