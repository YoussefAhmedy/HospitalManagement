using System.Globalization;
using System.Text;
using Hospital.BLL.Common;
using Hospital.BLL.Services.Abstraction;
using Hospital.DAL.DataBase;
using Hospital.DAL.Entities.Billing;
using Hospital.DAL.Entities.Clinical;
using Hospital.DAL.Entities.Diagnostics;
using Hospital.DAL.Entities.OwnedTypes;
using Hospital.DAL.Entities.Operations;
using Hospital.DAL.Entities.Pharmacy;
using Microsoft.EntityFrameworkCore;

namespace Hospital.BLL.Services.Implementation;

/// <summary>
/// Executes every report as a real query against the live dataset. Values are formatted
/// for display here so the HTML view, the print layout and the CSV export always agree.
/// </summary>
public sealed class ReportService(HospitalDbContext dbContext) : IReportService
{
    public IReadOnlyList<ReportDescriptor> Catalogue { get; } =
    [
        new("patient-visits", "Patient visit history", "Every scheduled visit in the period with outcome.", "report.view"),
        new("encounters", "Clinical encounters", "Encounter volume, department and clinician workload.", "report.view"),
        new("appointments-status", "Appointment outcomes", "Booked, attended, cancelled and no-show counts.", "report.view"),
        new("no-shows", "No-show register", "Confirmed appointments not attended.", "report.view"),
        new("admissions", "Admissions and discharges", "Inpatient flow for the period.", "report.view"),
        new("bed-occupancy", "Bed occupancy", "Live occupancy by ward and room.", "report.view"),
        new("department-workload", "Department workload", "Encounter volume per department.", "report.view"),
        new("lab-throughput", "Laboratory throughput", "Orders by state with turnaround.", "report.view"),
        new("lab-pending", "Pending laboratory results", "Unreleased orders awaiting action.", "report.view"),
        new("lab-tests", "Tests requested", "Volume by test with abnormal rate.", "report.view"),
        new("lab-critical", "Critical results", "Values outside critical limits requiring action.", "report.view"),
        new("revenue", "Revenue by period", "Approved payments and refunds by day.", "report.financial"),
        new("revenue-department", "Revenue by department", "Income attribution by service department.", "report.financial"),
        new("outstanding", "Outstanding invoices", "Balances owed by patients.", "report.financial"),
        new("payments", "Payments ledger", "Every payment and refund recorded.", "report.financial"),
        new("inventory", "Medication inventory", "Stock on hand, value and reorder state.", "report.view"),
        new("expiry", "Expiring stock", "Batches expiring within the horizon.", "report.view"),
        new("prescriptions", "Prescription register", "Prescribing volume by medication.", "report.view")
    ];

    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("en-US");

    public async Task<ReportResult> RunAsync(string key, ReportFilter filter, CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, filter.Page);
        var size = filter.PageSize is < 1 or > 200 ? 50 : filter.PageSize;

