using Platform.Automation;
using Platform.Contracts;
using Platform.Core;
using Platform.Instruments;
using Platform.Libraries;

namespace Platform.InstrumentManager;

public sealed class InstrumentManager : IInstrumentServices
{
    private readonly RetryPolicy _policy;
    private readonly InstrumentTimeouts _timeouts;
    private readonly ILogger _logger;
    private readonly Guid _correlationId;
    private readonly int _slotId;

    public InstrumentManager(InstrumentTimeouts timeouts, RetryPolicy policy, ILogger logger, Guid correlationId, int slotId, int seed)
    {
        _timeouts = timeouts;
        _policy = policy;
        _logger = logger;
        _correlationId = correlationId;
        _slotId = slotId;
        Dmm = new TimedDmm(new MockDmm(seed + 1), policy, timeouts.Dmm, logger, correlationId, slotId);
        Psu = new TimedPsu(new MockPsu(seed + 2), policy, timeouts.Psu, logger, correlationId, slotId);
        Can = new TimedCanBus(new MockCanBus(seed + 3), policy, timeouts.Can, logger, correlationId, slotId);
    }

    public IDmm Dmm { get; }
    public IPsu Psu { get; }
    public ICanBus Can { get; }
}

internal sealed class TimedDmm : IDmm
{
    private readonly IDmm _inner;
    private readonly RetryPolicy _policy;
    private readonly TimeSpan _timeout;
    private readonly ILogger _logger;
    private readonly Guid _correlationId;
    private readonly int _slotId;

    public TimedDmm(IDmm inner, RetryPolicy policy, TimeSpan timeout, ILogger logger, Guid correlationId, int slotId)
    {
        _inner = inner;
        _policy = policy;
        _timeout = timeout;
        _logger = logger;
        _correlationId = correlationId;
        _slotId = slotId;
    }

    public string Name => _inner.Name;

    public Task InitializeAsync(CancellationToken cancellationToken)
    {
        return _inner.InitializeAsync(cancellationToken);
    }

    public Task<double> MeasureVoltageAsync(CancellationToken cancellationToken)
    {
        return _policy.ExecuteAsync(async token =>
        {
            Log.Write(_logger, _correlationId, _slotId, LogLevel.Debug, "DMM measurement started.");
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeoutCts.CancelAfter(_timeout);
            var value = await _inner.MeasureVoltageAsync(timeoutCts.Token);
            Log.Write(_logger, _correlationId, _slotId, LogLevel.Debug, $"DMM measurement completed: {value:F3} V.");
            return value;
        }, cancellationToken);
    }
}

internal sealed class TimedPsu : IPsu
{
    private readonly IPsu _inner;
    private readonly RetryPolicy _policy;
    private readonly TimeSpan _timeout;
    private readonly ILogger _logger;
    private readonly Guid _correlationId;
    private readonly int _slotId;

    public TimedPsu(IPsu inner, RetryPolicy policy, TimeSpan timeout, ILogger logger, Guid correlationId, int slotId)
    {
        _inner = inner;
        _policy = policy;
        _timeout = timeout;
        _logger = logger;
        _correlationId = correlationId;
        _slotId = slotId;
    }

    public string Name => _inner.Name;

    public Task InitializeAsync(CancellationToken cancellationToken)
    {
        return _inner.InitializeAsync(cancellationToken);
    }

    public Task SetOutputAsync(bool enabled, double voltage, double currentLimit, CancellationToken cancellationToken)
    {
        return _policy.ExecuteAsync(async token =>
        {
            Log.Write(_logger, _correlationId, _slotId, LogLevel.Debug, $"PSU set output enabled={enabled} V={voltage:F2} I={currentLimit:F2}.");
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeoutCts.CancelAfter(_timeout);
            await _inner.SetOutputAsync(enabled, voltage, currentLimit, timeoutCts.Token);
            return 0;
        }, cancellationToken);
    }
}

internal sealed class TimedCanBus : ICanBus
{
    private readonly ICanBus _inner;
    private readonly RetryPolicy _policy;
    private readonly TimeSpan _timeout;
    private readonly ILogger _logger;
    private readonly Guid _correlationId;
    private readonly int _slotId;

    public TimedCanBus(ICanBus inner, RetryPolicy policy, TimeSpan timeout, ILogger logger, Guid correlationId, int slotId)
    {
        _inner = inner;
        _policy = policy;
        _timeout = timeout;
        _logger = logger;
        _correlationId = correlationId;
        _slotId = slotId;
    }

    public string Name => _inner.Name;

    public Task InitializeAsync(CancellationToken cancellationToken)
    {
        return _inner.InitializeAsync(cancellationToken);
    }

    public Task<string> SendAsync(string payload, CancellationToken cancellationToken)
    {
        return _policy.ExecuteAsync(async token =>
        {
            Log.Write(_logger, _correlationId, _slotId, LogLevel.Debug, $"CAN send {payload}.");
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeoutCts.CancelAfter(_timeout);
            var response = await _inner.SendAsync(payload, timeoutCts.Token);
            Log.Write(_logger, _correlationId, _slotId, LogLevel.Debug, $"CAN response {response}.");
            return response;
        }, cancellationToken);
    }
}
