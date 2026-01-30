using System.Text.Json;
using Platform.Contracts;

namespace Platform.Mes;

public sealed class OfflineMesClient : IMesClient
{
    private readonly string _basePath;
    private readonly SemaphoreSlim _mutex = new(1, 1);
    private readonly JsonSerializerOptions _options = new() { WriteIndented = true };

    public OfflineMesClient(string? basePath = null)
    {
        _basePath = string.IsNullOrWhiteSpace(basePath) ? "/mes_mock" : basePath;
        Directory.CreateDirectory(_basePath);
    }

    public async Task<UnitInfo?> GetUnitInfoAsync(string serial, CancellationToken cancellationToken = default)
    {
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            var units = await ReadListAsync<UnitInfo>(GetPath("units.json"), cancellationToken);
            return units.FirstOrDefault(unit => string.Equals(unit.Serial, serial, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task UploadResultsAsync(RunReport runReport, CancellationToken cancellationToken = default)
    {
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            var path = GetPath("runs.json");
            var runs = await ReadListAsync<RunReport>(path, cancellationToken);
            var updated = runs.ToList();
            updated.Add(runReport);
            await WriteListAsync(path, updated, cancellationToken);
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<string> ReserveSerialAsync(string? requestedSerial = null, CancellationToken cancellationToken = default)
    {
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            var path = GetPath("reservations.json");
            var state = await ReadStateAsync(path, cancellationToken);

            var serial = string.IsNullOrWhiteSpace(requestedSerial)
                ? $"OFFLINE-{state.NextSerial:D6}"
                : requestedSerial;

            state.NextSerial++;
            state.ReservedSerials.Add(serial);
            await WriteStateAsync(path, state, cancellationToken);

            return serial;
        }
        finally
        {
            _mutex.Release();
        }
    }

    private string GetPath(string fileName) => Path.Combine(_basePath, fileName);

    private async Task<IReadOnlyList<T>> ReadListAsync<T>(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return Array.Empty<T>();
        }

        var json = await File.ReadAllTextAsync(path, cancellationToken);
        return JsonSerializer.Deserialize<List<T>>(json, _options) ?? new List<T>();
    }

    private async Task WriteListAsync<T>(string path, List<T> data, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(data, _options);
        await File.WriteAllTextAsync(path, json, cancellationToken);
    }

    private async Task<ReservationState> ReadStateAsync(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return new ReservationState();
        }

        var json = await File.ReadAllTextAsync(path, cancellationToken);
        return JsonSerializer.Deserialize<ReservationState>(json, _options) ?? new ReservationState();
    }

    private async Task WriteStateAsync(string path, ReservationState state, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(state, _options);
        await File.WriteAllTextAsync(path, json, cancellationToken);
    }

    private sealed class ReservationState
    {
        public int NextSerial { get; set; } = 1;

        public List<string> ReservedSerials { get; } = new();
    }
}
