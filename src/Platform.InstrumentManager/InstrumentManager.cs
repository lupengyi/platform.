using System.Collections.Concurrent;
using System.Diagnostics;
using Platform.Automation;
using Platform.Contracts;
using Platform.Core;
using Platform.Instruments;
using Platform.Libraries;

namespace Platform.InstrumentManager;

public sealed record CircuitBreakerOptions(int FailureThreshold, TimeSpan OpenDuration);

public sealed record InstrumentPolicyOptions(RetryOptions Retry, CircuitBreakerOptions CircuitBreaker);

public sealed record HealthCheckOptions(bool Enabled, TimeSpan Interval, TimeSpan Timeout)
{
    public static HealthCheckOptions Disabled => new(false, TimeSpan.Zero, TimeSpan.Zero);
}

public sealed record MockInstrumentOptions(int CanTransientFailures, bool RequireReinitializeAfterFailure)
{
    public static MockInstrumentOptions Default => new(0, true);
}

public sealed class InstrumentManager : IInstrumentServices, IAsyncDisposable
{
    private readonly InstrumentWatchdog? _watchdog;

    public InstrumentManager(
        InstrumentTimeouts timeouts,
        InstrumentPolicyOptions policyOptions,
        ILogger logger,
        Guid correlationId,
        int slotId,
        int seed,
        HealthCheckOptions? healthCheckOptions = null,
        MockInstrumentOptions? mockInstrumentOptions = null)
    {
        var leaseManager = new InstrumentLeaseManager();
        var dmmPolicy = new InstrumentPolicy(policyOptions, timeouts.Dmm);
        var psuPolicy = new InstrumentPolicy(policyOptions, timeouts.Psu);
        var canPolicy = new InstrumentPolicy(policyOptions, timeouts.Can);
        var mockOptions = mockInstrumentOptions ?? MockInstrumentOptions.Default;

        var dmm = new MockDmm(seed + 1);
        var psu = new MockPsu(seed + 2);
        var can = new MockCanBus(seed + 3, mockOptions.CanTransientFailures, mockOptions.RequireReinitializeAfterFailure);

        Dmm = new TimedDmm(dmm, dmmPolicy, leaseManager, logger, correlationId, slotId);
        Psu = new TimedPsu(psu, psuPolicy, leaseManager, logger, correlationId, slotId);
        Can = new TimedCanBus(can, canPolicy, leaseManager, logger, correlationId, slotId);

        var healthOptions = healthCheckOptions ?? HealthCheckOptions.Disabled;
        if (healthOptions.Enabled)
        {
            var checks = new List<(string InstrumentId, IHealthCheck Check)>();
            if (Dmm is IHealthCheck dmmCheck)
            {
                checks.Add((Dmm.Name, dmmCheck));
            }
            if (Psu is IHealthCheck psuCheck)
            {
                checks.Add((Psu.Name, psuCheck));
            }
            if (Can is IHealthCheck canCheck)
            {
                checks.Add((Can.Name, canCheck));
            }

            _watchdog = new InstrumentWatchdog(healthOptions, checks, logger, correlationId, slotId);
        }
    }

    public IDmm Dmm { get; }
    public IPsu Psu { get; }
    public ICanBus Can { get; }

    public ValueTask DisposeAsync()
    {
        return _watchdog?.DisposeAsync() ?? ValueTask.CompletedTask;
    }
}

internal sealed class InstrumentPolicy
{
    private readonly RetryOptions _retryOptions;
    private readonly CircuitBreaker _breaker;
    private readonly TimeSpan _timeout;

    public InstrumentPolicy(InstrumentPolicyOptions options, TimeSpan timeout)
    {
        _retryOptions = options.Retry;
        _breaker = new CircuitBreaker(options.CircuitBreaker);
        _timeout = timeout;
    }

