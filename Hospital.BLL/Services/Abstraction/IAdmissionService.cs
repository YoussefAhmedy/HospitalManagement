using Hospital.BLL.Common;
using Hospital.DAL.Entities.Operations;

namespace Hospital.BLL.Services.Abstraction;

public sealed record BedBoardRow(
    int BedId, string Ward, string Room, string Bed,
    bool IsOccupied, string? PatientName, string? Mrn, int? AdmissionId, DateTime? SinceUtc);

public sealed record AdmissionListItem(
    int Id, string Number, string Mrn, string PatientName, string? WardRoomBed,
    AdmissionStatus Status, DateTime RequestedAtUtc, DateTime? AdmittedAtUtc, DateTime? DischargedAtUtc);

public sealed record OccupancySummary(int TotalBeds, int Occupied, decimal OccupancyPercent, int PendingAdmissions, int DischargesToday);

/// <summary>
/// Admissions and bed management. Bed occupancy is protected by a filtered unique index in
/// the database; the service maps that violation to an explicit business failure.
/// </summary>
public interface IAdmissionService
{
    Task<Result<Admission>> RequestAdmissionAsync(
        string patientId, string? admittingDoctorId, int? encounterId, string? reason,
        CancellationToken cancellationToken = default);

    Task<Result<Admission>> AdmitWithBedAsync(
        int admissionId, int bedId, string actorUserId,
        CancellationToken cancellationToken = default);

    Task<Result> TransferBedAsync(int admissionId, int newBedId, string actorUserId, CancellationToken cancellationToken = default);

    Task<Result> DischargeAsync(int admissionId, string summary, string approvingUserId, CancellationToken cancellationToken = default);

    Task<Result> CancelAsync(int admissionId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BedBoardRow>> BedBoardAsync(CancellationToken cancellationToken = default);

    Task<PagedResult<AdmissionListItem>> QueryAsync(AdmissionStatus? status, string? term, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<OccupancySummary> SummaryAsync(CancellationToken cancellationToken = default);

    /// <summary>Compact headline figures for the admissions workspace.</summary>
    Task<OccupancySummary> SummarizeAsync(CancellationToken cancellationToken = default);
}
