using Platform.Contracts;

namespace Platform.Mes;

public static class RunManifestWriter
{
    public static async Task WriteAsync(string runFolder, RunManifest manifest, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(runFolder);
        var manifestPath = Path.Combine(runFolder, "manifest.json");
        var json = RunManifestSerializer.Serialize(manifest);
        await File.WriteAllTextAsync(manifestPath, json, cancellationToken);
    }
}
