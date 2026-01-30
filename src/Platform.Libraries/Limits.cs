using System.Globalization;
using Platform.Contracts;

namespace Platform.Libraries;

public static class LimitsEvaluator
{
    public static LimitResult Evaluate(LimitDefinition limit, Measurement measurement)
    {
        var pass = true;
        var message = "OK";

        if (!string.Equals(limit.Name, measurement.Name, StringComparison.OrdinalIgnoreCase))
        {
            pass = false;
            message = "Measurement name mismatch.";
        }
        else
        {
            if (limit.Lsl is not null && measurement.Value < limit.Lsl.Value)
            {
                pass = false;
                message = "Below LSL";
            }
            if (limit.Usl is not null && measurement.Value > limit.Usl.Value)
            {
                pass = false;
                message = pass ? "Above USL" : message + "; Above USL";
            }
        }

        return new LimitResult(
            limit.Name,
            limit.Unit,
            measurement.Value,
            limit.Lsl,
            limit.Usl,
            limit.Target,
            pass,
            message);
    }
}

public static class LimitCsvParser
{
    public static IReadOnlyList<LimitDefinition> Parse(string csv)
    {
        var lines = csv.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length <= 1)
        {
            return Array.Empty<LimitDefinition>();
        }

        var limits = new List<LimitDefinition>();
        for (var i = 1; i < lines.Length; i++)
        {
            var parts = lines[i].Split(',');
            if (parts.Length < 4)
            {
                continue;
            }
            var name = parts[0].Trim();
            var unit = parts[1].Trim();
            var lsl = ParseNullable(parts[2]);
            var usl = ParseNullable(parts[3]);
            var target = parts.Length > 4 ? ParseNullable(parts[4]) : null;
            limits.Add(new LimitDefinition(name, unit, lsl, usl, target));
        }
        return limits;
    }

    public static async Task<IReadOnlyList<LimitDefinition>> LoadAsync(string path, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(path);
        var content = await reader.ReadToEndAsync(cancellationToken);
        return Parse(content);
    }

    private static double? ParseNullable(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }
        if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }
        return null;
    }
}
