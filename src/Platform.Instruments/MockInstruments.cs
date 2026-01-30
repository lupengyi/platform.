using Platform.Contracts;

namespace Platform.Instruments;

public abstract class MockInstrumentBase : IInstrument, IHealthCheck
{
    protected readonly Random Random;

    protected MockInstrumentBase(string name, int seed)
    {
        Name = name;
        Random = new Random(seed);
    }

    public string Name { get; }

    public virtual Task InitializeAsync(CancellationToken cancellationToken)
    {
        return Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);
    }

    public virtual Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(new HealthCheckResult(true, "OK"));
    }
}

public sealed class MockDmm : MockInstrumentBase, IDmm
{
    public MockDmm(int seed) : base("MockDmm", seed) { }

    public async Task<double> MeasureVoltageAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(120), cancellationToken);
        return 3.0 + Random.NextDouble() * 0.2;
    }
}

public sealed class MockPsu : MockInstrumentBase, IPsu
{
    public MockPsu(int seed) : base("MockPsu", seed) { }

    public async Task SetOutputAsync(bool enabled, double voltage, double currentLimit, CancellationToken cancellationToken)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
    }
}

public sealed class MockCanBus : MockInstrumentBase, ICanBus
{
    private int _transientFailuresRemaining;
    private readonly bool _requireReinitializeAfterFailure;
    private bool _requiresReinitialize;

    public MockCanBus(int seed, int transientFailures = 0, bool requireReinitializeAfterFailure = true) : base("MockCan", seed)
    {
        _transientFailuresRemaining = transientFailures;
        _requireReinitializeAfterFailure = requireReinitializeAfterFailure;
    }

    public async Task<string> SendAsync(string payload, CancellationToken cancellationToken)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(80), cancellationToken);
        if (_requiresReinitialize)
        {
            throw new InvalidOperationException("CAN controller requires reinitialization.");
        }
        if (_transientFailuresRemaining > 0)
        {
            _transientFailuresRemaining--;
            if (_requireReinitializeAfterFailure)
            {
                _requiresReinitialize = true;
            }
            throw new TimeoutException("CAN card transient failure.");
        }
        return $"ACK:{payload}";
    }

    public async Task ReinitializeAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(60), cancellationToken);
        _requiresReinitialize = false;
    }

    public override Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken)
    {
        var healthy = !_requiresReinitialize;
        var details = healthy ? "OK" : "Reinit required";
        return Task.FromResult(new HealthCheckResult(healthy, details));
    }
}
