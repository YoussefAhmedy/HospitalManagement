using Hospital.BLL.Common;
using Hospital.BLL.Services.Abstraction;
using Hospital.DAL.DataBase;
using Microsoft.EntityFrameworkCore;

namespace Hospital.BLL.Services.Implementation;

public sealed class PatientLookupService(HospitalDbContext dbContext) : IPatientLookupService
{
    public async Task<PagedResult<PatientLookupRow>> SearchAsync(
        string? term,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Patients.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(term))
        {
            var trimmed = term.Trim();
            query = query.Where(p =>
                (p.Mrn != null && p.Mrn.Contains(trimmed)) ||
                p.FirstName.Contains(trimmed) ||
                p.LastName.Contains(trimmed) ||
                (p.PhoneNumber != null && p.PhoneNumber.Contains(trimmed)) ||
                (p.Email != null && p.Email.Contains(trimmed)));
        }

        var total = await query.CountAsync(cancellationToken);

        var rows = await query
            .OrderBy(p => p.LastName).ThenBy(p => p.FirstName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new
            {
                p.Id,
                p.Mrn,
                p.FirstName,
                p.LastName,
                p.PhoneNumber,
                p.DateOfBirth,
                Encounters = dbContext.Encounters.Count(e => e.PatientId == p.Id),
                LastVisit = dbContext.Encounters
                    .Where(e => e.PatientId == p.Id)
                    .Max(e => (DateTime?)e.RegisteredAtUtc)
            })
            .ToListAsync(cancellationToken);

        var mapped = rows.Select(p => new PatientLookupRow(
            p.Id,
            p.Mrn ?? "—",
            p.FirstName + " " + p.LastName,
            p.PhoneNumber,
            p.DateOfBirth is null ? null : DateOnly.FromDateTime(p.DateOfBirth.Value),
            p.Encounters,
            p.LastVisit)).ToList();

        return new PagedResult<PatientLookupRow>(mapped, total, page, pageSize);
    }

    /// <summary>
    /// Conservative duplicate guard: the same full name plus (date of birth or phone).
    /// Deliberately does not block on name alone, which produces false positives for
    /// common names.
    /// </summary>
    public async Task<bool> HasSimilarAsync(
        string firstName,
        string lastName,
        DateOnly? dateOfBirth,
        string? phone,
        string? excludeId = null,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Patients.AsNoTracking()
            .Where(p => p.FirstName == firstName && p.LastName == lastName);

        if (excludeId is not null)
        {
            query = query.Where(p => p.Id != excludeId);
        }

        var candidates = await query
            .Select(p => new { p.DateOfBirth, p.PhoneNumber })
            .ToListAsync(cancellationToken);

        foreach (var candidate in candidates)
        {
            var dobMatch = dateOfBirth is not null
                && candidate.DateOfBirth is not null
                && DateOnly.FromDateTime(candidate.DateOfBirth.Value) == dateOfBirth;

            var phoneMatch = !string.IsNullOrWhiteSpace(phone)
                && !string.IsNullOrWhiteSpace(candidate.PhoneNumber)
                && candidate.PhoneNumber.Trim() == phone.Trim();

            if (dobMatch || phoneMatch)
            {
                return true;
            }
        }

        return false;
    }
}
