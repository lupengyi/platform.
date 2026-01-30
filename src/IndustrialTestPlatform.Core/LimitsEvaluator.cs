using IndustrialTestPlatform.Contracts;

namespace IndustrialTestPlatform.Core;

public sealed record LimitsEvaluation(bool Passed, IReadOnlyList<LimitResult> Results);

public static class LimitsEvaluator
{
    public static LimitsEvaluation Evaluate(
        IEnumerable<Measurement> measurements,
        IReadOnlyDictionary<string, LimitDefinition> limits)
    {
        var results = new List<LimitResult>();
        var passed = true;

        foreach (var measurement in measurements)
        {
            if (!limits.TryGetValue(measurement.Name, out var limit))
            {
                results.Add(new LimitResult(measurement.Name, measurement.Value, measurement.Unit, false, "Limit missing"));
                passed = false;
                continue;
            }

            if (measurement.Unit != limit.Unit)
            {
                results.Add(new LimitResult(measurement.Name, measurement.Value, measurement.Unit, false, "Unit mismatch"));
                passed = false;
                continue;
            }

            if (double.IsNaN(measurement.Value) || double.IsInfinity(measurement.Value))
            {
                results.Add(new LimitResult(measurement.Name, measurement.Value, measurement.Unit, false, "Invalid measurement"));
                passed = false;
                continue;
            }

            var isPass = true;
            if (limit.Min.HasValue && measurement.Value < limit.Min.Value)
            {
                isPass = false;
            }

            if (limit.Max.HasValue && measurement.Value > limit.Max.Value)
            {
                isPass = false;
            }

            if (!isPass)
            {
                passed = false;
                results.Add(new LimitResult(measurement.Name, measurement.Value, measurement.Unit, false, "Out of bounds"));
            }
            else
            {
                results.Add(new LimitResult(measurement.Name, measurement.Value, measurement.Unit, true, "Within limits"));
            }
        }

        return new LimitsEvaluation(passed, results);
    }
}
