using IndustrialTestPlatform.Core;
using Xunit;

namespace IndustrialTestPlatform.Tests;

public sealed class ConfigLoaderTests
{
    [Fact]
    public void Load_Throws_WhenMissingKeys()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var configPath = Path.Combine(tempDir, "config.json");
        File.WriteAllText(configPath, "{ \"RunName\": \"Demo\" }");

        var ex = Assert.Throws<ConfigException>(() => ConfigLoader.Load(configPath));

        Assert.Contains("ReportsRoot", ex.Message);
    }

    [Fact]
    public void LoadLimitsCsv_Throws_WhenCsvBadFormat()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var csvPath = Path.Combine(tempDir, "limits.csv");
        File.WriteAllText(csvPath, "VBAT,11.0\n");

        var ex = Assert.Throws<ConfigException>(() => ConfigLoader.LoadLimitsCsv(csvPath));

        Assert.Contains("format error", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
