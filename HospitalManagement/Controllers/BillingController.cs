using System.Security.Claims;
using Hospital.BLL.Common;
using Hospital.BLL.Permissions;
using Hospital.BLL.Services.Abstraction;
using Hospital.DAL.DataBase;
using Hospital.DAL.Entities.Billing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HospitalManagement.Controllers;

/// <summary>
/// Billing workspace: invoices, payments and refunds. Clinical and financial permissions are
/// deliberately separate, and large refunds require a second officer's approval.
/// </summary>
[Authorize(Policy = "perm:" + Permission.BillingView)]
public sealed class BillingController(
    IBillingService billing,
    IWorkspaceService workspace,
    IOrganizationDirectoryService directory,
    IPatientLookupService lookup,
    HospitalDbContext dbContext,
    IAuditLogger audit) : Controller
{
    private string CurrentUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    public async Task<IActionResult> Index(string? status, DateOnly? from, DateOnly? to, string? term, int page = 1, CancellationToken cancellationToken = default)
    {
        InvoiceStatus? parsed = Enum.TryParse<InvoiceStatus>(status, true, out var value) ? value : null;
        var range = new DateRange(from, to);
        var result = await billing.QueryAsync(parsed, range, term, page, 20, cancellationToken);

        ViewData["Title"] = "Billing";
        ViewData["Section"] = "Administration";
        ViewData["Subtitle"] = "Invoices, payments, refunds and outstanding balances.";

        ViewBag.Kpis = await workspace.BillingAsync(cancellationToken);
        ViewBag.Status = status;
        ViewBag.Term = term;
        ViewBag.From = from?.ToString("yyyy-MM-dd");
        ViewBag.To = to?.ToString("yyyy-MM-dd");
        ViewBag.PendingRefunds = await dbContext.Payments.AsNoTracking()
            .Include(p => p.Invoice).ThenInclude(i => i!.Patient)
            .Where(p => p.IsRefund && p.RequiresApproval && p.ApprovedByUserId == null)
            .OrderBy(p => p.PaidAtUtc)
            .ToListAsync(cancellationToken);
        ViewBag.CanManage = User.HasClaim(AppClaimTypes.Permission, Permission.BillingManage);
        ViewBag.CanApproveRefund = User.HasClaim(AppClaimTypes.Permission, Permission.BillingRefund);
        return View(result);
    }

    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var invoice = await billing.GetAsync(id, cancellationToken);
        if (invoice is null)
        {
            return NotFound();
        }

        await audit.RecordAsync("invoice.view", CurrentUserId(), invoice.PatientId, "Invoice", id.ToString(), cancellationToken);

        ViewData["Title"] = $"Invoice {invoice.Number}";
        ViewData["Section"] = "Administration";
        ViewData["Subtitle"] = $"{invoice.Patient?.FirstName} {invoice.Patient?.LastName} · MRN {invoice.Patient?.Mrn ?? "—"}";

        ViewBag.CanManage = User.HasClaim(AppClaimTypes.Permission, Permission.BillingManage);
        return View(invoice);
    }

    public async Task<IActionResult> Create(string? patientId, CancellationToken cancellationToken)
    {
        ViewData["Title"] = "New invoice";
        ViewData["Section"] = "Administration";
        ViewBag.Patients = await lookup.SearchAsync(null, 1, 50, cancellationToken);
        ViewBag.SelectedPatient = patientId;
        ViewBag.Services = (await directory.SnapshotAsync(cancellationToken)).ServiceItems;
        return View();
    }

    [HttpPost]
    [Authorize(Policy = "perm:" + Permission.BillingManage)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string patientId, int? serviceItemId, string description, int quantity, decimal unitPrice, CancellationToken cancellationToken)
    {
        var lines = new[]
        {
            new AddInvoiceLine(
                string.IsNullOrWhiteSpace(description) ? "Service" : description,
                Math.Max(1, quantity),
                Math.Max(0, unitPrice),
                serviceItemId)
        };

        var result = await billing.CreateInvoiceAsync(patientId, null, lines, CurrentUserId(), cancellationToken);
        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = result.Error;
            return RedirectToAction(nameof(Create), new { patientId });
        }

        await audit.RecordAsync("invoice.create", CurrentUserId(), patientId, "Invoice", result.Value!.Id.ToString(), cancellationToken);
        TempData["StatusMessage"] = $"Invoice {result.Value.Number} issued.";
        return RedirectToAction(nameof(Details), new { id = result.Value.Id });
    }

    [HttpPost]
    [Authorize(Policy = "perm:" + Permission.BillingManage)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Pay(int id, decimal amount, PaymentMethod method, string? reference, CancellationToken cancellationToken)
    {
        var result = await billing.RecordPaymentAsync(id, amount, method, reference, CurrentUserId(), cancellationToken);
        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = result.Error;
        }
        else
        {
            await audit.RecordAsync("payment.record", CurrentUserId(), null, "Invoice", id.ToString(), cancellationToken);
            TempData["StatusMessage"] = $"Payment recorded. Receipt {result.Value!.ReceiptNumber}.";
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [Authorize(Policy = "perm:" + Permission.BillingManage)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Refund(int id, decimal amount, string reason, CancellationToken cancellationToken)
    {
        var result = await billing.RecordRefundAsync(id, amount, reason, CurrentUserId(), cancellationToken);
        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = result.Error;
        }
        else
        {
            await audit.RecordAsync("payment.refund", CurrentUserId(), null, "Invoice", id.ToString(), cancellationToken);
            TempData["StatusMessage"] = result.Value!.RequiresApproval
                ? "Refund recorded and awaiting a second officer's approval."
                : "Refund recorded.";
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [Authorize(Policy = "perm:" + Permission.BillingRefund)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveRefund(int paymentId, CancellationToken cancellationToken)
    {
        var result = await billing.ApproveRefundAsync(paymentId, CurrentUserId(), cancellationToken);
        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = result.Error;
        }
        else
        {
            await audit.RecordAsync("payment.refund.approve", CurrentUserId(), null, "Payment", paymentId.ToString(), cancellationToken);
            TempData["StatusMessage"] = "Refund approved.";
        }

        return RedirectToAction(nameof(Index));
    }
}
