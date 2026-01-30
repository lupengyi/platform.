using System.Threading;
using System.Threading.Tasks;

namespace Platform.Contracts;

public interface IMesClient
{
    Task<UnitInfo?> GetUnitInfoAsync(string serial, CancellationToken cancellationToken = default);
    Task UploadResultsAsync(RunReport runReport, CancellationToken cancellationToken = default);
    Task<string> ReserveSerialAsync(string? requestedSerial = null, CancellationToken cancellationToken = default);
}
