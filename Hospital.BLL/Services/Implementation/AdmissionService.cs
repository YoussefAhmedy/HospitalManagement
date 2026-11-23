using Hospital.BLL.Common;
using Hospital.BLL.Common.Rules;
using Hospital.BLL.Services.Abstraction;
using Hospital.DAL.DataBase;
using Hospital.DAL.Entities.Operations;
using Microsoft.EntityFrameworkCore;

namespace Hospital.BLL.Services.Implementation;

public sealed class AdmissionService(
    HospitalDbContext dbContext,
    INumberSequenceService numbers) : IAdmissionService
{
    public async Task<Result<Admission>> RequestAdmissionAsync(
        string patientId, string? admittingDoctorId, int? encounterId, string? reason,
        CancellationToken cancellationToken = default)
    {
        if (!await dbContext.Patients.AnyAsync(p => p.Id == patientId, cancellationToken))
        {
            return Result<Admission>.Fail("Patient not found.", "patient_not_found");
        }

        var admission = new Admission
        {
            Number = await numbers.NextFormattedAsync(ClinicalNumber.Admission, "admission", cancellationToken),
            PatientId = patientId,
            AdmittingDoctorId = admittingDoctorId,
            EncounterId = encounterId,
            Reason = reason?.Length > 500 ? reason[..500] : reason,
            Status = AdmissionStatus.Requested,
            RequestedAtUtc = DateTime.UtcNow
        };

        dbContext.Admissions.Add(admission);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<Admission>.Ok(admission);
    }

    public async Task<Result<Admission>> AdmitWithBedAsync(
        int admissionId, int bedId, string actorUserId,
        CancellationToken cancellationToken = default)
    {
        var admission = await dbContext.Admissions
            .Include(a => a.BedAssignments)
            .FirstOrDefaultAsync(a => a.Id == admissionId, cancellationToken);

        if (admission is null)
        {
            return Result<Admission>.Fail("Admission not found.", "not_found");
        }

        if (!BedRules.CanTransition(admission.Status, AdmissionStatus.Admitted) &&
            admission.Status != AdmissionStatus.Admitted)
        {
            return Result<Admission>.Fail($"An admission in state {admission.Status} cannot be admitted again.", "invalid_state");
        }

        var bed = await dbContext.Beds
            .Include(b => b.Room).ThenInclude(r => r!.Ward)
            .Include(b => b.Assignments)
            .FirstOrDefaultAsync(b => b.Id == bedId && b.IsActive, cancellationToken);

        if (bed is null)
        {
            return Result<Admission>.Fail("Bed not found or inactive.", "bed_not_found");
        }

        var alreadyOpen = await dbContext.BedAssignments
            .AsNoTracking()
            .AnyAsync(a => a.AdmissionId == admissionId && a.ReleasedAtUtc == null, cancellationToken);

        if (alreadyOpen)
        {
            // Transfer path: release the previous bed before occupying the new one.
            foreach (var assignment in admission.BedAssignments.Where(a => a.ReleasedAtUtc is null))
            {
                assignment.ReleasedAtUtc = DateTime.UtcNow;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }
        else if (BedRules.IsOccupied(bed.Assignments))
        {
            return Result<Admission>.Fail("That bed is already occupied. Choose another bed.", "bed_occupied");
        }

        if (admission.Status == AdmissionStatus.Requested)
        {
            admission.Status = AdmissionStatus.Admitted;
            admission.AdmittedAtUtc = DateTime.UtcNow;
        }

        dbContext.BedAssignments.Add(new BedAssignment
        {
            AdmissionId = admissionId,
            BedId = bedId,
            AssignedAtUtc = DateTime.UtcNow
        });

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception, cancellationToken))
        {
            // Two requests raced for the same bed; the database index is the final authority.
            return Result<Admission>.Fail("That bed was just assigned to another patient. Choose another bed.", "bed_occupied");
        }

        return Result<Admission>.Ok(admission);
    }

    public async Task<Result> TransferBedAsync(
        int admissionId, int newBedId, string actorUserId,
        CancellationToken cancellationToken = default)
    {
        var admission = await dbContext.Admissions
            .FirstOrDefaultAsync(a => a.Id == admissionId, cancellationToken);

        if (admission is null || admission.Status != AdmissionStatus.Admitted)
        {
            return Result.Fail("Only an admitted patient can be transferred.", "invalid_state");
        }

        var result = await AdmitWithBedAsync(admissionId, newBedId, actorUserId, cancellationToken);
        return result.Succeeded
            ? Result.Ok()
            : Result.Fail(result.Error ?? "Transfer failed.", result.Code);
    }

    public async Task<Result> DischargeAsync(
        int admissionId, string summary, string approvingUserId,
        CancellationToken cancellationToken = default)
    {
        var admission = await dbContext.Admissions
            .Include(a => a.BedAssignments)
            .FirstOrDefaultAsync(a => a.Id == admissionId, cancellationToken);

        if (admission is null)
        {
            return Result.Fail("Admission not found.", "not_found");
        }

        if (!BedRules.CanTransition(admission.Status, AdmissionStatus.Discharged))
        {
            return Result.Fail("Only an admitted patient can be discharged.", "invalid_state");
        }

        if (string.IsNullOrWhiteSpace(summary))
        {
            return Result.Fail("A discharge summary is required.", "summary_required");
        }

        admission.Status = AdmissionStatus.Discharged;
        admission.DischargedAtUtc = DateTime.UtcNow;
        admission.DischargeSummary = summary.Length > 2000 ? summary[..2000] : summary;
        admission.DischargeApprovedByUserId = approvingUserId;

        foreach (var assignment in admission.BedAssignments.Where(a => a.ReleasedAtUtc is null))
        {
            assignment.ReleasedAtUtc = DateTime.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }

    public async Task<Result> CancelAsync(int admissionId, CancellationToken cancellationToken = default)
    {
        var admission = await dbContext.Admissions.FirstOrDefaultAsync(a => a.Id == admissionId, cancellationToken);
        if (admission is null)
        {
            return Result.Fail("Admission not found.", "not_found");
        }

        if (!BedRules.CanTransition(admission.Status, AdmissionStatus.Cancelled))
        {
            return Result.Fail("An admitted patient must be discharged, not cancelled.", "invalid_state");
        }

        admission.Status = AdmissionStatus.Cancelled;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }

    public async Task<IReadOnlyList<BedBoardRow>> BedBoardAsync(CancellationToken cancellationToken = default)
    {
        var beds = await dbContext.Beds
            .AsNoTracking()
            .Include(b => b.Room).ThenInclude(r => r!.Ward)
            .Include(b => b.Assignments).ThenInclude(a => a.Admission).ThenInclude(a => a!.Patient)
            .Where(b => b.IsActive)
            .OrderBy(b => b.Room!.Ward!.Name).ThenBy(b => b.Room!.Number).ThenBy(b => b.Label)
            .ToListAsync(cancellationToken);

        return beds.Select(bed =>
        {
            var active = bed.Assignments.FirstOrDefault(a => a.ReleasedAtUtc is null);
            var patient = active?.Admission?.Patient;
            return new BedBoardRow(
                bed.Id,
                bed.Room?.Ward?.Name ?? "—",
                bed.Room?.Number ?? "—",
                bed.Label,
                active is not null,
                patient is null ? null : $"{patient.FirstName} {patient.LastName}",
                patient?.Mrn,
                active?.AdmissionId,
                active?.AssignedAtUtc);
        }).ToList();
    }

    public async Task<PagedResult<AdmissionListItem>> QueryAsync(
        AdmissionStatus? status, string? term, int page, int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Admissions.AsNoTracking().AsQueryable();

        if (status is not null)
        {
            query = query.Where(a => a.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(term))
        {
            var trimmed = term.Trim();
            query = query.Where(a =>
                a.Number.Contains(trimmed) ||
                (a.Patient != null && (a.Patient.FirstName.Contains(trimmed) || a.Patient.LastName.Contains(trimmed))) ||
                (a.Patient != null && a.Patient.Mrn != null && a.Patient.Mrn.Contains(trimmed)));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(a => a.RequestedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new AdmissionListItem(
                a.Id,
                a.Number,
                a.Patient!.Mrn ?? "—",
                a.Patient.FirstName + " " + a.Patient.LastName,
                a.BedAssignments
                    .Where(b => b.ReleasedAtUtc == null)
                    .Select(b => b.Bed!.Room!.Ward!.Name + " / " + b.Bed.Room.Number + " / " + b.Bed.Label)
                    .FirstOrDefault(),
                a.Status,
                a.RequestedAtUtc,
                a.AdmittedAtUtc,
                a.DischargedAtUtc))
            .ToListAsync(cancellationToken);

        return new PagedResult<AdmissionListItem>(items, total, page, pageSize);
    }

    public async Task<OccupancySummary> SummaryAsync(CancellationToken cancellationToken = default)
    {
        var beds = await dbContext.Beds.AsNoTracking()
            .Include(b => b.Assignments)
            .Where(b => b.IsActive)
            .ToListAsync(cancellationToken);

        var today = DateTime.UtcNow.Date;
        var pending = await dbContext.Admissions.AsNoTracking()
            .CountAsync(a => a.Status == AdmissionStatus.Requested, cancellationToken);
        var dischargesToday = await dbContext.Admissions.AsNoTracking()
            .CountAsync(a => a.Status == AdmissionStatus.Discharged && a.DischargedAtUtc >= today, cancellationToken);

        return new OccupancySummary(
            beds.Count,
            BedRules.OccupiedCount(beds),
            BedRules.OccupancyRate(beds),
            pending,
            dischargesToday);
    }

    public Task<OccupancySummary> SummarizeAsync(CancellationToken cancellationToken = default) =>
        SummaryAsync(cancellationToken);

    private static bool IsUniqueViolation(DbUpdateException exception, CancellationToken _) =>
        exception.InnerException is not null &&
        (exception.InnerException.Message.Contains("UX_BedAssignments_ActiveOccupancy", StringComparison.OrdinalIgnoreCase) ||
         exception.InnerException.Message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase));
}
