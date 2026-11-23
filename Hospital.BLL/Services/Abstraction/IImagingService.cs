using Hospital.BLL.Common;
using Hospital.DAL.Entities.Diagnostics;

namespace Hospital.BLL.Services.Abstraction;

public sealed record ImagingWorklistItem(
    int Id, string Number, string Mrn, string PatientName, string Modality, string StudyName,
    ImagingOrderStatus Status, string? Radiologist, DateTime RequestedAtUtc, DateTime? PerformedAtUtc);

/// <summary>
/// Imaging requests and radiologist reporting. There is no fabricated image or finding:
/// a study only becomes clinically usable once a radiologist verifies and releases it.
/// </summary>
public interface IImagingService
{
    Task<Result<ImagingOrder>> RequestAsync(
        int encounterId, string modality, string studyName, string? indication, string actorUserId,
        CancellationToken cancellationToken = default);

    Task<Result<ImagingOrder>> GetAsync(int id, CancellationToken cancellationToken = default);

    Task<PagedResult<ImagingWorklistItem>> QueryAsync(
        ImagingOrderStatus? status, string? term, int page, int pageSize,
        CancellationToken cancellationToken = default);

    Task<Result> ScheduleAsync(int orderId, DateTime scheduledForUtc, string actorUserId, CancellationToken cancellationToken = default);

    Task<Result> MarkPerformedAsync(int orderId, string actorUserId, CancellationToken cancellationToken = default);

    /// <summary>Records the radiologist's own report text. Nothing is generated on their behalf.</summary>
    Task<Result> SaveReportAsync(int orderId, string findings, string impression, string radiologistUserId, CancellationToken cancellationToken = default);

    Task<Result> VerifyAsync(int orderId, string radiologistUserId, CancellationToken cancellationToken = default);

    Task<Result> ReleaseAsync(int orderId, string actorUserId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ImagingOrder>> ForPatientAsync(string patientId, CancellationToken cancellationToken = default);
}
