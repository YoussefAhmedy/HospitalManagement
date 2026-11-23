using Hospital.BLL.Common;
using Hospital.BLL.Common.Rules;
using Hospital.BLL.Services.Abstraction;
using Hospital.DAL.DataBase;
using Hospital.DAL.Entities.Diagnostics;
using Microsoft.EntityFrameworkCore;

namespace Hospital.BLL.Services.Implementation;

public sealed class LabService(
    HospitalDbContext dbContext,
    INumberSequenceService numbers) : ILabService
{
    public async Task<Result<LabOrder>> CreateOrderAsync(
        int encounterId, IReadOnlyCollection<int> labTestIds, string actorUserId,
        string? clinicalNotes, CancellationToken cancellationToken = default)
    {
        if (labTestIds.Count == 0)
        {
            return Result<LabOrder>.Fail("Select at least one test.", "empty");
        }

        var encounter = await dbContext.Encounters
            .FirstOrDefaultAsync(e => e.Id == encounterId, cancellationToken);
        if (encounter is null)
        {
            return Result<LabOrder>.Fail("Encounter not found.", "not_found");
        }

        if (encounter.Status == DAL.Entities.Clinical.EncounterStatus.Cancelled)
        {
            return Result<LabOrder>.Fail("Cannot order tests on a cancelled encounter.", "invalid_state");
        }

        var distinctIds = labTestIds.Distinct().ToArray();
        var tests = await dbContext.LabTests
            .Where(t => distinctIds.Contains(t.Id) && t.IsActive)
            .ToListAsync(cancellationToken);

        if (tests.Count != distinctIds.Length)
        {
            return Result<LabOrder>.Fail("One or more selected tests are unavailable.", "invalid_test");
        }

        var order = new LabOrder
        {
            Number = await numbers.NextFormattedAsync(ClinicalNumber.LabOrder, "lab-order", cancellationToken),
            EncounterId = encounterId,
            PatientId = encounter.PatientId,
            OrderedByUserId = actorUserId,
            ClinicalNotes = clinicalNotes?.Length > 500 ? clinicalNotes[..500] : clinicalNotes,
            Status = LabOrderStatus.AwaitingCollection,
            CreatedAtUtc = DateTime.UtcNow,
            Items = tests.Select(test => new LabOrderItem
            {
                LabTestId = test.Id,
                ResultPrice = test.Price
            }).ToList()
        };

        dbContext.LabOrders.Add(order);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<LabOrder>.Ok(order);
    }

    public async Task<Result<LabOrder>> TransitionAsync(
        int orderId, LabOrderStatus target, string actorUserId,
        string? specimenId, CancellationToken cancellationToken = default)
    {
        var order = await dbContext.LabOrders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        if (order is null)
        {
            return Result<LabOrder>.Fail("Laboratory order not found.", "not_found");
        }

        if (!LabRules.CanTransition(order.Status, target))
        {
            return Result<LabOrder>.Fail(
                $"The order cannot move from {order.Status} to {target}.", "invalid_transition");
        }

        if (target == LabOrderStatus.AwaitingVerification &&
            !order.Items.All(i => !string.IsNullOrWhiteSpace(i.ResultValue) || i.NumericResult is not null))
        {
            return Result<LabOrder>.Fail("Every test needs a result before verification can begin.", "incomplete_results");
        }

        switch (target)
        {
            case LabOrderStatus.Collected:
                order.CollectedAtUtc = DateTime.UtcNow;
                order.CollectedByUserId = actorUserId;
                if (!string.IsNullOrWhiteSpace(specimenId))
                {
                    order.SpecimenId = specimenId;
                }
                else if (string.IsNullOrWhiteSpace(order.SpecimenId))
                {
                    order.SpecimenId = await numbers.NextFormattedAsync(
                        ClinicalNumber.Specimen, "specimen", cancellationToken);
                }
                break;
            case LabOrderStatus.Received:
                order.ReceivedAtUtc = DateTime.UtcNow;
                break;
            case LabOrderStatus.Released:
                order.ReleasedAtUtc = DateTime.UtcNow;
                order.ReleasedByUserId = actorUserId;
                foreach (var item in order.Items)
                {
                    item.VerifiedByUserId ??= actorUserId;
                    item.VerifiedAtUtc ??= DateTime.UtcNow;
                }
                break;
        }

        order.Status = target;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<LabOrder>.Ok(order);
    }

    public async Task<Result<LabOrderItem>> EnterResultAsync(
        int orderItemId, string? value, decimal? numericValue,
        string actorUserId, string? comment,
        CancellationToken cancellationToken = default)
    {
        var item = await dbContext.LabOrderItems
            .Include(i => i.LabTest)
            .Include(i => i.LabOrder)
            .FirstOrDefaultAsync(i => i.Id == orderItemId, cancellationToken);

        if (item is null || item.LabOrder is null || item.LabTest is null)
        {
            return Result<LabOrderItem>.Fail("Laboratory order item not found.", "not_found");
        }

        var order = item.LabOrder;
        if (order.Status == LabOrderStatus.Released)
        {
            return Result<LabOrderItem>.Fail(
                "Released results are final. A correction must be issued as a new authorised result.", "immutable");
        }

        if (!LabRules.CanEnterResults(order.Status))
        {
            return Result<LabOrderItem>.Fail(
                "Results can only be entered after the specimen is received.", "invalid_state");
        }

        if (string.IsNullOrWhiteSpace(value) && numericValue is null)
        {
            return Result<LabOrderItem>.Fail("Enter a result value.", "empty");
        }

        item.ResultValue = value?.Length > 40 ? value[..40] : value;
        item.NumericResult = numericValue;
        item.Comment = comment?.Length > 500 ? comment[..500] : comment;
        item.Flag = LabRules.Classify(
            numericValue,
            item.LabTest.NormalLow,
            item.LabTest.NormalHigh,
            item.LabTest.CriticalLow,
            item.LabTest.CriticalHigh);
        item.EnteredByUserId = actorUserId;
        item.EnteredAtUtc = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<LabOrderItem>.Ok(item);
    }

    public async Task<LabOrder?> GetAsync(int orderId, CancellationToken cancellationToken = default) =>
        await dbContext.LabOrders
            .AsNoTracking()
            .Include(o => o.Patient)
            .Include(o => o.Encounter)
            .Include(o => o.Items).ThenInclude(i => i.LabTest)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

    public async Task<PagedResult<LabOrder>> QueryAsync(
        LabOrderStatus? status, string? patientId, DateRange range,
        string? term, int page, int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.LabOrders.AsNoTracking().AsQueryable();

        if (status is not null)
        {
            query = query.Where(o => o.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(patientId))
        {
            query = query.Where(o => o.PatientId == patientId);
        }

        if (range.FromUtc is not null)
        {
            query = query.Where(o => o.CreatedAtUtc >= range.FromUtc);
        }

        if (range.ToUtcExclusive is not null)
        {
            query = query.Where(o => o.CreatedAtUtc < range.ToUtcExclusive);
        }

        if (!string.IsNullOrWhiteSpace(term))
        {
            var trimmed = term.Trim();
            query = query.Where(o =>
                o.Number.Contains(trimmed) ||
                (o.Patient != null && (o.Patient.FirstName.Contains(trimmed) || o.Patient.LastName.Contains(trimmed))) ||
                (o.Patient != null && o.Patient.Mrn != null && o.Patient.Mrn.Contains(trimmed)));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(o => o.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<LabOrder>(items, total, page, pageSize);
    }

    public async Task<LabQueueCounts> QueueCountsAsync(CancellationToken cancellationToken = default)
    {
        var counts = await dbContext.LabOrders.AsNoTracking()
            .GroupBy(o => o.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        int Get(LabOrderStatus status) => counts.FirstOrDefault(c => c.Status == status)?.Count ?? 0;

        var criticalToday = await dbContext.LabOrderItems.AsNoTracking()
            .CountAsync(i => i.Flag == ResultFlag.Critical && i.EnteredAtUtc >= DateTime.UtcNow.Date, cancellationToken);

        return new LabQueueCounts(
            Get(LabOrderStatus.AwaitingCollection),
            Get(LabOrderStatus.Collected) + Get(LabOrderStatus.Received),
            Get(LabOrderStatus.Processing),
            Get(LabOrderStatus.AwaitingVerification),
            Get(LabOrderStatus.Released),
            criticalToday);
    }
}
