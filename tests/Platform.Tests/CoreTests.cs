using Platform.Automation;
using Platform.Contracts;
using Platform.Core;
using Platform.Libraries;
using Xunit;

namespace Platform.Tests;

public sealed class LimitsEvaluatorTests
{
    [Fact]
    public void Evaluate_Pass_WhenWithinLimits()
    {
        var limit = new LimitDefinition("Voltage", "V", 2.9, 3.4, 3.2);
        var measurement = new Measurement("Voltage", 3.1, "V", DateTimeOffset.UtcNow);
        var result = LimitsEvaluator.Evaluate(limit, measurement);

        Assert.True(result.Pass);
    }

    [Fact]
    public void Evaluate_Fail_WhenOutsideLimits()
    {
        var limit = new LimitDefinition("Voltage", "V", 2.9, 3.4, 3.2);
        var measurement = new Measurement("Voltage", 3.8, "V", DateTimeOffset.UtcNow);
        var result = LimitsEvaluator.Evaluate(limit, measurement);

        Assert.False(result.Pass);
        Assert.Contains("USL", result.Message);
    }
}

public sealed class ConfigValidationTests
{
    [Fact]
    public void ValidateStationConfig_Fails_WhenSlotCountInvalid()
    {
        var config = new StationConfig("Demo", 0, 1, new InstrumentTimeouts(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1)), new[] { "Safety" });
        var result = ConfigLoader.ValidateStationConfig(config);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task LoadStationConfig_RoundTrips()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"station_{Guid.NewGuid():N}.json");
        var config = new StationConfig("Demo", 2, 1, new InstrumentTimeouts(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1)), new[] { "Safety" });
        await ConfigLoader.SaveStationConfigAsync(tempPath, config, CancellationToken.None);
        var loaded = await ConfigLoader.LoadStationConfigAsync(tempPath, CancellationToken.None);

        Assert.Equal(config.StationName, loaded.StationName);
    }
}

public sealed class RetryPolicyTests
{
    [Fact]
    public async Task RetryPolicy_Retries_UntilSuccess()
    {
        var logger = new BufferingLogger();
        var policy = new RetryPolicy(new RetryOptions(3, TimeSpan.FromMilliseconds(10), false), logger, Guid.NewGuid(), 1);
        var attempts = 0;

        var result = await policy.ExecuteAsync<int>(_ =>
        {
            attempts++;
            if (attempts < 2)
            {
                throw new InvalidOperationException("fail");
            }
            return Task.FromResult(42);
        }, CancellationToken.None);

        Assert.Equal(42, result);
        Assert.Equal(2, attempts);
    }
}
