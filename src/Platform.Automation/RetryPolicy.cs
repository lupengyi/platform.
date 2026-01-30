using Platform.Core;

namespace Platform.Automation;

public sealed record RetryOptions(int MaxAttempts, TimeSpan Delay, bool ExponentialBackoff);

public sealed class RetryPolicy
{
    private readonly RetryOptions _options;
    private readonly ILogger _logger;
    private readonly Guid _correlationId;
    private readonly int _slotId;

    public RetryPolicy(RetryOptions options, ILogger logger, Guid correlationId, int slotId)
    {
        _options = options;
        _logger = logger;
        _correlationId = correlationId;
        _slotId = slotId;
    }

    public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken)
    {
        Exception? lastException = null;
        for (var attempt = 1; attempt <= _options.MaxAttempts; attempt++)
        {
            try
            {
                return await action(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                lastException = ex;
                Log.Write(_logger, _correlationId, _slotId, LogLevel.Warning, $"Attempt {attempt} failed: {ex.Message}");
                if (attempt == _options.MaxAttempts)
                {
                    break;
                }
                var delay = _options.ExponentialBackoff
                    ? TimeSpan.FromMilliseconds(_options.Delay.TotalMilliseconds * Math.Pow(2, attempt - 1))
                    : _options.Delay;
                await Task.Delay(delay, cancellationToken);
            }
        }

        throw new InvalidOperationException("Retry policy exhausted.", lastException);
    }
}
