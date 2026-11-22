using System.Security.Claims;
using Hospital.BLL.Common;
using Hospital.BLL.Common.Rules;
using Hospital.BLL.Permissions;
using Hospital.BLL.Services.Abstraction;
using Hospital.DAL.DataBase;
using Hospital.DAL.Entities.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HospitalManagement.Controllers;

/// <summary>
/// Laboratory workspace. Specimen collection, result entry, verification and release are
/// separate permissions, and a result is only clinically final once released.
/// </summary>
[Authorize(Policy = "perm:" + Permission.LabCollect)]
public sealed class LaboratoryController(
    ILabService lab,
    IWorkspaceService workspace,
    HospitalDbContext dbContext,
    IAuditLogger audit) : Controller
{
    private string CurrentUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    public async Task<IActionResult> Index(string? status, string? term, string? patientId, int page = 1, CancellationToken cancellationToken = default)
    {
        LabOrderStatus? parsed = Enum.TryParse<LabOrderStatus>(status, true, out var value) ? value : null;
        var result = await lab.QueryAsync(parsed, patientId, new DateRange(null, null), term, page, 20, cancellationToken);

        ViewData["Title"] = "Laboratory";
        ViewData["Section"] = "Diagnostics";
        ViewData["Subtitle"] = "Specimen-to-release workflow with separate verification and authorisation.";

        ViewBag.Status = status;
        ViewBag.Term = term;
        ViewBag.PatientId = patientId;
        ViewBag.Kpis = await workspace.LaboratoryAsync(cancellationToken);
        ViewBag.CanCollect = User.HasClaim(AppClaimTypes.Permission, Permission.LabCollect);
        ViewBag.CanEnter = User.HasClaim(AppClaimTypes.Permission, Permission.LabEnterResult);
        ViewBag.CanRelease = User.HasClaim(AppClaimTypes.Permission, Permission.LabRelease);
        return View(result);
    }

    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var order = await lab.GetAsync(id, cancellationToken);
        if (order is null)
        {
            return NotFound();
        }

        await audit.RecordAsync("lab.order.view", CurrentUserId(), order.PatientId, "LabOrder", id.ToString(), cancellationToken);

        ViewData["Title"] = $"Laboratory order {order.Number}";
        ViewData["Section"] = "Diagnostics";
        ViewData["Subtitle"] = $"{order.Patient?.FirstName} {order.Patient?.LastName} · MRN {order.Patient?.Mrn ?? "—"}";

        ViewBag.CanEnter = User.HasClaim(AppClaimTypes.Permission, Permission.LabEnterResult);
        ViewBag.CanVerify = User.HasClaim(AppClaimTypes.Permission, Permission.LabVerify);
        ViewBag.CanRelease = User.HasClaim(AppClaimTypes.Permission, Permission.LabRelease);
        ViewBag.CanCollect = User.HasClaim(AppClaimTypes.Permission, Permission.LabCollect);
        return View(order);
    }

    [HttpPost]
    [Authorize(Policy = "perm:" + Permission.LabOrderCreate)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateOrder(int encounterId, int[] labTestIds, string? clinicalNotes, CancellationToken cancellationToken)
    {
        var result = await lab.CreateOrderAsync(encounterId, labTestIds, CurrentUserId(), clinicalNotes, cancellationToken);
        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = result.Error;
            return RedirectToAction("Details", "Encounters", new { id = encounterId });
        }

        await audit.RecordAsync("lab.order.create", CurrentUserId(), null, "LabOrder", result.Value!.Id.ToString(), cancellationToken);
        TempData["StatusMessage"] = $"Laboratory order {result.Value.Number} created and awaiting specimen collection.";
        return RedirectToAction(nameof(Details), new { id = result.Value.Id });
    }

    [HttpPost]
    [Authorize(Policy = "perm:" + Permission.LabCollect)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Transition(int id, LabOrderStatus target, string? specimenId, CancellationToken cancellationToken)
    {
        var result = await lab.TransitionAsync(id, target, CurrentUserId(), specimenId, cancellationToken);
        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = result.Error;
            return RedirectToAction(nameof(Details), new { id });
        }

        await audit.RecordAsync($"lab.order.{target.ToString().ToLowerInvariant()}", CurrentUserId(), null, "LabOrder", id.ToString(), cancellationToken);
        TempData["StatusMessage"] = target switch
        {
            LabOrderStatus.Collected => "Specimen collected and labelled.",
            LabOrderStatus.Received => "Specimen received in the laboratory.",
            LabOrderStatus.Processing => "Analysis started.",
            LabOrderStatus.AwaitingVerification => "Results submitted for verification.",
            LabOrderStatus.Released => "Results verified and released to the clinical record.",
            _ => "Laboratory order updated."
        };
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [Authorize(Policy = "perm:" + Permission.LabEnterResult)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EnterResult(int id, int orderItemId, string? value, decimal? numericValue, string? comment, CancellationToken cancellationToken)
    {
        var result = await lab.EnterResultAsync(orderItemId, value, numericValue, CurrentUserId(), comment, cancellationToken);
        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = result.Error;
        }
        else
        {
            await audit.RecordAsync("lab.result.enter", CurrentUserId(), null, "LabOrderItem", orderItemId.ToString(), cancellationToken);
            TempData["StatusMessage"] = "Result saved. It is not visible as final until the order is released.";
        }

        return RedirectToAction(nameof(Details), new { id });
    }

}
