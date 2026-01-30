using Platform.Contracts;

namespace Platform.Instruments;

public abstract class MockInstrumentBase : IInstrument
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
    public MockCanBus(int seed) : base("MockCan", seed) { }

    public async Task<string> SendAsync(string payload, CancellationToken cancellationToken)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(80), cancellationToken);
        return $"ACK:{payload}";
    }
}
