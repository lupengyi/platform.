namespace IndustrialTestPlatform.Core;

public sealed class RetryPolicy
{
    private readonly RetryOptions _options;

    public RetryPolicy(RetryOptions options)
    {
        _options = options;
    }

    public async Task<T> ExecuteAsync<T>(Func<Task<T>> action, Func<Exception, bool> isTransient, CancellationToken cancellationToken)
    {
        var attempts = 0;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return await action().ConfigureAwait(false);
            }
            catch (Exception ex) when (isTransient(ex))
            {
                attempts++;
                if (attempts >= _options.MaxAttempts)
                {
                    throw;
                }

                if (_options.DelayMs > 0)
                {
                    await Task.Delay(_options.DelayMs, cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }

    public Task ExecuteAsync(Func<Task> action, Func<Exception, bool> isTransient, CancellationToken cancellationToken)
    {
        return ExecuteAsync(async () =>
        {
            await action().ConfigureAwait(false);
            return true;
        }, isTransient, cancellationToken);
    }
}
