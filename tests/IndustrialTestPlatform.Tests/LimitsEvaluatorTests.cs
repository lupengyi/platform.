using IndustrialTestPlatform.Contracts;
using IndustrialTestPlatform.Core;
using Xunit;

namespace IndustrialTestPlatform.Tests;

public sealed class LimitsEvaluatorTests
{
    [Fact]
    public void Evaluate_Passes_WhenWithinBounds()
    {
        var limits = new Dictionary<string, LimitDefinition>
        {
            ["VBAT"] = new LimitDefinition { Name = "VBAT", Min = 11.5, Max = 12.6, Unit = MeasurementUnit.Volt }
        };
        var measurements = new[] { new Measurement("VBAT", 12.1, MeasurementUnit.Volt) };

        var result = LimitsEvaluator.Evaluate(measurements, limits);

        Assert.True(result.Passed);
        Assert.Single(result.Results);
        Assert.True(result.Results[0].Passed);
    }

    [Fact]
    public void Evaluate_Fails_WhenOutOfBounds()
    {
        var limits = new Dictionary<string, LimitDefinition>
        {
            ["VBAT"] = new LimitDefinition { Name = "VBAT", Min = 11.5, Max = 12.6, Unit = MeasurementUnit.Volt }
        };
        var measurements = new[] { new Measurement("VBAT", 12.9, MeasurementUnit.Volt) };

        var result = LimitsEvaluator.Evaluate(measurements, limits);

        Assert.False(result.Passed);
        Assert.Contains(result.Results, r => !r.Passed);
    }

    [Fact]
    public void Evaluate_Fails_WhenLimitMissing()
    {
        var limits = new Dictionary<string, LimitDefinition>();
        var measurements = new[] { new Measurement("VBAT", 12.1, MeasurementUnit.Volt) };

        var result = LimitsEvaluator.Evaluate(measurements, limits);

        Assert.False(result.Passed);
        Assert.Equal("Limit missing", result.Results[0].Message);
    }

    [Fact]
    public void Evaluate_Fails_WhenNaN()
    {
        var limits = new Dictionary<string, LimitDefinition>
        {
            ["VBAT"] = new LimitDefinition { Name = "VBAT", Min = 11.5, Max = 12.6, Unit = MeasurementUnit.Volt }
        };
        var measurements = new[] { new Measurement("VBAT", double.NaN, MeasurementUnit.Volt) };

        var result = LimitsEvaluator.Evaluate(measurements, limits);

        Assert.False(result.Passed);
        Assert.Equal("Invalid measurement", result.Results[0].Message);
    }

    [Fact]
    public void Evaluate_Fails_WhenUnitMismatch()
    {
        var limits = new Dictionary<string, LimitDefinition>
        {
            ["VBAT"] = new LimitDefinition { Name = "VBAT", Min = 11.5, Max = 12.6, Unit = MeasurementUnit.Volt }
        };
        var measurements = new[] { new Measurement("VBAT", 12.1, MeasurementUnit.Ampere) };

        var result = LimitsEvaluator.Evaluate(measurements, limits);

        Assert.False(result.Passed);
        Assert.Equal("Unit mismatch", result.Results[0].Message);
    }
}
