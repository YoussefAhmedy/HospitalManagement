using Hospital.BLL.Common;
using Hospital.DAL.Entities.Diagnostics;

namespace Hospital.BLL.Services.Abstraction;

public sealed record LabQueueCounts(
    int AwaitingCollection,
    int Received,
    int Processing,
    int AwaitingVerification,
    int Released,
    int CriticalToday);

/// <summary>
/// Laboratory workflow. State transitions are validated by <c>LabRules</c>; a result is
/// never presented as final before it is released by an authorised professional.
/// </summary>
public interface ILabService
{
    Task<Result<LabOrder>> CreateOrderAsync(
        int encounterId, IReadOnlyCollection<int> labTestIds, string actorUserId,
        string? clinicalNotes, CancellationToken cancellationToken = default);

    Task<Result<LabOrder>> TransitionAsync(
        int orderId, LabOrderStatus target, string actorUserId,
        string? specimenId, CancellationToken cancellationToken = default);

    Task<Result<LabOrderItem>> EnterResultAsync(
        int orderItemId, string? value, decimal? numericValue,
        string actorUserId, string? comment,
        CancellationToken cancellationToken = default);

    Task<LabOrder?> GetAsync(int orderId, CancellationToken cancellationToken = default);

    Task<PagedResult<LabOrder>> QueryAsync(
        LabOrderStatus? status, string? patientId, DateRange range,
        string? term, int page, int pageSize,
        CancellationToken cancellationToken = default);

    Task<LabQueueCounts> QueueCountsAsync(CancellationToken cancellationToken = default);
}
