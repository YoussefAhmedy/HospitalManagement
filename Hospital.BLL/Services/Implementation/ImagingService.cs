using Hospital.BLL.Common;
using Hospital.BLL.Services.Abstraction;
using Hospital.DAL.DataBase;
using Hospital.DAL.Entities.Diagnostics;
using Microsoft.EntityFrameworkCore;

namespace Hospital.BLL.Services.Implementation;

/// <summary>
/// Imaging workflow. Requests, scheduling, performance and radiologist reporting are distinct
/// audited steps; report text is always authored by a radiologist, never generated.
/// </summary>
public sealed class ImagingService(
    HospitalDbContext dbContext,
    INumberSequenceService numbers) : IImagingService
{
    public async Task<Result<ImagingOrder>> RequestAsync(
        int encounterId, string modality, string studyName, string? indication, string actorUserId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(modality) || string.IsNullOrWhiteSpace(studyName))
        {
            return Result<ImagingOrder>.Fail("Modality and study name are required.", "validation");
        }

        var encounter = await dbContext.Encounters
            .FirstOrDefaultAsync(e => e.Id == encounterId, cancellationToken);
        if (encounter is null)
        {
            return Result<ImagingOrder>.Fail("Encounter not found.", "not_found");
        }

        if (encounter.Status is DAL.Entities.Clinical.EncounterStatus.Cancelled or DAL.Entities.Clinical.EncounterStatus.Completed)
        {
            return Result<ImagingOrder>.Fail("Imaging cannot be requested on a closed encounter.", "invalid_state");
        }

        var order = new ImagingOrder
        {
            Number = await numbers.NextFormattedAsync(ClinicalNumber.ImagingOrder, "imaging-order", cancellationToken),
            EncounterId = encounterId,
            PatientId = encounter.PatientId,
            Modality = modality.Trim().ToUpperInvariant(),
            StudyName = studyName.Trim(),
            ClinicalIndication = indication,
            OrderedByUserId = actorUserId,
            Status = ImagingOrderStatus.Requested,
            CreatedAtUtc = DateTime.UtcNow
        };

        dbContext.ImagingOrders.Add(order);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<ImagingOrder>.Ok(order);
    }

    public async Task<Result<ImagingOrder>> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        var order = await dbContext.ImagingOrders.AsNoTracking()
            .Include(o => o.Patient)
            .Include(o => o.Encounter)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

        return order is null
            ? Result<ImagingOrder>.Fail("Imaging order not found.", "not_found")
            : Result<ImagingOrder>.Ok(order);
    }

    public async Task<PagedResult<ImagingWorklistItem>> QueryAsync(
        ImagingOrderStatus? status, string? term, int page, int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.ImagingOrders.AsNoTracking();

        if (status is not null)
        {
            query = query.Where(o => o.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(term))
        {
            var trimmed = term.Trim();
            query = query.Where(o => o.Number.Contains(trimmed)
                || o.StudyName.Contains(trimmed)
                || (o.Patient != null && (o.Patient.FirstName.Contains(trimmed)
                    || o.Patient.LastName.Contains(trimmed)
                    || o.Patient.Mrn.Contains(trimmed))));
        }

        var total = await query.CountAsync(cancellationToken);
        var pageIndex = Math.Max(1, page);
        var size = Math.Clamp(pageSize, 1, 100);

        var items = await query
            .OrderByDescending(o => o.CreatedAtUtc)
            .Skip((pageIndex - 1) * size)
            .Take(size)
            .Select(o => new ImagingWorklistItem(
                o.Id,
                o.Number,
                o.Patient == null ? string.Empty : o.Patient.Mrn,
                o.Patient == null ? string.Empty : o.Patient.FirstName + " " + o.Patient.LastName,
                o.Modality,
                o.StudyName,
                o.Status,
                o.RadiologistUserId,
                o.CreatedAtUtc,
                o.PerformedAtUtc))
            .ToListAsync(cancellationToken);

        return new PagedResult<ImagingWorklistItem>(items, total, pageIndex, size);
    }

    public async Task<Result> ScheduleAsync(int orderId, DateTime scheduledForUtc, string actorUserId, CancellationToken cancellationToken = default)
    {
        var order = await dbContext.ImagingOrders.FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);
        if (order is null)
        {
            return Result.Fail("Imaging order not found.", "not_found");
        }

        if (order.Status != ImagingOrderStatus.Requested)
        {
            return Result.Fail("Only a newly requested study can be scheduled.", "invalid_state");
        }

        if (scheduledForUtc < DateTime.UtcNow.AddMinutes(-5))
        {
            return Result.Fail("The scheduled time is in the past.", "invalid_time");
        }

        order.ScheduledForUtc = scheduledForUtc;
        order.Status = ImagingOrderStatus.Scheduled;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }

    public async Task<Result> MarkPerformedAsync(int orderId, string actorUserId, CancellationToken cancellationToken = default)
    {
        var order = await dbContext.ImagingOrders.FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);
        if (order is null)
        {
            return Result.Fail("Imaging order not found.", "not_found");
        }

        if (order.Status is not (ImagingOrderStatus.Scheduled or ImagingOrderStatus.Requested))
        {
            return Result.Fail("The study is not in a state that can be marked as performed.", "invalid_state");
        }

        order.PerformedAtUtc = DateTime.UtcNow;
        order.Status = ImagingOrderStatus.Performed;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }

    public async Task<Result> SaveReportAsync(
        int orderId, string findings, string impression, string radiologistUserId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(findings) || string.IsNullOrWhiteSpace(impression))
        {
            return Result.Fail("Findings and impression are both required.", "validation");
        }

        var order = await dbContext.ImagingOrders.FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);
        if (order is null)
        {
            return Result.Fail("Imaging order not found.", "not_found");
        }

        if (order.Status == ImagingOrderStatus.Released)
        {
            return Result.Fail("A released report is final and cannot be edited. Create an amendment instead.", "immutable");
        }

        if (order.Status is ImagingOrderStatus.Requested or ImagingOrderStatus.Scheduled)
        {
            return Result.Fail("The study must be performed before a report can be written.", "invalid_state");
        }

        order.Findings = findings.Trim();
        order.Impression = impression.Trim();
        order.RadiologistUserId = radiologistUserId;
        order.Status = ImagingOrderStatus.Reported;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }

    public async Task<Result> VerifyAsync(int orderId, string radiologistUserId, CancellationToken cancellationToken = default)
    {
        var order = await dbContext.ImagingOrders.FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);
        if (order is null)
        {
            return Result.Fail("Imaging order not found.", "not_found");
        }

        if (order.Status != ImagingOrderStatus.Reported)
        {
            return Result.Fail("Only a drafted report can be verified.", "invalid_state");
        }

        if (string.IsNullOrWhiteSpace(order.Findings) || string.IsNullOrWhiteSpace(order.Impression))
        {
            return Result.Fail("Write the findings and impression before verifying.", "validation");
        }

        // The verifier is always recorded separately from the author, so a single-radiologist
        // department shows up as self-verified rather than appearing independently reviewed.
        order.VerifiedByUserId = radiologistUserId;
        order.VerifiedAtUtc = DateTime.UtcNow;
        order.Status = ImagingOrderStatus.Verified;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }

    public async Task<Result> ReleaseAsync(int orderId, string actorUserId, CancellationToken cancellationToken = default)
    {
        var order = await dbContext.ImagingOrders.FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);
        if (order is null)
        {
            return Result.Fail("Imaging order not found.", "not_found");
        }

        if (order.Status != ImagingOrderStatus.Verified)
        {
            return Result.Fail("Only a verified report can be released to the clinical record.", "invalid_state");
        }

        order.Status = ImagingOrderStatus.Released;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }

    public async Task<IReadOnlyList<ImagingOrder>> ForPatientAsync(string patientId, CancellationToken cancellationToken = default)
    {
        return await dbContext.ImagingOrders.AsNoTracking()
            .Where(o => o.PatientId == patientId)
            .OrderByDescending(o => o.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }
}
