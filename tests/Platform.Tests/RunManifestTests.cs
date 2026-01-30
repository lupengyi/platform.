using Platform.Contracts;
using Platform.Mes;
using Xunit;

namespace Platform.Tests;

public sealed class RunManifestTests
{
    [Fact]
    public void ComputeFileHash_IsDeterministic()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, "abc");
            var hash = Hashing.ComputeFileHash(tempFile);

            Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", hash);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void Serialize_ProducesStableOutput()
    {
        var snapshot = new RunSnapshot(
            "config-hash",
            "limits-hash",
            new Dictionary<string, string>
            {
                ["plugin-b"] = "hash-b",
                ["plugin-a"] = "hash-a"
            },
            new List<string> { "Instrument-2", "Instrument-1" },
            "operator-1");

        var manifest = new RunManifest(
            "run-123",
            "serial-456",
            new DateTimeOffset(2024, 1, 2, 3, 4, 5, TimeSpan.Zero),
            snapshot);

        var normalized = new RunManifestBuilder().Build(
            manifest.RunId,
            manifest.Serial,
            null,
            null,
            Array.Empty<string>(),
            manifest.Snapshot.InstrumentIdentities,
            manifest.Snapshot.OperatorId,
            manifest.CreatedAt);

        var expected = """
{
  \"RunId\": \"run-123\",
  \"Serial\": \"serial-456\",
  \"CreatedAt\": \"2024-01-02T03:04:05+00:00\",
  \"Snapshot\": {
    \"ConfigVersionsHash\": \"e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855\",
    \"LimitsFileHash\": \"e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855\",
    \"PluginHashes\": {},
    \"InstrumentIdentities\": [
      \"Instrument-1\",
      \"Instrument-2\"
    ],
    \"OperatorId\": \"operator-1\"
  }
}
""";

        var json = RunManifestSerializer.Serialize(normalized);
        Assert.Equal(expected.Trim(), json);
    }
}
