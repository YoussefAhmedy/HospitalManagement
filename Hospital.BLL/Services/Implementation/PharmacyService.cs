using Hospital.BLL.Common;
using Hospital.BLL.Services.Abstraction;
using Hospital.DAL.DataBase;
using Hospital.DAL.Entities.Pharmacy;
using Microsoft.EntityFrameworkCore;

namespace Hospital.BLL.Services.Implementation;

public sealed class PharmacyService(
    HospitalDbContext dbContext,
    INumberSequenceService numbers) : IPharmacyService
{
    public async Task<Result<Prescription>> CreateAsync(
        int encounterId, IReadOnlyCollection<int> medicationIds, string actorUserId,
        string dose, string frequency, string duration, string? route,
        CancellationToken cancellationToken = default)
    {
        if (medicationIds.Count == 0)
        {
            return Result<Prescription>.Fail("Select at least one medication.", "empty");
        }

        if (string.IsNullOrWhiteSpace(dose) || string.IsNullOrWhiteSpace(frequency) || string.IsNullOrWhiteSpace(duration))
        {
            return Result<Prescription>.Fail("Dose, frequency and duration are required.", "incomplete");
        }

        var encounter = await dbContext.Encounters.FirstOrDefaultAsync(e => e.Id == encounterId, cancellationToken);
        if (encounter is null)
        {
            return Result<Prescription>.Fail("Encounter not found.", "not_found");
        }

        if (encounter.Status == DAL.Entities.Clinical.EncounterStatus.Cancelled)
        {
            return Result<Prescription>.Fail("Cannot prescribe on a cancelled encounter.", "invalid_state");
        }

        var distinct = medicationIds.Distinct().ToArray();
        var medications = await dbContext.Medications
            .Where(m => distinct.Contains(m.Id) && m.IsActive)
            .ToListAsync(cancellationToken);

        if (medications.Count != distinct.Length)
        {
            return Result<Prescription>.Fail("One or more medications are unavailable.", "invalid_medication");
        }

        var prescription = new Prescription
        {
            Number = await numbers.NextFormattedAsync(ClinicalNumber.Prescription, "prescription", cancellationToken),
            EncounterId = encounterId,
            PatientId = encounter.PatientId,
            PrescribedByUserId = actorUserId,
            Status = PrescriptionStatus.Pending,
            CreatedAtUtc = DateTime.UtcNow,
            Items = medications.Select(medication => new PrescriptionItem
            {
                MedicationId = medication.Id,
                Dose = dose,
                Frequency = frequency,
                Duration = duration,
                Route = route,
                Quantity = Math.Max(1, ParseQuantity(dose) * ParseFrequencyPerDay(frequency) * ParseDurationDays(duration))
            }).ToList()
        };

        dbContext.Prescriptions.Add(prescription);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<Prescription>.Ok(prescription);
    }

    public async Task<Result> ReviewAsync(
        int prescriptionId, bool approve, string pharmacistUserId, string? note,
        CancellationToken cancellationToken = default)
    {
        var prescription = await dbContext.Prescriptions
            .Include(p => p.Items)
            .FirstOrDefaultAsync(p => p.Id == prescriptionId, cancellationToken);

        if (prescription is null)
        {
            return Result.Fail("Prescription not found.", "not_found");
        }

        if (prescription.Status != PrescriptionStatus.Pending)
        {
            return Result.Fail($"Only pending prescriptions can be reviewed (current: {prescription.Status}).", "invalid_state");
        }

        prescription.Status = approve ? PrescriptionStatus.Reviewed : PrescriptionStatus.Rejected;
        prescription.PharmacistNote = note?.Length > 500 ? note[..500] : note;
        prescription.ReviewedByUserId = pharmacistUserId;
        prescription.ReviewedAtUtc = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }

    public async Task<Result<Prescription>> DispenseAsync(
        int prescriptionId, IReadOnlyCollection<DispenseLineRequest> lines, string pharmacistUserId,
        CancellationToken cancellationToken = default)
    {
        var prescription = await dbContext.Prescriptions
            .Include(p => p.Items)
            .FirstOrDefaultAsync(p => p.Id == prescriptionId, cancellationToken);

        if (prescription is null)
        {
            return Result<Prescription>.Fail("Prescription not found.", "not_found");
        }

        if (prescription.Status is not (PrescriptionStatus.Reviewed or PrescriptionStatus.PartiallyDispensed))
        {
            return Result<Prescription>.Fail("Only reviewed prescriptions can be dispensed.", "invalid_state");
        }

        if (lines.Count == 0)
        {
            return Result<Prescription>.Fail("Select at least one line to dispense.", "empty");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        foreach (var line in lines)
        {
            var item = prescription.Items.FirstOrDefault(i => i.Id == line.PrescriptionItemId);
            if (item is null)
            {
                return Result<Prescription>.Fail("Dispense line does not belong to this prescription.", "invalid_line");
            }

            var outstanding = item.Quantity - item.DispensedQuantity;
            if (line.Quantity <= 0 || line.Quantity > outstanding)
            {
                return Result<Prescription>.Fail("Dispense quantity exceeds the outstanding amount.", "invalid_quantity");
            }

            var batch = await dbContext.StockBatches
                .Where(b => b.MedicationId == item.MedicationId && b.QuantityOnHand >= line.Quantity)
                .OrderBy(b => b.ExpiryDate)
                .FirstOrDefaultAsync(cancellationToken);

            if (batch is null)
            {
                return Result<Prescription>.Fail("Insufficient stock for one or more medications.", "insufficient_stock");
            }

            batch.QuantityOnHand -= line.Quantity;
            item.DispensedQuantity += line.Quantity;

            dbContext.StockMovements.Add(new StockMovement
            {
                MedicationId = item.MedicationId,
                StockBatchId = batch.Id,
                Type = StockMovementType.Dispense,
                Quantity = -line.Quantity,
                Reason = $"Prescription {prescription.Number}",
                PrescriptionId = prescription.Id,
                PerformedByUserId = pharmacistUserId,
                OccurredAtUtc = DateTime.UtcNow
            });
        }

        prescription.Status = prescription.Items.All(i => i.DispensedQuantity >= i.Quantity)
            ? PrescriptionStatus.Dispensed
            : PrescriptionStatus.PartiallyDispensed;

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result<Prescription>.Ok(prescription);
    }

    public async Task<PharmacyQueueCounts> QueueCountsAsync(CancellationToken cancellationToken = default)
    {
        var counts = await dbContext.Prescriptions.AsNoTracking()
            .GroupBy(p => p.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        int Get(PrescriptionStatus status) => counts.FirstOrDefault(c => c.Status == status)?.Count ?? 0;

        var stock = await dbContext.Medications.AsNoTracking()
            .Where(m => m.IsActive)
            .Select(m => new { OnHand = m.Batches.Sum(b => b.QuantityOnHand), m.ReorderLevel })
            .ToListAsync(cancellationToken);

        var lowStock = stock.Count(s => s.OnHand <= s.ReorderLevel);

        var horizon = DateTime.UtcNow.Date.AddDays(90);
        var expiringSoon = await dbContext.StockBatches.AsNoTracking()
            .CountAsync(b => b.QuantityOnHand > 0 && b.ExpiryDate <= horizon, cancellationToken);

        return new PharmacyQueueCounts(
            Get(PrescriptionStatus.Pending),
            Get(PrescriptionStatus.Reviewed),
            Get(PrescriptionStatus.PartiallyDispensed),
            Get(PrescriptionStatus.Dispensed),
            lowStock,
            expiringSoon);
    }

    private static int ParseQuantity(string dose) =>
        int.TryParse(new string(dose.TakeWhile(char.IsDigit).ToArray()), out var value) && value > 0 ? value : 1;

    private static int ParseFrequencyPerDay(string frequency) =>
        frequency.Contains("three", StringComparison.OrdinalIgnoreCase) ? 3
        : frequency.Contains("twice", StringComparison.OrdinalIgnoreCase) ? 2
        : 1;

    private static int ParseDurationDays(string duration) =>
        int.TryParse(new string(duration.TakeWhile(char.IsDigit).ToArray()), out var value) && value > 0 ? value : 1;
}
