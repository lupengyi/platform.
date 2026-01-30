using IndustrialTestPlatform.Contracts;

namespace IndustrialTestPlatform.Core;

public interface IStructuredLogger
{
    void Log(SlotLogEntry entry);
}

public sealed class InMemoryStructuredLogger : IStructuredLogger
{
    private readonly List<SlotLogEntry> _entries = new();

    public IReadOnlyList<SlotLogEntry> Entries => _entries;

    public void Log(SlotLogEntry entry)
    {
        _entries.Add(entry);
    }
}

public sealed class CompositeStructuredLogger : IStructuredLogger
{
    private readonly IReadOnlyList<IStructuredLogger> _loggers;

    public CompositeStructuredLogger(params IStructuredLogger[] loggers)
    {
        _loggers = loggers;
    }

    public void Log(SlotLogEntry entry)
    {
        foreach (var logger in _loggers)
        {
            logger.Log(entry);
        }
    }
}

public sealed class ConsoleStructuredLogger : IStructuredLogger
{
    public void Log(SlotLogEntry entry)
    {
        var metadata = entry.Metadata.Count == 0
            ? string.Empty
            : string.Join(" ", entry.Metadata.Select(kvp => $"{kvp.Key}={kvp.Value}"));
        Console.WriteLine($"[{entry.Timestamp:O}] Run={entry.RunId} Slot={entry.SlotId} {entry.Event} {entry.Message} {metadata}".Trim());
    }
}
