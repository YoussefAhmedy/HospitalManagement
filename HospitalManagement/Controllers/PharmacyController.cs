using System.Security.Claims;
using Hospital.BLL.Common;
using Hospital.BLL.Permissions;
using Hospital.BLL.Services.Abstraction;
using Hospital.DAL.DataBase;
using Hospital.DAL.Entities.Pharmacy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HospitalManagement.Controllers;

/// <summary>Pharmacy workspace: prescription review, dispensing and stock visibility.</summary>
[Authorize]
public sealed class PharmacyController(
    IPharmacyService pharmacy,
    IWorkspaceService workspace,
    HospitalDbContext dbContext,
    IAuditLogger audit) : Controller
{
    private string CurrentUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    private bool CanReview => User.HasClaim(AppClaimTypes.Permission, Permission.PrescriptionReview);
    private bool CanDispense => User.HasClaim(AppClaimTypes.Permission, Permission.PrescriptionDispense);
    private bool CanPrescribe => User.HasClaim(AppClaimTypes.Permission, Permission.PrescriptionCreate);

    [Authorize(Policy = "perm:" + Permission.PrescriptionReview)]
    public async Task<IActionResult> Index(string? status, string? term, int page = 1, CancellationToken cancellationToken = default)
    {
        var query = dbContext.Prescriptions.AsNoTracking()
            .Include(p => p.Patient)
            .Include(p => p.Items).ThenInclude(i => i.Medication)
            .AsQueryable();

        if (Enum.TryParse<PrescriptionStatus>(status, true, out var parsed))
        {
            query = query.Where(p => p.Status == parsed);
        }

        if (!string.IsNullOrWhiteSpace(term))
        {
            var trimmed = term.Trim();
            query = query.Where(p => p.Number.Contains(trimmed)
                || (p.Patient != null && (p.Patient.FirstName.Contains(trimmed) || p.Patient.LastName.Contains(trimmed))));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(p => p.CreatedAtUtc)
            .Skip((Math.Max(1, page) - 1) * 20).Take(20)
            .ToListAsync(cancellationToken);

        ViewData["Title"] = "Pharmacy";
        ViewData["Section"] = "Clinical";
        ViewData["Subtitle"] = "Pharmacist review, dispensing and stock control.";

        ViewBag.Status = status;
        ViewBag.Term = term;
        ViewBag.Kpis = await workspace.PharmacyAsync(cancellationToken);
        ViewBag.Prescriptions = new PagedResult<Prescription>(items, total, Math.Max(1, page), 20);
        return View();
    }

    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var prescription = await dbContext.Prescriptions.AsNoTracking()
            .Include(p => p.Patient)
            .Include(p => p.Encounter)
            .Include(p => p.Items).ThenInclude(i => i.Medication)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (prescription is null)
        {
            return NotFound();
        }

        await audit.RecordAsync("prescription.view", CurrentUserId(), prescription.PatientId, "Prescription", id.ToString(), cancellationToken);

        ViewData["Title"] = $"Prescription {prescription.Number}";
        ViewData["Section"] = "Clinical";
        ViewData["Subtitle"] = $"{prescription.Patient?.FirstName} {prescription.Patient?.LastName} · MRN {prescription.Patient?.Mrn ?? "—"}";

        ViewBag.CanReview = CanReview;
        ViewBag.CanDispense = CanDispense;
        return View(prescription);
    }

    [HttpPost]
    [Authorize(Policy = "perm:" + Permission.PrescriptionCreate)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(int encounterId, int[] medicationIds, string dose, string frequency, string duration, string? route, CancellationToken cancellationToken)
    {
        var result = await pharmacy.CreateAsync(encounterId, medicationIds, CurrentUserId(), dose, frequency, duration, route, cancellationToken);
        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = result.Error;
        }
        else
        {
            await audit.RecordAsync("prescription.create", CurrentUserId(), null, "Prescription", result.Value!.Id.ToString(), cancellationToken);
            TempData["StatusMessage"] = $"Prescription {result.Value.Number} sent to pharmacy for review.";
        }

        return RedirectToAction("Details", "Encounters", new { id = encounterId });
    }

    [HttpPost]
    [Authorize(Policy = "perm:" + Permission.PrescriptionReview)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Review(int id, bool approve, string? note, CancellationToken cancellationToken)
    {
        var result = await pharmacy.ReviewAsync(id, approve, CurrentUserId(), note, cancellationToken);
        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = result.Error;
        }
        else
        {
            await audit.RecordAsync(approve ? "prescription.approve" : "prescription.reject", CurrentUserId(), null, "Prescription", id.ToString(), cancellationToken);
            TempData["StatusMessage"] = approve ? "Prescription reviewed and approved for dispensing." : "Prescription rejected with a pharmacist note.";
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [Authorize(Policy = "perm:" + Permission.PrescriptionDispense)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Dispense(int id, int[] itemIds, CancellationToken cancellationToken)
    {
        var prescription = await dbContext.Prescriptions
            .Include(p => p.Items)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (prescription is null)
        {
            return NotFound();
        }

        var lines = new List<DispenseLineRequest>();
        foreach (var itemId in itemIds)
        {
            var quantityText = Request.Form[$"quantity_{itemId}"].ToString();
            var quantity = int.TryParse(quantityText, out var parsed)
                ? parsed
                : prescription.Items.FirstOrDefault(i => i.Id == itemId) is { } item
                    ? item.Quantity - item.DispensedQuantity
                    : 0;
            if (quantity > 0)
            {
                lines.Add(new DispenseLineRequest(itemId, quantity));
            }
        }

        var result = await pharmacy.DispenseAsync(id, lines, CurrentUserId(), cancellationToken);
        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = result.Error;
        }
        else
        {
            await audit.RecordAsync("prescription.dispense", CurrentUserId(), null, "Prescription", id.ToString(), cancellationToken);
            TempData["StatusMessage"] = "Dispensation recorded and stock updated.";
        }

        return RedirectToAction(nameof(Details), new { id });
    }
}
