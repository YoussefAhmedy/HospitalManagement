using Hospital.BLL.Common;

namespace Hospital.BLL.Services.Abstraction;

public sealed record PatientLookupRow(
    string Id,
    string Mrn,
    string Name,
    string? Phone,
    DateOnly? DateOfBirth,
    int Encounters,
    DateTime? LastVisitUtc);

/// <summary>Read-optimised patient directory for search, paging and duplicate detection.</summary>
public interface IPatientLookupService
{
    Task<PagedResult<PatientLookupRow>> SearchAsync(string? term, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<bool> HasSimilarAsync(string firstName, string lastName, DateOnly? dateOfBirth, string? phone, string? excludeId = null, CancellationToken cancellationToken = default);
}
