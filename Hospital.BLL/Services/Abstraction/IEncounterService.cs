using Hospital.BLL.Common;
using Hospital.DAL.Entities.Clinical;

namespace Hospital.BLL.Services.Abstraction;

public sealed record EncounterListItem(
    int Id, string Number, string PatientId, string Mrn, string PatientName,
    string? DoctorName, string? Department, EncounterType Type,
    EncounterStatus Status, DateTime RegisteredAtUtc);

public sealed record EncounterQueueCounts(int Waiting, int Triaged, int InConsultation, int CompletedToday);

/// <summary>Encounter lifecycle: register → triage/vitals → consult → complete → sign.</summary>
public interface IEncounterService
{
    Task<Result<Encounter>> RegisterAsync(
        string patientId, string? doctorId, int? departmentId,
        EncounterType type, string? chiefComplaint, string actorUserId,
        CancellationToken cancellationToken = default);

    Task<Result<VitalSign>> RecordVitalsAsync(
        int encounterId, VitalSign vitals, string actorUserId,
        CancellationToken cancellationToken = default);

    Task<Result> StartConsultationAsync(int encounterId, string? doctorId, string actorUserId, CancellationToken cancellationToken = default);

    Task<Result<ClinicalNote>> AddNoteAsync(
        int encounterId, string content, string authorUserId, string? role,
        int? amendsNoteId, CancellationToken cancellationToken = default);

    Task<Result> SignNoteAsync(int noteId, string actorUserId, CancellationToken cancellationToken = default);

    Task<Result<Diagnosis>> AddDiagnosisAsync(
        int encounterId, string description, DiagnosisType type, string? code,
        string actorUserId, CancellationToken cancellationToken = default);

    Task<Result> CompleteAsync(int encounterId, string actorUserId, CancellationToken cancellationToken = default);

    Task<Result> SignEncounterAsync(int encounterId, string actorUserId, CancellationToken cancellationToken = default);

    Task<Encounter?> GetAsync(int id, CancellationToken cancellationToken = default);

    Task<PagedResult<EncounterListItem>> QueryAsync(
        EncounterStatus? status, string? patientId, string? doctorId,
        DateRange range, string? term, int page, int pageSize,
        CancellationToken cancellationToken = default);

    Task<EncounterQueueCounts> QueueCountsAsync(CancellationToken cancellationToken = default);
}
