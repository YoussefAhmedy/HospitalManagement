using Hospital.BLL.Common;
using Hospital.BLL.Services.Abstraction;
using Hospital.DAL.DataBase;
using Hospital.DAL.Entities.Clinical;
using Hospital.DAL.Entities.Diagnostics;
using Hospital.DAL.Entities.OwnedTypes;
using Hospital.DAL.Entities.Pharmacy;
using Microsoft.EntityFrameworkCore;

namespace Hospital.BLL.Services.Implementation;

public sealed class WorkspaceService(HospitalDbContext dbContext) : IWorkspaceService
{
    public async Task<IReadOnlyList<Kpi>> AdminAsync(CancellationToken cancellationToken = default)
    {
        var today = DateTime.UtcNow.Date;

        var registeredToday = await dbContext.Encounters.AsNoTracking()
            .CountAsync(e => e.RegisteredAtUtc >= today, cancellationToken);
        var signedToday = await dbContext.Encounters.AsNoTracking()
            .CountAsync(e => e.SignedAtUtc >= today, cancellationToken);
        var pendingLabs = await dbContext.LabOrders.AsNoTracking()
            .CountAsync(o => o.Status != LabOrderStatus.Released && o.Status != LabOrderStatus.Cancelled, cancellationToken);
        var critical = await dbContext.LabOrderItems.AsNoTracking()
            .CountAsync(i => i.Flag == ResultFlag.Critical && i.EnteredAtUtc >= today, cancellationToken);
        var pendingAdmissions = await dbContext.Admissions.AsNoTracking()
            .CountAsync(a => a.Status == DAL.Entities.Operations.AdmissionStatus.Requested, cancellationToken);
        var openInvoices = await dbContext.Invoices.AsNoTracking()
            .CountAsync(i => i.Status == DAL.Entities.Billing.InvoiceStatus.Issued
                          || i.Status == DAL.Entities.Billing.InvoiceStatus.PartiallyPaid, cancellationToken);
        var lowStock = await dbContext.Medications.AsNoTracking()
            .CountAsync(m => m.IsActive && m.Batches.Sum(b => b.QuantityOnHand) <= m.ReorderLevel, cancellationToken);

        return
        [
            new Kpi("Encounters today", registeredToday.ToString(), "Registered visits across all departments"),
            new Kpi("Signed today", signedToday.ToString(), "Completed and authorised encounters"),
            new Kpi("Pending laboratory", pendingLabs.ToString(), "Orders not yet released", "attention"),
            new Kpi("Critical results today", critical.ToString(), "Requires clinical review", critical > 0 ? "critical" : "neutral"),
            new Kpi("Admissions pending", pendingAdmissions.ToString(), "Awaiting bed assignment", "attention"),
            new Kpi("Open invoices", openInvoices.ToString(), "Issued or partially paid"),
            new Kpi("Low-stock medications", lowStock.ToString(), "At or below reorder level", lowStock > 0 ? "danger" : "neutral")
        ];
    }

    public async Task<IReadOnlyList<Kpi>> DoctorAsync(string doctorId, CancellationToken cancellationToken = default)
    {
        var today = DateTime.UtcNow.Date;

        var appointmentsToday = await dbContext.Appointments.AsNoTracking()
            .CountAsync(a => a.DoctorID == doctorId && a.AppointmentDate == today
                          && a.Status != AppointStatus.Cancelled && a.Status != AppointStatus.NotApproved, cancellationToken);
        var waiting = await dbContext.Appointments.AsNoTracking()
            .CountAsync(a => a.DoctorID == doctorId && a.AppointmentDate == today
                          && a.Status == AppointStatus.CheckedIn, cancellationToken);
        var inConsultation = await dbContext.Encounters.AsNoTracking()
            .CountAsync(e => e.DoctorId == doctorId && e.Status == EncounterStatus.InConsultation, cancellationToken);
        var pendingResults = await dbContext.LabOrders.AsNoTracking()
            .CountAsync(o => o.Encounter!.DoctorId == doctorId
                          && o.Status != LabOrderStatus.Released && o.Status != LabOrderStatus.Cancelled, cancellationToken);
        var unsignedCertificates = await dbContext.Encounters.AsNoTracking()
            .CountAsync(e => e.DoctorId == doctorId && e.Status == EncounterStatus.Completed, cancellationToken);

        return
        [
            new Kpi("Appointments today", appointmentsToday.ToString(), "Confirmed and pending for your clinic"),
            new Kpi("Patients waiting", waiting.ToString(), "Checked in and waiting", waiting > 0 ? "attention" : "neutral"),
            new Kpi("In consultation", inConsultation.ToString(), "Open encounters assigned to you"),
            new Kpi("Tests pending", pendingResults.ToString(), "Laboratory orders awaiting release"),
            new Kpi("Awaiting signature", unsignedCertificates.ToString(), "Completed encounters not yet signed", unsignedCertificates > 0 ? "attention" : "neutral")
        ];
    }

