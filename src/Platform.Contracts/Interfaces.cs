namespace Platform.Contracts;

public interface IInstrument
{
    string Name { get; }
    Task InitializeAsync(CancellationToken cancellationToken);
}

public interface IDmm : IInstrument
{
    Task<double> MeasureVoltageAsync(CancellationToken cancellationToken);
}

public interface IPsu : IInstrument
{
    Task SetOutputAsync(bool enabled, double voltage, double currentLimit, CancellationToken cancellationToken);
}

public interface ICanBus : IInstrument
{
    Task<string> SendAsync(string payload, CancellationToken cancellationToken);
}

public interface ITestContext
{
    Guid RunId { get; }
    Guid CorrelationId { get; }
    int SlotId { get; }
    string SerialNumber { get; }
    DateTimeOffset StartUtc { get; }
    IInstrumentServices Instruments { get; }
    IProgress<LogEntry> Log { get; }
    IReadOnlyDictionary<string, LimitDefinition> Limits { get; }
    IDictionary<string, object> State { get; }
}

public interface IInstrumentServices
{
    IDmm Dmm { get; }
    IPsu Psu { get; }
    ICanBus Can { get; }
}

public interface ITestStep
{
    string Name { get; }
    Task<StepResult> ExecuteAsync(ITestContext context, CancellationToken cancellationToken);
}

public sealed record LogEntry(
    DateTimeOffset TimestampUtc,
    Guid CorrelationId,
    int SlotId,
    LogLevel Level,
    string Message);

public enum LogLevel
{
    Trace,
    Debug,
    Info,
    Warning,
    Error,
    Critical
}
