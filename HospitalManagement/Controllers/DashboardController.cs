using System.Security.Claims;
using Hospital.BLL.Common;
using Hospital.BLL.Helpers;
using Hospital.BLL.Permissions;
using Hospital.BLL.Services.Abstraction;
using Hospital.DAL.Entities.OwnedTypes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HospitalManagement.Controllers;

/// <summary>
/// The landing workspace. The controller routes each signed-in identity to the workspace
/// that matches its operational role; every figure comes from a live query.
/// </summary>
[Authorize]
public sealed class DashboardController(
    IWorkspaceService workspace,
    IAppointmentSchedulingService scheduling,
    IEncounterService encounters,
    ILabService lab,
    IAdmissionService admissions,
    IReportService reports) : Controller
{
    private string CurrentUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Overview";
        ViewData["Section"] = "Workspace";

        if (User.IsInRole(nameof(Role.Patient)))
        {
            return RedirectToAction("Index", "Patient");
        }

        if (User.IsInRole(nameof(Role.Doctor)))
        {
            var kpis = await workspace.DoctorAsync(CurrentUserId(), cancellationToken);
            var queue = await scheduling.QueueAsync(DateTime.UtcNow.Date, cancellationToken);
            ViewData["Subtitle"] = "Your clinic at a glance: today's list, waiting patients and open documentation.";
            ViewBag.Kpis = kpis;
            ViewBag.Queue = queue;
            return View("Clinician");
        }

        if (User.IsInRole(nameof(Role.Receptionist)))
        {
            ViewBag.Kpis = await workspace.ReceptionAsync(cancellationToken);
            ViewBag.Queue = await scheduling.QueueAsync(DateTime.UtcNow.Date, cancellationToken);
            ViewData["Subtitle"] = "Registration, check-in and today's appointment flow.";
            return View("Reception");
        }

        if (User.IsInRole(nameof(Role.LabTechnician)))
        {
            return RedirectToAction("Index", "Laboratory");
        }

        if (User.IsInRole(nameof(Role.Pharmacist)))
        {
            return RedirectToAction("Index", "Pharmacy");
        }

        if (User.IsInRole(nameof(Role.BillingOfficer)))
        {
            return RedirectToAction("Index", "Billing");
        }

        if (User.IsInRole(nameof(Role.Nurse)))
        {
            ViewBag.Kpis = await workspace.ReceptionAsync(cancellationToken);
            var triage = await encounters.QueryAsync(Hospital.DAL.Entities.Clinical.EncounterStatus.Registered, null, null, new DateRange(null, null), null, 1, 10, cancellationToken);
            ViewBag.Triage = triage;
            ViewData["Subtitle"] = "Triage queue and vitals recording.";
            return View("Reception");
        }

        // Administration, management and auditor share the operational overview.
        ViewBag.Kpis = await workspace.AdminAsync(cancellationToken);
        ViewBag.Occupancy = await admissions.SummaryAsync(cancellationToken);
        ViewBag.Recent = await workspace.RecentActivityAsync(8, cancellationToken);
        var month = await reports.RunAsync("appointments-status", new ReportFilter(DateRange.ThisMonth(DateOnly.FromDateTime(DateTime.UtcNow))), cancellationToken);
        ViewBag.AppointmentOutcomes = month;
        var revenue = await reports.RunAsync("revenue", new ReportFilter(DateRange.LastDays(14, DateOnly.FromDateTime(DateTime.UtcNow))), cancellationToken);
        ViewBag.Revenue = revenue;
        ViewData["Subtitle"] = "Operational posture across admissions, diagnostics, revenue and documentation.";
        return View("Operations");
    }

    [Authorize(Policy = "perm:" + Permission.ReportFinancial)]
    public async Task<IActionResult> Revenue(CancellationToken cancellationToken)
    {
        var report = await reports.RunAsync("revenue", new ReportFilter(DateRange.LastDays(14, DateOnly.FromDateTime(DateTime.UtcNow))), cancellationToken);
        return Json(new
        {
            total = report.Summary.FirstOrDefault(s => s.Key == "Net revenue").Value,
            rows = report.Rows
        });
    }
}
