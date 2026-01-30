using System.Text.Json;
using IndustrialTestPlatform.Contracts;

namespace IndustrialTestPlatform.Core;

public sealed record SlotReport(
    int SlotId,
    string RunId,
    DateTimeOffset StartedAt,
    DateTimeOffset FinishedAt,
    SlotStatus FinalStatus,
    IReadOnlyList<Measurement> Measurements,
    IReadOnlyList<LimitResult> LimitResults,
    IReadOnlyList<SlotLogEntry> Logs);

public sealed record SummaryReport(string RunId, DateTimeOffset StartedAt, DateTimeOffset FinishedAt, IReadOnlyList<SlotSummary> Slots);

public sealed record SlotSummary(int SlotId, SlotStatus FinalStatus, bool Passed);

public static class ReportWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static void WriteSlotReport(string path, SlotReport report)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        var json = JsonSerializer.Serialize(report, JsonOptions);
        File.WriteAllText(path, json);
    }

    public static void WriteSummaryCsv(string path, SummaryReport report)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        using var writer = new StreamWriter(path);
        writer.WriteLine("SlotId,FinalStatus,Passed");
        foreach (var slot in report.Slots)
        {
            writer.WriteLine($"{slot.SlotId},{slot.FinalStatus},{slot.Passed}");
        }
    }
}
