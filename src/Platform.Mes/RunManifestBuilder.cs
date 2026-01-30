using Platform.Contracts;

namespace Platform.Mes;

public sealed class RunManifestBuilder
{
    public RunManifest Build(
        string runId,
        string serial,
        string? configVersionsPath,
        string? limitsFilePath,
        IEnumerable<string> pluginFiles,
        IEnumerable<string> instrumentIdentities,
        string? operatorId,
        DateTimeOffset? createdAt = null)
    {
        var configHash = string.IsNullOrWhiteSpace(configVersionsPath)
            ? Hashing.ComputeContentHash(string.Empty)
            : Hashing.ComputeFileHash(configVersionsPath);

        var limitsHash = string.IsNullOrWhiteSpace(limitsFilePath)
            ? Hashing.ComputeContentHash(string.Empty)
            : Hashing.ComputeFileHash(limitsFilePath);

        var pluginHashes = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var pluginFile in pluginFiles ?? Array.Empty<string>())
        {
            if (string.IsNullOrWhiteSpace(pluginFile))
            {
                continue;
            }

            var pluginName = Path.GetFileName(pluginFile);
            var pluginHash = Hashing.ComputeFileHash(pluginFile);
            pluginHashes[pluginName] = pluginHash;
        }

        var instruments = instrumentIdentities
            ?.Where(identity => !string.IsNullOrWhiteSpace(identity))
            .OrderBy(identity => identity, StringComparer.Ordinal)
            .ToList()
            ?? new List<string>();

        var snapshot = new RunSnapshot(
            configHash,
            limitsHash,
            pluginHashes,
            instruments,
            operatorId);

        return new RunManifest(
            runId,
            serial,
            createdAt ?? DateTimeOffset.UtcNow,
            snapshot);
    }
}