    public async Task<IReadOnlyList<Kpi>> ReceptionAsync(CancellationToken cancellationToken = default)
    {
        var today = DateTime.UtcNow.Date;
        var confirmed = await dbContext.Appointments.AsNoTracking()
            .CountAsync(a => a.AppointmentDate == today && a.Status == AppointStatus.Approved, cancellationToken);
        var checkedIn = await dbContext.Appointments.AsNoTracking()
            .CountAsync(a => a.AppointmentDate == today && a.Status == AppointStatus.CheckedIn, cancellationToken);
        var pending = await dbContext.Appointments.AsNoTracking()
            .CountAsync(a => a.Status == AppointStatus.Pending, cancellationToken);
        var newPatients = await dbContext.Patients.AsNoTracking()
            .CountAsync(p => p.CreatedAt >= today, cancellationToken);

        return
        [
            new Kpi("Confirmed today", confirmed.ToString(), "Appointments ready for check-in"),
            new Kpi("Checked in", checkedIn.ToString(), "Patients currently in the clinic"),
            new Kpi("Requests awaiting review", pending.ToString(), "New appointment requests", pending > 0 ? "attention" : "neutral"),
            new Kpi("New patients today", newPatients.ToString(), "Registrations completed"),
            new Kpi("Open encounters", (await dbContext.Encounters.AsNoTracking()
                .CountAsync(e => e.Status == EncounterStatus.Registered || e.Status == EncounterStatus.Triaged, cancellationToken)).ToString(), "Registered or triaged")
        ];
    }

    public async Task<IReadOnlyList<Kpi>> LaboratoryAsync(CancellationToken cancellationToken = default)
    {
        var counts = await dbContext.LabOrders.AsNoTracking()
            .GroupBy(o => o.Status)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        int Get(LabOrderStatus s) => counts.FirstOrDefault(c => c.Key == s)?.Count ?? 0;
        var critical = await dbContext.LabOrderItems.AsNoTracking()
            .CountAsync(i => i.Flag == ResultFlag.Critical && i.EnteredAtUtc >= DateTime.UtcNow.Date, cancellationToken);

        return
        [
            new Kpi("Awaiting collection", Get(LabOrderStatus.AwaitingCollection).ToString(), "Specimens not yet drawn"),
            new Kpi("In laboratory", (Get(LabOrderStatus.Collected) + Get(LabOrderStatus.Received) + Get(LabOrderStatus.Processing)).ToString(), "Collected, received or processing"),
            new Kpi("Awaiting verification", Get(LabOrderStatus.AwaitingVerification).ToString(), "Results entered, not verified", "attention"),
            new Kpi("Released today", Get(LabOrderStatus.Released).ToString(), "Authorised results"),
            new Kpi("Critical today", critical.ToString(), "Out-of-range critical values", critical > 0 ? "critical" : "neutral")
        ];
    }

    public async Task<IReadOnlyList<Kpi>> PharmacyAsync(CancellationToken cancellationToken = default)
    {
        var counts = await dbContext.Prescriptions.AsNoTracking()
            .GroupBy(p => p.Status)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        int Get(PrescriptionStatus s) => counts.FirstOrDefault(c => c.Key == s)?.Count ?? 0;
        var lowStock = await dbContext.Medications.AsNoTracking()
            .CountAsync(m => m.IsActive && m.Batches.Sum(b => b.QuantityOnHand) <= m.ReorderLevel, cancellationToken);
        var expiringSoon = await dbContext.StockBatches.AsNoTracking()
            .CountAsync(b => b.QuantityOnHand > 0 && b.ExpiryDate <= DateTime.UtcNow.Date.AddDays(90), cancellationToken);

        return
        [
            new Kpi("Awaiting review", Get(PrescriptionStatus.Pending).ToString(), "New prescriptions", "attention"),
            new Kpi("Ready to dispense", Get(PrescriptionStatus.Reviewed).ToString(), "Reviewed by a pharmacist"),
            new Kpi("Partial dispensations", Get(PrescriptionStatus.PartiallyDispensed).ToString(), "Awaiting remaining stock"),
            new Kpi("Low stock", lowStock.ToString(), "At or below reorder level", lowStock > 0 ? "danger" : "neutral"),
            new Kpi("Expiring ≤ 90 days", expiringSoon.ToString(), "Batches to review", expiringSoon > 0 ? "attention" : "neutral")
        ];
    }