    public async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken,
        Func<Exception, CancellationToken, Task>? onFailure = null)
    {
        Exception? lastException = null;
        for (var attempt = 1; attempt <= _retryOptions.MaxAttempts; attempt++)
        {
            if (!_breaker.AllowRequest())
            {
                throw new InvalidOperationException("Circuit breaker open; instrument call blocked.");
            }

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(_timeout);

            try
            {
                var result = await action(timeoutCts.Token);
                _breaker.RecordSuccess();
                return result;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                var timeoutException = new TimeoutException($"Instrument operation exceeded timeout of {_timeout.TotalMilliseconds:F0} ms.");
                lastException = timeoutException;
                _breaker.RecordFailure();
                if (onFailure is not null)
                {
                    await onFailure(timeoutException, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                lastException = ex;
                _breaker.RecordFailure();
                if (onFailure is not null)
                {
                    await onFailure(ex, cancellationToken);
                }
            }

            if (attempt == _retryOptions.MaxAttempts)
            {
                break;
            }

            var delay = _retryOptions.ExponentialBackoff
                ? TimeSpan.FromMilliseconds(_retryOptions.Delay.TotalMilliseconds * Math.Pow(2, attempt - 1))
                : _retryOptions.Delay;
            await Task.Delay(delay, cancellationToken);
        }

        throw new InvalidOperationException("Instrument policy exhausted.", lastException);
    }
}

internal sealed class CircuitBreaker
{
    private readonly CircuitBreakerOptions _options;
    private int _failureCount;
    private DateTimeOffset? _openedAt;

    public CircuitBreaker(CircuitBreakerOptions options)
    {
        _options = options;
    }

    public bool AllowRequest()
    {
        if (_openedAt is null)
        {
            return true;
        }

        if (DateTimeOffset.UtcNow - _openedAt < _options.OpenDuration)
        {
            return false;
        }

        _openedAt = null;
        _failureCount = 0;
        return true;
    }

    public void RecordSuccess()
    {
        _failureCount = 0;
        _openedAt = null;
    }

    public void RecordFailure()
    {
        _failureCount++;
        if (_failureCount >= _options.FailureThreshold)
        {
            _openedAt = DateTimeOffset.UtcNow;
        }
    }
}

internal sealed class InstrumentLeaseManager
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _leases = new(StringComparer.OrdinalIgnoreCase);

    public async Task<InstrumentLease> AcquireAsync(string instrumentId, CancellationToken cancellationToken)
    {
        var gate = _leases.GetOrAdd(instrumentId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        return new InstrumentLease(gate);
    }
}

internal sealed class InstrumentLease : IAsyncDisposable
{
    private readonly SemaphoreSlim _gate;

    public InstrumentLease(SemaphoreSlim gate)
    {
        _gate = gate;
    }

    public ValueTask DisposeAsync()
    {
        _gate.Release();
        return ValueTask.CompletedTask;
    }
}

internal sealed class InstrumentOperationExecutor
{
    private readonly string _instrumentId;
    private readonly InstrumentPolicy _policy;
    private readonly InstrumentLeaseManager _leaseManager;
    private readonly ILogger _logger;
    private readonly Guid _correlationId;
    private readonly int _slotId;

    public InstrumentOperationExecutor(
        string instrumentId,
        InstrumentPolicy policy,
        InstrumentLeaseManager leaseManager,
        ILogger logger,
        Guid correlationId,
        int slotId)
    {
        _instrumentId = instrumentId;
        _policy = policy;
        _leaseManager = leaseManager;
        _logger = logger;
        _correlationId = correlationId;
        _slotId = slotId;
    }

    public async Task<T> ExecuteAsync<T>(
        string command,
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken,
        Func<Exception, CancellationToken, Task>? onFailure = null)
    {
        await using var lease = await _leaseManager.AcquireAsync(_instrumentId, cancellationToken);
        return await ExecuteWithLeaseAsync(command, action, cancellationToken, onFailure);
    }

    public async Task<T> ExecuteWithLeaseAsync<T>(
        string command,
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken,
        Func<Exception, CancellationToken, Task>? onFailure = null)
    {
        var stopwatch = Stopwatch.StartNew();
        Exception? failure = null;
        string response = "<none>";
        try
        {
            var result = await _policy.ExecuteAsync(action, cancellationToken, onFailure);
            response = result is null ? "<null>" : result.ToString() ?? "<null>";
            return result;
        }
        catch (Exception ex)
        {
            failure = ex;
            response = ex.Message;
            throw;
        }
        finally
        {
            stopwatch.Stop();
            var level = failure is null ? LogLevel.Info : LogLevel.Error;
            var message = $"IO instrument={_instrumentId} command={command} response={response} durationMs={stopwatch.Elapsed.TotalMilliseconds:F0} slot={_slotId}";
            Log.Write(_logger, _correlationId, _slotId, level, message);
        }
    }

    public Task ExecuteAsync(
        string command,
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken,
        Func<Exception, CancellationToken, Task>? onFailure = null)
    {
        return ExecuteAsync(command, async token =>
        {
            await action(token);
            return "OK";
        }, cancellationToken, onFailure);
    }

    public Task ExecuteWithLeaseAsync(
        string command,
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken,
        Func<Exception, CancellationToken, Task>? onFailure = null)
    {
        return ExecuteWithLeaseAsync(command, async token =>
        {
            await action(token);
            return "OK";
        }, cancellationToken, onFailure);
    }
}

internal sealed class InstrumentWatchdog : IAsyncDisposable
{
    private readonly HealthCheckOptions _options;
    private readonly IReadOnlyList<(string InstrumentId, IHealthCheck Check)> _checks;
    private readonly ILogger _logger;
    private readonly Guid _correlationId;
    private readonly int _slotId;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _loop;

    public InstrumentWatchdog(
        HealthCheckOptions options,
        IReadOnlyList<(string InstrumentId, IHealthCheck Check)> checks,
        ILogger logger,
        Guid correlationId,
        int slotId)
    {
        _options = options;
        _checks = checks;
        _logger = logger;
        _correlationId = correlationId;
        _slotId = slotId;
        _loop = Task.Run(() => RunAsync(_cts.Token), _cts.Token);
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            foreach (var (instrumentId, check) in _checks)
            {
                try
                {
                    using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    timeoutCts.CancelAfter(_options.Timeout);
                    var result = await check.CheckHealthAsync(timeoutCts.Token);
                    var level = result.Healthy ? LogLevel.Debug : LogLevel.Warning;
                    Log.Write(_logger, _correlationId, _slotId, level, $"HealthCheck instrument={instrumentId} healthy={result.Healthy} details={result.Details} slot={_slotId}");
                }
                catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
                {
                    Log.Write(_logger, _correlationId, _slotId, LogLevel.Warning, $"HealthCheck instrument={instrumentId} exception={ex.Message} slot={_slotId}");
                }
            }

            try
            {
                await Task.Delay(_options.Interval, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync();
        try
        {
            await _loop;
        }
        catch (OperationCanceledException)
        {
            // Ignored.
        }
        _cts.Dispose();
    }
}

internal sealed class TimedDmm : IDmm, IHealthCheck
{
    private readonly IDmm _inner;
    private readonly InstrumentOperationExecutor _executor;

    public TimedDmm(IDmm inner, InstrumentPolicy policy, InstrumentLeaseManager leaseManager, ILogger logger, Guid correlationId, int slotId)
    {
        _inner = inner;
        _executor = new InstrumentOperationExecutor(inner.Name, policy, leaseManager, logger, correlationId, slotId);
    }

    public string Name => _inner.Name;

    public Task InitializeAsync(CancellationToken cancellationToken)
    {
        return _executor.ExecuteAsync("DMM.Initialize", _inner.InitializeAsync, cancellationToken);
    }

    public Task<double> MeasureVoltageAsync(CancellationToken cancellationToken)
    {
        return _executor.ExecuteAsync("DMM.MeasureVoltage", _inner.MeasureVoltageAsync, cancellationToken);
    }

    public Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken)
    {
        if (_inner is IHealthCheck check)
        {
            return check.CheckHealthAsync(cancellationToken);
        }

        return Task.FromResult(new HealthCheckResult(true, "OK"));
    }
}

internal sealed class TimedPsu : IPsu, IHealthCheck
{
    private readonly IPsu _inner;
    private readonly InstrumentOperationExecutor _executor;

    public TimedPsu(IPsu inner, InstrumentPolicy policy, InstrumentLeaseManager leaseManager, ILogger logger, Guid correlationId, int slotId)
    {
        _inner = inner;
        _executor = new InstrumentOperationExecutor(inner.Name, policy, leaseManager, logger, correlationId, slotId);
    }

    public string Name => _inner.Name;

    public Task InitializeAsync(CancellationToken cancellationToken)
    {
        return _executor.ExecuteAsync("PSU.Initialize", _inner.InitializeAsync, cancellationToken);
    }

    public Task SetOutputAsync(bool enabled, double voltage, double currentLimit, CancellationToken cancellationToken)
    {
        var command = $"PSU.SetOutput enabled={enabled} voltage={voltage:F2} currentLimit={currentLimit:F2}";
        return _executor.ExecuteAsync(command, token => _inner.SetOutputAsync(enabled, voltage, currentLimit, token), cancellationToken);
    }

    public Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken)
    {
        if (_inner is IHealthCheck check)
        {
            return check.CheckHealthAsync(cancellationToken);
        }

        return Task.FromResult(new HealthCheckResult(true, "OK"));
    }
}

internal sealed class TimedCanBus : ICanBus, IHealthCheck
{
    private readonly ICanBus _inner;
    private readonly InstrumentOperationExecutor _executor;

    public TimedCanBus(ICanBus inner, InstrumentPolicy policy, InstrumentLeaseManager leaseManager, ILogger logger, Guid correlationId, int slotId)
    {
        _inner = inner;
        _executor = new InstrumentOperationExecutor(inner.Name, policy, leaseManager, logger, correlationId, slotId);
    }

    public string Name => _inner.Name;

    public Task InitializeAsync(CancellationToken cancellationToken)
    {
        return _executor.ExecuteAsync("CAN.Initialize", _inner.InitializeAsync, cancellationToken);
    }

    public Task<string> SendAsync(string payload, CancellationToken cancellationToken)
    {
        var command = $"CAN.Send payload={payload}";
        return _executor.ExecuteAsync(command,
            token => _inner.SendAsync(payload, token),
            cancellationToken,
            onFailure: (_, token) => _executor.ExecuteWithLeaseAsync("CAN.Reinitialize", _inner.ReinitializeAsync, token));
    }

    public Task ReinitializeAsync(CancellationToken cancellationToken)
    {
        return _executor.ExecuteAsync("CAN.Reinitialize", _inner.ReinitializeAsync, cancellationToken);
    }

    public Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken)
    {
        if (_inner is IHealthCheck check)
        {
            return check.CheckHealthAsync(cancellationToken);
        }

        return Task.FromResult(new HealthCheckResult(true, "OK"));
    }
}
