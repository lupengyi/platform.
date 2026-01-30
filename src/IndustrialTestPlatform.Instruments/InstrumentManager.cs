namespace IndustrialTestPlatform.Instruments;

public sealed class InstrumentLease : IAsyncDisposable
{
    private readonly SemaphoreSlim _semaphore;

    public InstrumentLease(int slotId, IPowerSupply powerSupply, ICanBus canBus, IDmm dmm, SemaphoreSlim semaphore)
    {
        SlotId = slotId;
        PowerSupply = powerSupply;
        CanBus = canBus;
        Dmm = dmm;
        _semaphore = semaphore;
    }

    public int SlotId { get; }
    public IPowerSupply PowerSupply { get; }
    public ICanBus CanBus { get; }
    public IDmm Dmm { get; }

    public ValueTask DisposeAsync()
    {
        _semaphore.Release();
        return ValueTask.CompletedTask;
    }
}

public sealed class InstrumentManager
{
    private sealed record SlotInstruments(IPowerSupply PowerSupply, ICanBus CanBus, IDmm Dmm, SemaphoreSlim Semaphore);

    private readonly Dictionary<int, SlotInstruments> _slots = new();

    public InstrumentManager(IEnumerable<int> slotIds)
    {
        foreach (var slotId in slotIds)
        {
            _slots[slotId] = new SlotInstruments(new MockPowerSupply(), new MockCanBus(), new MockDmm(), new SemaphoreSlim(1, 1));
        }
    }

    public async Task<InstrumentLease> AcquireAsync(int slotId, CancellationToken cancellationToken)
    {
        if (!_slots.TryGetValue(slotId, out var slot))
        {
            throw new InvalidOperationException($"Slot {slotId} not configured.");
        }

        await slot.Semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        return new InstrumentLease(slotId, slot.PowerSupply, slot.CanBus, slot.Dmm, slot.Semaphore);
    }
}
