using IndustrialTestPlatform.Core;
using Xunit;

namespace IndustrialTestPlatform.Tests;

public sealed class RetryPolicyTests
{
    [Fact]
    public async Task ExecuteAsync_RetriesTransientThenSucceeds()
    {
        var options = new RetryOptions { MaxAttempts = 3, DelayMs = 1 };
        var policy = new RetryPolicy(options);
        var attempts = 0;

        var result = await policy.ExecuteAsync(async () =>
        {
            attempts++;
            await Task.Delay(1);
            if (attempts < 3)
            {
                throw new InvalidOperationException("transient");
            }

            return "ok";
        }, ex => ex.Message == "transient", CancellationToken.None);

        Assert.Equal("ok", result);
        Assert.Equal(3, attempts);
    }
}