        return key switch
        {
            "patient-visits" => await PatientVisitsAsync(filter, page, size, cancellationToken),
            "encounters" => await EncountersAsync(filter, page, size, cancellationToken),
            "appointments-status" => await AppointmentStatusAsync(filter, page, size, cancellationToken),
            "no-shows" => await NoShowsAsync(filter, page, size, cancellationToken),
            "admissions" => await AdmissionsAsync(filter, page, size, cancellationToken),
            "bed-occupancy" => await BedOccupancyAsync(filter, page, size, cancellationToken),
            "department-workload" => await DepartmentWorkloadAsync(filter, page, size, cancellationToken),
            "lab-throughput" => await LabThroughputAsync(filter, page, size, cancellationToken),
            "lab-pending" => await LabPendingAsync(filter, page, size, cancellationToken),
            "lab-tests" => await LabTestsAsync(filter, page, size, cancellationToken),
            "lab-critical" => await LabCriticalAsync(filter, page, size, cancellationToken),
            "revenue" => await RevenueAsync(filter, page, size, cancellationToken),
            "revenue-department" => await RevenueDepartmentAsync(filter, page, size, cancellationToken),
            "outstanding" => await OutstandingAsync(filter, page, size, cancellationToken),
            "payments" => await PaymentsAsync(filter, page, size, cancellationToken),
            "inventory" => await InventoryAsync(filter, page, size, cancellationToken),
            "expiry" => await ExpiryAsync(filter, page, size, cancellationToken),
            "prescriptions" => await PrescriptionsAsync(filter, page, size, cancellationToken),
            _ => ReportResult.Empty(key, "Unknown report")
        };
    }

    public string ToCsv(ReportResult report)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Report,{Csv(report.Title)}");
        builder.AppendLine($"Generated,{report.GeneratedAtUtc.ToString("u", CultureInfo.InvariantCulture)}");
        if (!string.IsNullOrWhiteSpace(report.FilterSummary))
        {
            builder.AppendLine($"Filters,{Csv(report.FilterSummary)}");
        }

        builder.AppendLine();
        builder.AppendLine(string.Join(",", report.Columns.Select(c => Csv(c.Label))));
        foreach (var row in report.Rows)
        {
            builder.AppendLine(string.Join(",", row.Select(Csv)));
        }

        builder.AppendLine();
        foreach (var pair in report.Summary)
        {
            builder.AppendLine($"{Csv(pair.Key)},{Csv(pair.Value)}");
        }

        return builder.ToString();
    }

    // ---------------------------------------------------------------- reports

    private async Task<ReportResult> PatientVisitsAsync(ReportFilter filter, int page, int size, CancellationToken ct)
    {
        var query = ApplyRange(dbContext.Appointments.AsNoTracking()
            .Where(a => a.Status != AppointStatus.Cancelled && a.PatientID != null), filter);

        var total = await query.CountAsync(ct);
        var rows = await query
            .OrderByDescending(a => a.AppointmentDate).ThenBy(a => a.SlotMinutes)
            .Skip((page - 1) * size).Take(size)
            .Select(a => new
            {
                a.AppointmentDate,
                a.SlotMinutes,
                Patient = a.Patient!.FirstName + " " + a.Patient.LastName,
                Mrn = a.Patient.Mrn,
                Doctor = a.Doctor == null ? "—" : a.Doctor.FirstName + " " + a.Doctor.LastName,
                a.Status
            })
            .ToListAsync(ct);

        var data = rows.Select(r => (IReadOnlyList<string>)
        [
            r.AppointmentDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            Slot(r.SlotMinutes),
            r.Mrn ?? "—",
            r.Patient,
            r.Doctor,
            r.Status.ToString()
        ]).ToList();

        var attended = await query.CountAsync(a => a.Status == AppointStatus.Completed, ct);

        return Build("patient-visits", "Patient visit history", filter, total, page, size,
            [new("date", "Date", "date"), new("slot", "Slot", "text"), new("mrn", "MRN", "text"), new("patient", "Patient", "text"), new("clinician", "Clinician", "text"), new("status", "Status", "status")],
            data,
            [new("Attended", attended.ToString(CultureInfo.InvariantCulture)), new("Registered", total.ToString(CultureInfo.InvariantCulture))]);
    }

    private async Task<ReportResult> EncountersAsync(ReportFilter filter, int page, int size, CancellationToken ct)
    {
        var query = dbContext.Encounters.AsNoTracking();
        if (filter.Range.FromUtc is not null) query = query.Where(e => e.RegisteredAtUtc >= filter.Range.FromUtc);
        if (filter.Range.ToUtcExclusive is not null) query = query.Where(e => e.RegisteredAtUtc < filter.Range.ToUtcExclusive);
        if (filter.DepartmentId is not null) query = query.Where(e => e.DepartmentId == filter.DepartmentId);
        if (!string.IsNullOrWhiteSpace(filter.DoctorId)) query = query.Where(e => e.DoctorId == filter.DoctorId);
        if (Enum.TryParse<EncounterStatus>(filter.Status, true, out var status)) query = query.Where(e => e.Status == status);

        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(e => e.RegisteredAtUtc).Skip((page - 1) * size).Take(size)
            .Select(e => new
            {
                e.Number,
                Date = e.RegisteredAtUtc,
                Patient = e.Patient!.FirstName + " " + e.Patient.LastName,
                Doctor = e.Doctor == null ? "—" : e.Doctor.FirstName + " " + e.Doctor.LastName,
                Department = e.Department == null ? "—" : e.Department.Name,
                e.Type,
                e.Status
            }).ToListAsync(ct);

        var data = rows.Select(r => (IReadOnlyList<string>)
        [
            r.Number, r.Date.ToString("yyyy-MM-dd HH:mm", Culture, CultureInfo.InvariantCulture),
            r.Patient, r.Doctor, r.Department, r.Type.ToString(), r.Status.ToString()
        ]).ToList();

        var signed = await query.CountAsync(e => e.Status == EncounterStatus.Signed, ct);

        return Build("encounters", "Clinical encounters", filter, total, page, size,
            [new("encounter", "Encounter", "text"), new("registered", "Registered", "datetime"), new("patient", "Patient", "text"), new("clinician", "Clinician", "text"), new("department", "Department", "text"), new("type", "Type", "text"), new("status", "Status", "status")],
            data,
            [new("Total", total.ToString(CultureInfo.InvariantCulture)), new("Signed", signed.ToString(CultureInfo.InvariantCulture))]);
    }

    private async Task<ReportResult> AppointmentStatusAsync(ReportFilter filter, int page, int size, CancellationToken ct)
    {
        var query = ApplyRange(dbContext.Appointments.AsNoTracking(), filter);
        var groups = await query.GroupBy(a => a.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var total = groups.Sum(g => g.Count);
        var data = groups.OrderByDescending(g => g.Count)
            .Select(g => (IReadOnlyList<string>)[g.Status.ToString(), g.Count.ToString(CultureInfo.InvariantCulture),
                total == 0 ? "0%" : (g.Count * 100d / total).ToString("0.0", Culture) + "%"])
            .ToList();

        return Build("appointments-status", "Appointment outcomes", filter, groups.Count, page, size,
            [new("status", "Status", "status"), new("count", "Count", "number"), new("share", "Share", "percent")], data,
            [new("Bookings", total.ToString(CultureInfo.InvariantCulture))]);
    }

    private async Task<ReportResult> NoShowsAsync(ReportFilter filter, int page, int size, CancellationToken ct)
    {
        var query = ApplyRange(dbContext.Appointments.AsNoTracking()
            .Where(a => a.Status == AppointStatus.NoShow && a.PatientID != null), filter);

        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(a => a.AppointmentDate).Skip((page - 1) * size).Take(size)
            .Select(a => new
            {
                a.AppointmentDate,
                Patient = a.Patient!.FirstName + " " + a.Patient.LastName,
                Mrn = a.Patient.Mrn,
                Doctor = a.Doctor == null ? "—" : a.Doctor.FirstName + " " + a.Doctor.LastName
            }).ToListAsync(ct);

        return Build("no-shows", "No-show register", filter, total, page, size,
            [new("date", "Date", "date"), new("mrn", "MRN", "text"), new("patient", "Patient", "text"), new("clinician", "Clinician", "text")],
            rows.Select(r => (IReadOnlyList<string>)[r.AppointmentDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), r.Mrn ?? "—", r.Patient, r.Doctor]).ToList(),
            [new("No-shows", total.ToString(CultureInfo.InvariantCulture))]);
    }

    private async Task<ReportResult> AdmissionsAsync(ReportFilter filter, int page, int size, CancellationToken ct)
    {
        var query = dbContext.Admissions.AsNoTracking();
        if (filter.Range.FromUtc is not null) query = query.Where(a => a.RequestedAtUtc >= filter.Range.FromUtc);
        if (filter.Range.ToUtcExclusive is not null) query = query.Where(a => a.RequestedAtUtc < filter.Range.ToUtcExclusive);

        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(a => a.RequestedAtUtc).Skip((page - 1) * size).Take(size)
            .Select(a => new
            {
                a.Number,
                Patient = a.Patient!.FirstName + " " + a.Patient.LastName,
                a.RequestedAtUtc,
                a.AdmittedAtUtc,
                a.DischargedAtUtc,
                a.Status
            }).ToListAsync(ct);

        var data = rows.Select(r => (IReadOnlyList<string>)
        [
            r.Number, r.Patient, r.RequestedAtUtc.ToString("yyyy-MM-dd HH:mm", Culture),
            r.AdmittedAtUtc?.ToString("yyyy-MM-dd HH:mm", Culture) ?? "—",
            r.DischargedAtUtc?.ToString("yyyy-MM-dd HH:mm", Culture) ?? "—",
            r.Status.ToString()
        ]).ToList();

        var discharged = await query.CountAsync(a => a.Status == AdmissionStatus.Discharged, ct);

        return Build("admissions", "Admissions and discharges", filter, total, page, size,
            [new("admission", "Admission", "text"), new("patient", "Patient", "text"), new("requested", "Requested", "datetime"), new("admitted", "Admitted", "datetime"), new("discharged", "Discharged", "datetime"), new("status", "Status", "status")],
            data,
            [new("Admissions", total.ToString(CultureInfo.InvariantCulture)), new("Discharged", discharged.ToString(CultureInfo.InvariantCulture))]);
    }

    private async Task<ReportResult> BedOccupancyAsync(ReportFilter filter, int page, int size, CancellationToken ct)
    {
        var beds = await dbContext.Beds.AsNoTracking()
            .Include(b => b.Room).ThenInclude(r => r!.Ward)
            .Where(b => b.IsActive)
            .Select(b => new
            {
                Ward = b.Room!.Ward!.Name,
                Room = b.Room.Number,
                b.Label,
                Occupied = b.Assignments.Any(a => a.ReleasedAtUtc == null)
            })
            .ToListAsync(ct);

        var grouped = beds.GroupBy(b => b.Ward).OrderBy(g => g.Key).ToList();
        var data = grouped.Select(g => (IReadOnlyList<string>)
        [
            g.Key,
            g.Count().ToString(CultureInfo.InvariantCulture),
            g.Count(b => b.Occupied).ToString(CultureInfo.InvariantCulture),
            g.Count(b => !b.Occupied).ToString(CultureInfo.InvariantCulture),
            g.Count() == 0 ? "0%" : (g.Count(b => b.Occupied) * 100d / g.Count()).ToString("0.0", Culture) + "%"
        ]).ToList();

        var occupied = beds.Count(b => b.Occupied);
        return Build("bed-occupancy", "Bed occupancy", filter, grouped.Count, page, size,
            [new("ward", "Ward", "text"), new("beds", "Beds", "number"), new("occupied", "Occupied", "number"), new("available", "Available", "number"), new("occupancy", "Occupancy", "percent")],
            data,
            [
                new("Version", "LIVE"),
                new("Total beds", beds.Count.ToString(CultureInfo.InvariantCulture)),
                new("Occupancy", beds.Count == 0 ? "0%" : (occupied * 100d / beds.Count).ToString("0.0", Culture) + "%")
            ]);
    }

    private async Task<ReportResult> DepartmentWorkloadAsync(ReportFilter filter, int page, int size, CancellationToken ct)
    {
        var query = dbContext.Encounters.AsNoTracking();
        if (filter.Range.FromUtc is not null) query = query.Where(e => e.RegisteredAtUtc >= filter.Range.FromUtc);
        if (filter.Range.ToUtcExclusive is not null) query = query.Where(e => e.RegisteredAtUtc < filter.Range.ToUtcExclusive);

        var grouped = await query.GroupBy(e => e.Department!.Name)
            .Select(g => new { Department = g.Key, Count = g.Count(), Signed = g.Count(e => e.Status == EncounterStatus.Signed) })
            .ToListAsync(ct);

        var ordered = grouped.OrderByDescending(g => g.Count).ToList();
        var data = ordered.Select(g => (IReadOnlyList<string>)
        [
            g.Department ?? "Unassigned", g.Count.ToString(CultureInfo.InvariantCulture), g.Signed.ToString(CultureInfo.InvariantCulture)
        ]).ToList();

        return Build("department-workload", "Department workload", filter, ordered.Count, page, size,
            [new("department", "Department", "text"), new("encounters", "Encounters", "number"), new("signed", "Signed", "number")], data,
            [new("Departments", ordered.Count.ToString(CultureInfo.InvariantCulture))]);
    }

    private async Task<ReportResult> LabThroughputAsync(ReportFilter filter, int page, int size, CancellationToken ct)
    {
        var query = dbContext.LabOrders.AsNoTracking();
        if (filter.Range.FromUtc is not null) query = query.Where(o => o.CreatedAtUtc >= filter.Range.FromUtc);
        if (filter.Range.ToUtcExclusive is not null) query = query.Where(o => o.CreatedAtUtc < filter.Range.ToUtcExclusive);

        var grouped = await query.GroupBy(o => o.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var data = grouped.OrderBy(g => g.Status)
            .Select(g => (IReadOnlyList<string>)[g.Status.ToString(), g.Count.ToString(CultureInfo.InvariantCulture)])
            .ToList();

        var released = await query.Where(o => o.Status == LabOrderStatus.Released && o.ReleasedAtUtc != null && o.CollectedAtUtc != null)
            .Select(o => new { Collected = o.CollectedAtUtc, Released = o.ReleasedAtUtc })
            .ToListAsync(ct);

        var avgHours = released.Count == 0 ? 0 : released.Average(o => (o.Released!.Value - o.Collected!.Value).TotalHours);

        return Build("lab-throughput", "Laboratory throughput", filter, grouped.Count, page, size,
            [new("status", "Status", "status"), new("orders", "Orders", "number")], data,
            [
                new("Released samples", released.Count.ToString(CultureInfo.InvariantCulture)),
                new("Average turnaround", avgHours == 0 ? "—" : avgHours.ToString("0.0", Culture) + " h")
            ]);
    }

    private async Task<ReportResult> LabPendingAsync(ReportFilter filter, int page, int size, CancellationToken ct)
    {
        var query = dbContext.LabOrders.AsNoTracking()
            .Where(o => o.Status != LabOrderStatus.Released && o.Status != LabOrderStatus.Cancelled);

        var total = await query.CountAsync(ct);
        var rows = await query.OrderBy(o => o.CreatedAtUtc).Skip((page - 1) * size).Take(size)
            .Select(o => new
            {
                o.Number,
                Patient = o.Patient!.FirstName + " " + o.Patient.LastName,
                o.CreatedAtUtc,
                o.Status,
                Tests = o.Items.Count
            }).ToListAsync(ct);

        var data = rows.Select(r => (IReadOnlyList<string>)
        [
            r.Number, r.Patient, r.CreatedAtUtc.ToString("yyyy-MM-dd HH:mm", Culture),
            r.Tests.ToString(CultureInfo.InvariantCulture), r.Status.ToString()
        ]).ToList();

        return Build("lab-pending", "Pending laboratory results", filter, total, page, size,
            [new("order", "Order", "text"), new("patient", "Patient", "text"), new("ordered", "Ordered", "datetime"), new("tests", "Tests", "number"), new("status", "Status", "status")], data,
            [new("Pending", total.ToString(CultureInfo.InvariantCulture))]);
    }

    private async Task<ReportResult> LabTestsAsync(ReportFilter filter, int page, int size, CancellationToken ct)
    {
        var query = dbContext.LabOrderItems.AsNoTracking().AsQueryable();
        if (filter.Range.FromUtc is not null) query = query.Where(i => i.LabOrder!.CreatedAtUtc >= filter.Range.FromUtc);
        if (filter.Range.ToUtcExclusive is not null) query = query.Where(i => i.LabOrder!.CreatedAtUtc < filter.Range.ToUtcExclusive);

        var grouped = await query.GroupBy(i => i.LabTest!.Name)
            .Select(g => new
            {
                Test = g.Key,
                Count = g.Count(),
                Abnormal = g.Count(i => i.Flag != ResultFlag.Normal)
            }).ToListAsync(ct);

        var ordered = grouped.OrderByDescending(g => g.Count).ToList();
        var data = ordered.Select(g => (IReadOnlyList<string>)
        [
            g.Test ?? "—",
            g.Count.ToString(CultureInfo.InvariantCulture),
            g.Abnormal.ToString(CultureInfo.InvariantCulture),
            g.Count == 0 ? "0%" : (g.Abnormal * 100d / g.Count).ToString("0.0", Culture) + "%"
        ]).ToList();

        return Build("lab-tests", "Tests requested", filter, ordered.Count, page, size,
            [new("test", "Test", "text"), new("requested", "Requested", "number"), new("abnormal", "Abnormal", "number"), new("abnormal-rate", "Abnormal rate", "percent")], data,
            [new("Distinct tests", ordered.Count.ToString(CultureInfo.InvariantCulture))]);
    }

    private async Task<ReportResult> LabCriticalAsync(ReportFilter filter, int page, int size, CancellationToken ct)
    {
        var query = dbContext.LabOrderItems.AsNoTracking().Where(i => i.Flag == ResultFlag.Critical);
        if (filter.Range.FromUtc is not null) query = query.Where(i => i.EnteredAtUtc >= filter.Range.FromUtc);
        if (filter.Range.ToUtcExclusive is not null) query = query.Where(i => i.EnteredAtUtc < filter.Range.ToUtcExclusive);

        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(i => i.EnteredAtUtc).Skip((page - 1) * size).Take(size)
            .Select(i => new
            {
                i.LabOrder!.Number,
                Test = i.LabTest!.Name,
                i.ResultValue,
                Unit = i.LabTest.Unit,
                i.EnteredAtUtc,
                Released = i.LabOrder.Status == LabOrderStatus.Released
            }).ToListAsync(ct);

        var data = rows.Select(r => (IReadOnlyList<string>)
        [
            r.Number, r.Test ?? "—", $"{r.ResultValue} {r.Unit}", r.EnteredAtUtc?.ToString("yyyy-MM-dd HH:mm", Culture) ?? "—",
            r.Released ? "Released" : "Pending release"
        ]).ToList();

        return Build("lab-critical", "Critical results", filter, total, page, size,
            [new("order", "Order", "text"), new("test", "Test", "text"), new("value", "Value", "text"), new("entered", "Entered", "datetime"), new("state", "State", "status")], data,
            [new("Critical values", total.ToString(CultureInfo.InvariantCulture))]);
    }

    private async Task<ReportResult> RevenueAsync(ReportFilter filter, int page, int size, CancellationToken ct)
    {
        var query = dbContext.Payments.AsNoTracking()
            .Where(p => p.ApprovedByUserId != null || !p.RequiresApproval);
        if (filter.Range.FromUtc is not null) query = query.Where(p => p.PaidAtUtc >= filter.Range.FromUtc);
        if (filter.Range.ToUtcExclusive is not null) query = query.Where(p => p.PaidAtUtc < filter.Range.ToUtcExclusive);

        var rows = await query.Select(p => new { p.PaidAtUtc, p.Amount, p.IsRefund }).ToListAsync(ct);
        var grouped = rows.GroupBy(p => p.PaidAtUtc.Date)
            .Select(g => new
            {
                Date = g.Key,
                Payments = g.Where(p => !p.IsRefund).Sum(p => p.Amount),
                Refunds = g.Where(p => p.IsRefund).Sum(p => p.Amount),
                Count = g.Count()
            })
            .OrderByDescending(g => g.Date).ToList();

        var data = grouped.Select(g => (IReadOnlyList<string>)
        [
            g.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            Money.Plain(g.Payments), Money.Plain(g.Refunds), Money.Plain(g.Payments - g.Refunds),
            g.Count.ToString(CultureInfo.InvariantCulture)
        ]).ToList();

        return Build("revenue", "Revenue by period", filter, grouped.Count, page, size,
            [new("date", "Date", "date"), new("payments", "Payments", "money"), new("refunds", "Refunds", "money"), new("net", "Net", "money"), new("transactions", "Transactions", "number")], data,
            [
                new("Net revenue", Money.Format(grouped.Sum(g => g.Payments - g.Refunds))),
                new("Transactions", grouped.Sum(g => g.Count).ToString(CultureInfo.InvariantCulture))
            ]);
    }

    private async Task<ReportResult> RevenueDepartmentAsync(ReportFilter filter, int page, int size, CancellationToken ct)
    {
        var lines = await dbContext.InvoiceLines.AsNoTracking()
            .Where(l => l.Invoice!.Status != InvoiceStatus.Void)
            .Select(l => new
            {
                Department = l.ServiceItem != null && l.ServiceItem.Department != null
                    ? l.ServiceItem.Department.Name
                    : l.ServiceItem != null ? l.ServiceItem.Category.ToString() : "Other",
                Amount = l.Quantity * l.UnitPrice
            }).ToListAsync(ct);

        var grouped = lines.GroupBy(l => l.Department).Select(g => new { Department = g.Key, Amount = g.Sum(x => x.Amount) })
            .OrderByDescending(g => g.Amount).ToList();

        var data = grouped.Select(g => (IReadOnlyList<string>)[g.Department ?? "Other", Money.Plain(g.Amount)]).ToList();

        return Build("revenue-department", "Revenue by department", filter, grouped.Count, page, size,
            [new("department", "Department", "text"), new("billed", "Billed", "money")], data,
            [new("Total billed", Money.Format(grouped.Sum(g => g.Amount)))]);
    }

    private async Task<ReportResult> OutstandingAsync(ReportFilter filter, int page, int size, CancellationToken ct)
    {
        var invoices = await dbContext.Invoices.AsNoTracking()
            .Where(i => i.Status == InvoiceStatus.Issued || i.Status == InvoiceStatus.PartiallyPaid)
            .Select(i => new
            {
                i.Number,
                Patient = i.Patient!.FirstName + " " + i.Patient.LastName,
                Mrn = i.Patient.Mrn,
                i.IssuedAtUtc,
                Total = i.Lines.Sum(l => l.Quantity * l.UnitPrice),
                Insurance = i.InsuranceCoveredAmount,
                Paid = i.Payments.Where(p => !p.IsRefund).Sum(p => p.Amount) - i.Payments.Where(p => p.IsRefund).Sum(p => p.Amount)
            }).ToListAsync(ct);

        var rows = invoices.Select(i => new { i.Number, i.Patient, i.Mrn, i.IssuedAtUtc, Balance = i.Total - i.Insurance - i.Paid })
            .Where(i => i.Balance > 0).OrderByDescending(i => i.Balance).ToList();

        var data = rows.Select(r => (IReadOnlyList<string>)
        [
            r.Number, r.Mrn ?? "—", r.Patient, r.IssuedAtUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), Money.Plain(r.Balance)
        ]).ToList();

        return Build("outstanding", "Outstanding invoices", filter, rows.Count, page, size,
            [new("invoice", "Invoice", "text"), new("mrn", "MRN", "text"), new("patient", "Patient", "text"), new("issued", "Issued", "date"), new("balance", "Balance", "money")], data,
            [new("Outstanding", Money.Format(rows.Sum(r => r.Balance)))]);
    }

    private async Task<ReportResult> PaymentsAsync(ReportFilter filter, int page, int size, CancellationToken ct)
    {
        var query = dbContext.Payments.AsNoTracking();
        if (filter.Range.FromUtc is not null) query = query.Where(p => p.PaidAtUtc >= filter.Range.FromUtc);
        if (filter.Range.ToUtcExclusive is not null) query = query.Where(p => p.PaidAtUtc < filter.Range.ToUtcExclusive);

        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(p => p.PaidAtUtc).Skip((page - 1) * size).Take(size)
            .Select(p => new
            {
                p.ReceiptNumber,
                Invoice = p.Invoice!.Number,
                Patient = p.Invoice.Patient!.FirstName + " " + p.Invoice.Patient.LastName,
                p.Amount,
                p.Method,
                p.IsRefund,
                p.PaidAtUtc,
                p.ApprovedByUserId
            }).ToListAsync(ct);

        var data = rows.Select(r => (IReadOnlyList<string>)
        [
            r.ReceiptNumber, r.Invoice, r.Patient, Money.Plain(r.Amount), r.Method.ToString(),
            r.PaidAtUtc.ToString("yyyy-MM-dd HH:mm", Culture), r.IsRefund ? "Refund" : "Payment",
            r.ApprovedByUserId is null && r.IsRefund ? "Awaiting approval" : "Approved"
        ]).ToList();

        return Build("payments", "Payments ledger", filter, total, page, size,
            [new("receipt", "Receipt", "text"), new("invoice", "Invoice", "text"), new("patient", "Patient", "text"), new("amount", "Amount", "money"), new("method", "Method", "text"), new("time", "Time", "datetime"), new("type", "Type", "text"), new("state", "State", "status")], data,
            [new("Transactions", total.ToString(CultureInfo.InvariantCulture))]);
    }

    private async Task<ReportResult> InventoryAsync(ReportFilter filter, int page, int size, CancellationToken ct)
    {
        var items = await dbContext.Medications.AsNoTracking()
            .Where(m => m.IsActive)
            .Select(m => new
            {
                m.Name,
                m.Strength,
                m.Form,
                OnHand = m.Batches.Sum(b => b.QuantityOnHand),
                m.ReorderLevel,
                m.UnitPrice
            }).ToListAsync(ct);

        var rows = items.OrderBy(i => i.OnHand).ToList();
        var data = rows.Select(i => (IReadOnlyList<string>)
        [
            i.Name, $"{i.Strength} {i.Form}".Trim(), i.OnHand.ToString(CultureInfo.InvariantCulture),
            i.ReorderLevel.ToString(CultureInfo.InvariantCulture),
            i.OnHand <= i.ReorderLevel ? "Reorder" : "OK",
            Money.Plain(i.OnHand * i.UnitPrice)
        ]).ToList();

        return Build("inventory", "Medication inventory", filter, rows.Count, page, size,
            [new("medication", "Medication", "text"), new("presentation", "Presentation", "text"), new("on-hand", "On hand", "number"), new("reorder-level", "Reorder level", "number"), new("state", "State", "status"), new("stock-value", "Stock value", "money")], data,
            [
                new("Items", rows.Count.ToString(CultureInfo.InvariantCulture)),
                new("Below reorder", rows.Count(i => i.OnHand <= i.ReorderLevel).ToString(CultureInfo.InvariantCulture)),
                new("Stock value", Money.Format(rows.Sum(i => i.OnHand * i.UnitPrice)))
            ]);
    }

    private async Task<ReportResult> ExpiryAsync(ReportFilter filter, int page, int size, CancellationToken ct)
    {
        var horizon = DateTime.UtcNow.Date.AddDays(90);
        var rows = await dbContext.StockBatches.AsNoTracking()
            .Where(b => b.QuantityOnHand > 0 && b.ExpiryDate <= horizon)
            .OrderBy(b => b.ExpiryDate)
            .Select(b => new { Medication = b.Medication!.Name, b.BatchNumber, b.ExpiryDate, b.QuantityOnHand })
            .ToListAsync(ct);

        var data = rows.Select(r => (IReadOnlyList<string>)
        [
            r.Medication ?? "—", r.BatchNumber, r.ExpiryDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            r.QuantityOnHand.ToString(CultureInfo.InvariantCulture),
            r.ExpiryDate < DateTime.UtcNow.Date ? "Expired" : "Expiring"
        ]).ToList();

        return Build("expiry", "Expiring stock", filter, rows.Count, page, size,
            [new("medication", "Medication", "text"), new("batch", "Batch", "text"), new("expiry", "Expiry", "date"), new("quantity", "Quantity", "number"), new("state", "State", "status")], data,
            [new("Batches", rows.Count.ToString(CultureInfo.InvariantCulture))]);
    }

    private async Task<ReportResult> PrescriptionsAsync(ReportFilter filter, int page, int size, CancellationToken ct)
    {
        var query = dbContext.PrescriptionItems.AsNoTracking().AsQueryable();
        if (filter.Range.FromUtc is not null) query = query.Where(i => i.Prescription!.CreatedAtUtc >= filter.Range.FromUtc);
        if (filter.Range.ToUtcExclusive is not null) query = query.Where(i => i.Prescription!.CreatedAtUtc < filter.Range.ToUtcExclusive);

        var grouped = await query.GroupBy(i => i.Medication!.Name)
            .Select(g => new { Medication = g.Key, Prescribed = g.Sum(i => i.Quantity), Dispensed = g.Sum(i => i.DispensedQuantity) })
            .ToListAsync(ct);

        var ordered = grouped.OrderByDescending(g => g.Prescribed).ToList();
        var data = ordered.Select(g => (IReadOnlyList<string>)
        [
            g.Medication ?? "—", g.Prescribed.ToString(CultureInfo.InvariantCulture), g.Dispensed.ToString(CultureInfo.InvariantCulture)
        ]).ToList();

        return Build("prescriptions", "Prescription register", filter, ordered.Count, page, size,
            [new("medication", "Medication", "text"), new("prescribed", "Prescribed", "number"), new("dispensed", "Dispensed", "number")], data,
            [new("Medications", ordered.Count.ToString(CultureInfo.InvariantCulture))]);
    }

    // ---------------------------------------------------------------- helpers

    private static IQueryable<Appointment> ApplyRange(IQueryable<Appointment> query, ReportFilter filter)
    {
        if (filter.Range.FromUtc is not null)
        {
            var from = filter.Range.From!.Value.ToDateTime(TimeOnly.MinValue);
            query = query.Where(a => a.AppointmentDate >= from);
        }

        if (filter.Range.To is not null)
        {
            var to = filter.Range.To!.Value.ToDateTime(TimeOnly.MinValue);
            query = query.Where(a => a.AppointmentDate <= to);
        }

        return query;
    }

    private static string Slot(int minutes) =>
        minutes <= 0 ? "—" : $"{minutes / 60:00}:{minutes % 60:00}";

    private static ReportResult Build(
        string key, string title, ReportFilter filter, int total, int page, int size,
        IReadOnlyList<ReportColumn> columns, IReadOnlyList<IReadOnlyList<string>> rows,
        IReadOnlyList<KeyValuePair<string, string>> summary)
    {
        var filterSummary = string.Join(" · ", new[]
        {
            filter.Range.From is null && filter.Range.To is null
                ? "All dates"
                : $"{filter.Range.From?.ToString("yyyy-MM-dd") ?? "…"} → {filter.Range.To?.ToString("yyyy-MM-dd") ?? "…"}",
            string.IsNullOrWhiteSpace(filter.Status) ? null : $"Status: {filter.Status}",
            string.IsNullOrWhiteSpace(filter.Search) ? null : $"Search: {filter.Search}"
        }.Where(part => !string.IsNullOrWhiteSpace(part)));

        return new ReportResult(key, title, null, columns, rows, summary, total, page, size, DateTime.UtcNow, filterSummary);
    }

    private static string Csv(string? value)
    {
        var text = value ?? string.Empty;
        return text.Contains(',') || text.Contains('"') || text.Contains('\n')
            ? $"\"{text.Replace("\"", "\"\"")}\""
            : text;
    }
}
