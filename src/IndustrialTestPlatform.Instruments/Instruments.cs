using IndustrialTestPlatform.Contracts;

namespace IndustrialTestPlatform.Instruments;

public interface IPowerSupply
{
    Task PowerOnAsync(CancellationToken cancellationToken);
    Task PowerOffAsync(CancellationToken cancellationToken);
}

public interface ICanBus
{
    Task ConnectAsync(CancellationToken cancellationToken);
    Task DisconnectAsync(CancellationToken cancellationToken);
}

public interface IDmm
{
    Task<Measurement> MeasureVoltageAsync(string name, CancellationToken cancellationToken);
}

public sealed class MockPowerSupply : IPowerSupply
{
    public Task PowerOnAsync(CancellationToken cancellationToken) => Task.Delay(150, cancellationToken);
    public Task PowerOffAsync(CancellationToken cancellationToken) => Task.Delay(100, cancellationToken);
}

public sealed class MockCanBus : ICanBus
{
    public Task ConnectAsync(CancellationToken cancellationToken) => Task.Delay(120, cancellationToken);
    public Task DisconnectAsync(CancellationToken cancellationToken) => Task.Delay(80, cancellationToken);
}

public sealed class MockDmm : IDmm
{
    private readonly Random _random = new();

    public Task<Measurement> MeasureVoltageAsync(string name, CancellationToken cancellationToken)
    {
        var value = 11.8 + _random.NextDouble() * 0.6;
        return Task.FromResult(new Measurement(name, value, MeasurementUnit.Volt));
    }
}