    public async Task<IReadOnlyList<Kpi>> BillingAsync(CancellationToken cancellationToken = default)
    {
        var revenue = await dbContext.Payments.AsNoTracking()
            .Where(p => p.ApprovedByUserId != null || !p.RequiresApproval)
            .Select(p => new { p.Amount, p.IsRefund, p.PaidAtUtc })
            .ToListAsync(cancellationToken);

        var today = DateTime.UtcNow.Date;
        var monthStart = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var todayTotal = revenue.Where(p => p.PaidAtUtc >= today).Sum(p => p.IsRefund ? -p.Amount : p.Amount);
        var monthTotal = revenue.Where(p => p.PaidAtUtc >= monthStart).Sum(p => p.IsRefund ? -p.Amount : p.Amount);

        var invoices = await dbContext.Invoices.AsNoTracking()
            .Where(i => i.Status != DAL.Entities.Billing.InvoiceStatus.Void)
            .Select(i => new
            {
                Total = i.Lines.Sum(l => l.Quantity * l.UnitPrice),
                i.InsuranceCoveredAmount,
                Paid = i.Payments.Where(p => !p.IsRefund).Sum(p => p.Amount) - i.Payments.Where(p => p.IsRefund).Sum(p => p.Amount),
                i.Status
            })
            .ToListAsync(cancellationToken);

        var outstanding = invoices.Select(i => i.Total - i.InsuranceCoveredAmount - i.Paid).Where(b => b > 0).Sum();
        var open = invoices.Count(i => i.Status == DAL.Entities.Billing.InvoiceStatus.Issued
                                    || i.Status == DAL.Entities.Billing.InvoiceStatus.PartiallyPaid);
        var pendingRefunds = await dbContext.Payments.AsNoTracking()
            .CountAsync(p => p.IsRefund && p.RequiresApproval && p.ApprovedByUserId == null, cancellationToken);

        return
        [
            new Kpi("Revenue today", Money.Format(todayTotal), "Net of refunds"),
            new Kpi("Revenue this month", Money.Format(monthTotal), "Net of refunds"),
            new Kpi("Outstanding", Money.Format(outstanding), "Unpaid balances", "attention"),
            new Kpi("Open invoices", open.ToString(), "Issued or partially paid"),
            new Kpi("Refunds awaiting approval", pendingRefunds.ToString(), "Requires a second officer", pendingRefunds > 0 ? "attention" : "neutral")
        ];
    }

    public async Task<IReadOnlyList<Kpi>> PatientAsync(string patientId, CancellationToken cancellationToken = default)
    {
        var today = DateTime.UtcNow.Date;
        var upcoming = await dbContext.Appointments.AsNoTracking()
            .CountAsync(a => a.PatientID == patientId && a.AppointmentDate >= today
                          && a.Status != AppointStatus.Cancelled && a.Status != AppointStatus.NotApproved, cancellationToken);
        var releasedResults = await dbContext.LabOrders.AsNoTracking()
            .CountAsync(o => o.PatientId == patientId && o.Status == LabOrderStatus.Released, cancellationToken);
        var activePrescriptions = await dbContext.Prescriptions.AsNoTracking()
            .CountAsync(p => p.PatientId == patientId && p.Status != PrescriptionStatus.Cancelled
                          && p.Status != PrescriptionStatus.Rejected, cancellationToken);
        var balance = await dbContext.Invoices.AsNoTracking()
            .Where(i => i.PatientId == patientId && i.Status != DAL.Entities.Billing.InvoiceStatus.Void)
            .Select(i => i.Lines.Sum(l => l.Quantity * l.UnitPrice) - i.InsuranceCoveredAmount
                       - (i.Payments.Where(p => !p.IsRefund).Sum(p => p.Amount) - i.Payments.Where(p => p.IsRefund).Sum(p => p.Amount)))
            .ToListAsync(cancellationToken);
        var allergies = await dbContext.Allergies.AsNoTracking()
            .CountAsync(a => a.PatientId == patientId, cancellationToken);

        return
        [
            new Kpi("Upcoming appointments", upcoming.ToString(), "Scheduled visits"),
            new Kpi("Released results", releasedResults.ToString(), "Authorised laboratory results"),
            new Kpi("Active prescriptions", activePrescriptions.ToString(), "Issued and not cancelled"),
            new Kpi("Account balance", Money.Format(balance.Where(b => b > 0).Sum()), "Outstanding invoices", balance.Any(b => b > 0) ? "attention" : "neutral"),
            new Kpi("Documented allergies", allergies.ToString(), allergies > 0 ? "Show this to every clinician" : "None recorded", allergies > 0 ? "attention" : "neutral")
        ];
    }

    public async Task<IReadOnlyList<ActivityItem>> RecentActivityAsync(int take = 8, CancellationToken cancellationToken = default)
    {
        var audits = await dbContext.AuditLogs.AsNoTracking()
            .OrderByDescending(a => a.OccurredAtUtc)
            .Take(take)
            .Select(a => new { a.EventType, a.ResourceType, a.OccurredAtUtc })
            .ToListAsync(cancellationToken);

        return audits.Select(a => new ActivityItem(
            Humanise(a.EventType),
            a.ResourceType ?? "system",
            a.OccurredAtUtc.ToString("yyyy-MM-dd HH:mm") + " UTC")).ToList();
    }

    private static string Humanise(string eventType) =>
        eventType.Replace('.', ' ').Replace('_', ' ') switch
        {
            var text when text.Length == 0 => "Event",
            var text => char.ToUpperInvariant(text[0]) + text[1..]
        };
}
