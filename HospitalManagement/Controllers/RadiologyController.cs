using System.Security.Claims;
using Hospital.BLL.Common;
using Hospital.BLL.Permissions;
using Hospital.BLL.Services.Abstraction;
using Hospital.DAL.Entities.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Hospital.BLL.Helpers;

namespace HospitalManagement.Controllers;

/// <summary>
/// Radiology worklist. Findings and impressions are written by the radiologist; the system
/// never generates clinical text. A report is only final after verification and release.
/// </summary>
[Authorize(Policy = "perm:" + Permission.ImagingView)]
public sealed class RadiologyController(
    IImagingService imaging,
    IAuditLogger audit) : Controller
{
    private string CurrentUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    public async Task<IActionResult> Index(string? status, string? term, int page = 1, CancellationToken cancellationToken = default)
    {
        ImagingOrderStatus? parsed = Enum.TryParse<ImagingOrderStatus>(status, true, out var value) ? value : null;
        var result = await imaging.QueryAsync(parsed, term, page, 20, cancellationToken);

        ViewData["Title"] = "Radiology";
        ViewData["Section"] = "Diagnostics";
        ViewData["Subtitle"] = "Imaging worklist from request to released report.";

        ViewBag.Status = status;
        ViewBag.Term = term;
        ViewBag.Kpis = await BuildKpisAsync(cancellationToken);
        ViewBag.CanReport = User.HasClaim(AppClaimTypes.Permission, Permission.ImagingReport);
        ViewBag.CanVerify = User.HasClaim(AppClaimTypes.Permission, Permission.ImagingVerify);
        ViewBag.CanManage = User.HasClaim(AppClaimTypes.Permission, Permission.ImagingManage);
        return View(result);
    }

    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var result = await imaging.GetAsync(id, cancellationToken);
        if (!result.Succeeded)
        {
            return NotFound();
        }

        var order = result.Value!;
        await audit.RecordAsync("imaging.view", CurrentUserId(), order.PatientId, "ImagingOrder", id.ToString(), cancellationToken);

        ViewData["Title"] = $"Imaging {order.Number}";
        ViewData["Section"] = "Diagnostics";
        ViewData["Subtitle"] = $"{order.Patient?.FirstName} {order.Patient?.LastName} · MRN {order.Patient?.Mrn ?? "—"}";

        ViewBag.CanReport = User.HasClaim(AppClaimTypes.Permission, Permission.ImagingReport);
        ViewBag.CanVerify = User.HasClaim(AppClaimTypes.Permission, Permission.ImagingVerify);
        ViewBag.CanRelease = User.HasClaim(AppClaimTypes.Permission, Permission.ImagingRelease);
        ViewBag.CanManage = User.HasClaim(AppClaimTypes.Permission, Permission.ImagingManage);
        return View(order);
    }

    [HttpPost]
    [Authorize(Policy = "perm:" + Permission.ImagingOrder)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Request(int encounterId, string modality, string studyName, string? indication, CancellationToken cancellationToken)
    {
        var result = await imaging.RequestAsync(encounterId, modality, studyName, indication, CurrentUserId(), cancellationToken);
        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = result.Error;
        }
        else
        {
            await audit.RecordAsync("imaging.request", CurrentUserId(), null, "ImagingOrder", result.Value!.Id.ToString(), cancellationToken);
            TempData["StatusMessage"] = $"Imaging request {result.Value.Number} created.";
        }

        return RedirectToAction("Details", "Encounters", new { id = encounterId });
    }

    [HttpPost]
    [Authorize(Policy = "perm:" + Permission.ImagingManage)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Transition(int id, string action, DateTime? scheduledFor, string? findings, string? impression, CancellationToken cancellationToken)
    {
        Result result;
        string? eventType = null;

        switch (action)
        {
            case "schedule" when scheduledFor is not null:
                result = await imaging.ScheduleAsync(id, scheduledFor.Value.ToUniversalTime(), CurrentUserId(), cancellationToken);
                eventType = "imaging.schedule";
                break;
            case "perform":
                result = await imaging.MarkPerformedAsync(id, CurrentUserId(), cancellationToken);
                eventType = "imaging.perform";
                break;
            case "verify":
                result = await imaging.VerifyAsync(id, CurrentUserId(), cancellationToken);
                eventType = "imaging.verify";
                break;
            case "release":
                result = await imaging.ReleaseAsync(id, CurrentUserId(), cancellationToken);
                eventType = "imaging.release";
                break;
            case "report":
                result = await imaging.SaveReportAsync(id, findings ?? string.Empty, impression ?? string.Empty, CurrentUserId(), cancellationToken);
                eventType = "imaging.report";
                break;
            default:
                result = Result.Fail("Unknown action.", "invalid_action");
                break;
        }

        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = result.Error;
        }
        else if (eventType is not null)
        {
            await audit.RecordAsync(eventType, CurrentUserId(), null, "ImagingOrder", id.ToString(), cancellationToken);
            TempData["StatusMessage"] = "Imaging order updated.";
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task<IReadOnlyList<Kpi>> BuildKpisAsync(CancellationToken cancellationToken)
    {
        var pending = await imaging.QueryAsync(ImagingOrderStatus.Requested, null, 1, 1, cancellationToken);
        var scheduled = await imaging.QueryAsync(ImagingOrderStatus.Scheduled, null, 1, 1, cancellationToken);
        var reported = await imaging.QueryAsync(ImagingOrderStatus.Reported, null, 1, 1, cancellationToken);
        var verified = await imaging.QueryAsync(ImagingOrderStatus.Verified, null, 1, 1, cancellationToken);

        return
        [
            new Kpi("Requested", pending.TotalCount.ToString(), "Awaiting scheduling", "info"),
            new Kpi("Scheduled", scheduled.TotalCount.ToString(), "On the scanner list", "teal"),
            new Kpi("Awaiting report", reported.TotalCount.ToString(), "Performed, draft pending", "warning"),
            new Kpi("Awaiting release", verified.TotalCount.ToString(), "Verified, not released", "purple")
        ];
    }
}
