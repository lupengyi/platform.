using Platform.Contracts;

namespace Platform.Core;

public interface ILogger
{
    void Log(LogEntry entry);
}

public sealed class ConsoleLogger : ILogger
{
    private readonly object _gate = new();

    public void Log(LogEntry entry)
    {
        lock (_gate)
        {
            Console.WriteLine($"{entry.TimestampUtc:O} [{entry.Level}] Slot {entry.SlotId} Correlation {entry.CorrelationId} - {entry.Message}");
        }
    }
}

public sealed class BufferingLogger : ILogger
{
    private readonly List<LogEntry> _entries = new();

    public IReadOnlyList<LogEntry> Entries => _entries;

    public void Log(LogEntry entry)
    {
        _entries.Add(entry);
    }
}

public static class Log
{
    public static void Write(ILogger logger, Guid correlationId, int slotId, LogLevel level, string message)
    {
        logger.Log(new LogEntry(DateTimeOffset.UtcNow, correlationId, slotId, level, message));
    }
}
