using Hospital.BLL.Common;
using Hospital.DAL.Entities.Pharmacy;

namespace Hospital.BLL.Services.Abstraction;

public sealed record DispenseLineRequest(int PrescriptionItemId, int Quantity);

public sealed record PharmacyQueueCounts(
    int Pending,
    int Reviewed,
    int PartiallyDispensed,
    int Dispensed,
    int LowStock,
    int ExpiringSoon);

public interface IPharmacyService
{
    Task<Result<Prescription>> CreateAsync(
        int encounterId, IReadOnlyCollection<int> medicationIds, string actorUserId,
        string dose, string frequency, string duration, string? route,
        CancellationToken cancellationToken = default);

    Task<Result> ReviewAsync(int prescriptionId, bool approve, string pharmacistUserId, string? note, CancellationToken cancellationToken = default);

    Task<Result<Prescription>> DispenseAsync(int prescriptionId, IReadOnlyCollection<DispenseLineRequest> lines, string pharmacistUserId, CancellationToken cancellationToken = default);

    Task<PharmacyQueueCounts> QueueCountsAsync(CancellationToken cancellationToken = default);
}
