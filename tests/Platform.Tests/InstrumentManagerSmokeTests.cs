using Platform.Automation;
using Platform.Core;
using Platform.InstrumentManager;
using Platform.Libraries;
using Xunit;

namespace Platform.Tests;

public sealed class InstrumentManagerSmokeTests
{
    [Fact]
    public async Task InstrumentManager_RecoversFromTransientCanFailure()
    {
        var logger = new BufferingLogger();
        var policyOptions = new InstrumentPolicyOptions(
            new RetryOptions(3, TimeSpan.FromMilliseconds(10), false),
            new CircuitBreakerOptions(2, TimeSpan.FromMilliseconds(200)));
        var timeouts = new InstrumentTimeouts(
            TimeSpan.FromMilliseconds(300),
            TimeSpan.FromMilliseconds(300),
            TimeSpan.FromMilliseconds(300));
        var mockOptions = new MockInstrumentOptions(CanTransientFailures: 1, RequireReinitializeAfterFailure: true);

        await using var instruments = new InstrumentManager.InstrumentManager(
            timeouts,
            policyOptions,
            logger,
            Guid.NewGuid(),
            slotId: 1,
            seed: 123,
            HealthCheckOptions.Disabled,
            mockOptions);

        await instruments.Dmm.InitializeAsync(CancellationToken.None);
        await instruments.Psu.InitializeAsync(CancellationToken.None);
        await instruments.Can.InitializeAsync(CancellationToken.None);

        var voltage = await instruments.Dmm.MeasureVoltageAsync(CancellationToken.None);
        await instruments.Psu.SetOutputAsync(true, 3.3, 0.5, CancellationToken.None);
        var response = await instruments.Can.SendAsync("PING", CancellationToken.None);

        Assert.InRange(voltage, 3.0, 3.2);
        Assert.StartsWith("ACK:", response);
        Assert.Contains(logger.Entries, entry => entry.Message.Contains("CAN.Send", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(logger.Entries, entry => entry.Message.Contains("Reinitialize", StringComparison.OrdinalIgnoreCase));
    }
}
