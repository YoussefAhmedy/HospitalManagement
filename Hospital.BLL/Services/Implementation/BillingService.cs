using Hospital.BLL.Common;
using Hospital.BLL.Common.Rules;
using Hospital.BLL.Services.Abstraction;
using Hospital.DAL.DataBase;
using Hospital.DAL.Entities.Billing;
using Microsoft.EntityFrameworkCore;

namespace Hospital.BLL.Services.Implementation;

public sealed class BillingService(
    HospitalDbContext dbContext,
    INumberSequenceService numbers) : IBillingService
{
    public async Task<Result<Invoice>> CreateInvoiceAsync(
        string patientId, int? encounterId, IReadOnlyCollection<AddInvoiceLine> lines,
        string actorUserId, CancellationToken cancellationToken = default)
    {
        if (lines.Count == 0)
        {
            return Result<Invoice>.Fail("An invoice needs at least one line.", "empty");
        }

        if (lines.Any(line => line.Quantity <= 0 || line.UnitPrice < 0))
        {
            return Result<Invoice>.Fail("Line quantities must be positive and prices non-negative.", "invalid_line");
        }

        if (!await dbContext.Patients.AnyAsync(p => p.Id == patientId, cancellationToken))
        {
            return Result<Invoice>.Fail("Patient not found.", "patient_not_found");
        }

        var policy = await dbContext.InsurancePolicies.AsNoTracking()
            .Where(p => p.PatientId == patientId && p.IsActive)
            .OrderByDescending(p => p.ValidFrom)
            .FirstOrDefaultAsync(cancellationToken);

        var invoice = new Invoice
        {
            Number = await numbers.NextFormattedAsync(ClinicalNumber.Invoice, "invoice", cancellationToken),
            PatientId = patientId,
            EncounterId = encounterId,
            Status = InvoiceStatus.Issued,
            IssuedAtUtc = DateTime.UtcNow,
            DueAtUtc = DateTime.UtcNow.AddDays(14),
            Lines = lines.Select(line => new InvoiceLine
            {
                Description = line.Description.Length > 200 ? line.Description[..200] : line.Description,
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice,
                ServiceItemId = line.ServiceItemId
            }).ToList()
        };

        var total = invoice.Lines.Sum(line => line.Amount);
        invoice.InsuranceCoveredAmount = policy is null
            ? 0
            : BillingRules.InsuranceCoverage(total, policy.CoveragePercent);

        dbContext.Invoices.Add(invoice);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<Invoice>.Ok(invoice);
    }

    public async Task<Result<Payment>> RecordPaymentAsync(
        int invoiceId, decimal amount, PaymentMethod method, string? reference,
        string actorUserId, CancellationToken cancellationToken = default)
    {
        var invoice = await dbContext.Invoices
            .Include(i => i.Lines)
            .Include(i => i.Payments)
            .FirstOrDefaultAsync(i => i.Id == invoiceId, cancellationToken);

        if (invoice is null)
        {
            return Result<Payment>.Fail("Invoice not found.", "not_found");
        }

        if (invoice.Status == InvoiceStatus.Void)
        {
            return Result<Payment>.Fail("A void invoice cannot accept payments.", "void");
        }

        var balance = BillingRules.Balance(
            invoice.Lines.Sum(l => l.Amount),
            invoice.InsuranceCoveredAmount,
            invoice.Payments.Where(p => !p.IsRefund).Sum(p => p.Amount) - invoice.Payments.Where(p => p.IsRefund).Sum(p => p.Amount));

        if (!BillingRules.IsPaymentValid(amount, balance))
        {
            return Result<Payment>.Fail($"Payment must be greater than zero and no more than the outstanding balance of {balance:0.00}.", "invalid_amount");
        }

        var payment = new Payment
        {
            InvoiceId = invoiceId,
            ReceiptNumber = await numbers.NextFormattedAsync(ClinicalNumber.Receipt, "receipt", cancellationToken),
            Amount = amount,
            Method = method,
            Reference = reference?.Length > 120 ? reference[..120] : reference,
            PaidAtUtc = DateTime.UtcNow,
            ReceivedByUserId = actorUserId
        };

        dbContext.Payments.Add(payment);
        invoice.Status = BillingRules.DeriveStatus(
            invoice.Lines.Sum(l => l.Amount),
            invoice.InsuranceCoveredAmount,
            invoice.Payments.Where(p => !p.IsRefund).Sum(p => p.Amount) + amount
                - invoice.Payments.Where(p => p.IsRefund).Sum(p => p.Amount),
            isVoid: false);

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<Payment>.Ok(payment);
    }

    public async Task<Result<Payment>> RecordRefundAsync(
        int invoiceId, decimal amount, string reason, string actorUserId,
        CancellationToken cancellationToken = default)
    {
        var invoice = await dbContext.Invoices
            .Include(i => i.Lines)
            .Include(i => i.Payments)
            .FirstOrDefaultAsync(i => i.Id == invoiceId, cancellationToken);

        if (invoice is null)
        {
            return Result<Payment>.Fail("Invoice not found.", "not_found");
        }

        var paidNet = invoice.Payments.Where(p => !p.IsRefund).Sum(p => p.Amount)
                      - invoice.Payments.Where(p => p.IsRefund).Sum(p => p.Amount);

        if (amount <= 0 || amount > paidNet)
        {
            return Result<Payment>.Fail($"Refund must be greater than zero and no more than the net paid amount of {paidNet:0.00}.", "invalid_amount");
        }

        var requiresApproval = BillingRules.RefundRequiresApproval(amount);
        var refund = new Payment
        {
            InvoiceId = invoiceId,
            ReceiptNumber = await numbers.NextFormattedAsync(ClinicalNumber.Receipt, "receipt", cancellationToken),
            Amount = amount,
            Method = PaymentMethod.Other,
            Reference = reason?.Length > 120 ? reason[..120] : reason,
            IsRefund = true,
            PaidAtUtc = DateTime.UtcNow,
            ReceivedByUserId = actorUserId,
            RequiresApproval = requiresApproval,
            ApprovedByUserId = requiresApproval ? null : actorUserId,
            ApprovedAtUtc = requiresApproval ? null : DateTime.UtcNow
        };

        dbContext.Payments.Add(refund);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<Payment>.Ok(refund);
    }

    public async Task<Result> ApproveRefundAsync(int paymentId, string approverUserId, CancellationToken cancellationToken = default)
    {
        var payment = await dbContext.Payments.FirstOrDefaultAsync(p => p.Id == paymentId, cancellationToken);
        if (payment is null || !payment.IsRefund)
        {
            return Result.Fail("Refund not found.", "not_found");
        }

        if (payment.ApprovedByUserId is not null)
        {
            return Result.Fail("This refund is already approved.", "already_approved");
        }

        if (string.Equals(payment.ReceivedByUserId, approverUserId, StringComparison.Ordinal))
        {
            return Result.Fail("A refund cannot be approved by the officer who recorded it.", "segregation_of_duties");
        }

        payment.ApprovedByUserId = approverUserId;
        payment.ApprovedAtUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }

    public Task<Invoice?> GetAsync(int invoiceId, CancellationToken cancellationToken = default) =>
        dbContext.Invoices
            .AsNoTracking()
            .Include(i => i.Patient)
            .Include(i => i.Lines)
            .Include(i => i.Payments)
            .FirstOrDefaultAsync(i => i.Id == invoiceId, cancellationToken);

    public async Task<PagedResult<InvoiceListItem>> ForPatientAsync(
        int patientId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        // Filter on the foreign key, never on a name. Two patients may share a surname; a name
        // search would then disclose one patient's financial record to the other.
        var query = dbContext.Invoices
            .AsNoTracking()
            .Where(i => i.PatientId == patientId);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(i => i.IssuedAtUtc)
            .Skip((Math.Max(page, 1) - 1) * pageSize)
            .Take(pageSize)
            .Select(i => new InvoiceListItem(
                i.Id, i.Number, i.Patient!.Mrn ?? "—",
                (i.Patient.FirstName + " " + i.Patient.LastName),
                i.IssuedAtUtc, i.Status, i.Total, i.InsuranceCovered, i.Paid, i.Balance))
            .ToListAsync(cancellationToken);

        return new PagedResult<InvoiceListItem>(items, total, Math.Max(page, 1), pageSize);
    }

    public async Task<PagedResult<InvoiceListItem>> QueryAsync(
        InvoiceStatus? status, DateRange range, string? term, int page, int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Invoices.AsNoTracking().AsQueryable();

        if (status is not null)
        {
            query = query.Where(i => i.Status == status);
        }

        if (range.FromUtc is not null)
        {
            query = query.Where(i => i.IssuedAtUtc >= range.FromUtc);
        }

        if (range.ToUtcExclusive is not null)
        {
            query = query.Where(i => i.IssuedAtUtc < range.ToUtcExclusive);
        }

        if (!string.IsNullOrWhiteSpace(term))
        {
            var trimmed = term.Trim();
            query = query.Where(i =>
                i.Number.Contains(trimmed) ||
                (i.Patient != null && (i.Patient.FirstName.Contains(trimmed) || i.Patient.LastName.Contains(trimmed))) ||
                (i.Patient != null && i.Patient.Mrn != null && i.Patient.Mrn.Contains(trimmed)));
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(i => i.IssuedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(i => new InvoiceListItem(
                i.Id,
                i.Number,
                i.Patient!.Mrn ?? "—",
                i.Patient.FirstName + " " + i.Patient.LastName,
                i.IssuedAtUtc,
                i.Status,
                i.Lines.Sum(l => l.Quantity * l.UnitPrice),
                i.InsuranceCoveredAmount,
                i.Payments.Where(p => !p.IsRefund).Sum(p => p.Amount) - i.Payments.Where(p => p.IsRefund).Sum(p => p.Amount),
                i.Lines.Sum(l => l.Quantity * l.UnitPrice) - i.InsuranceCoveredAmount
                    - (i.Payments.Where(p => !p.IsRefund).Sum(p => p.Amount) - i.Payments.Where(p => p.IsRefund).Sum(p => p.Amount))))
            .ToListAsync(cancellationToken);

        return new PagedResult<InvoiceListItem>(items, total, page, pageSize);
    }

    public async Task<RevenueSummary> RevenueAsync(CancellationToken cancellationToken = default)
    {
        var today = DateTime.UtcNow.Date;
        var monthStart = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        // Only approved, non-refunded payments count as revenue.
        var payments = await dbContext.Payments.AsNoTracking()
            .Where(p => p.ApprovedByUserId != null || !p.RequiresApproval)
            .Select(p => new { p.Amount, p.IsRefund, p.PaidAtUtc })
            .ToListAsync(cancellationToken);

        var revenueToday = payments.Where(p => p.PaidAtUtc >= today)
            .Sum(p => p.IsRefund ? -p.Amount : p.Amount);
        var revenueMonth = payments.Where(p => p.PaidAtUtc >= monthStart)
            .Sum(p => p.IsRefund ? -p.Amount : p.Amount);

        var invoices = await dbContext.Invoices.AsNoTracking()
            .Where(i => i.Status != InvoiceStatus.Void)
            .Select(i => new
            {
                Total = i.Lines.Sum(l => l.Quantity * l.UnitPrice),
                Insurance = i.InsuranceCoveredAmount,
                Paid = i.Payments.Where(p => !p.IsRefund).Sum(p => p.Amount)
                       - i.Payments.Where(p => p.IsRefund).Sum(p => p.Amount),
                Status = i.Status
            })
            .ToListAsync(cancellationToken);

        var outstanding = invoices
            .Select(i => i.Total - i.Insurance - i.Paid)
            .Where(balance => balance > 0)
            .Sum();

        var open = invoices.Count(i => i.Status is InvoiceStatus.Issued or InvoiceStatus.PartiallyPaid);
        var average = invoices.Count == 0 ? 0 : Math.Round(invoices.Average(i => i.Total), 2);

        return new RevenueSummary(revenueToday, revenueMonth, outstanding, open, average);
    }
}
