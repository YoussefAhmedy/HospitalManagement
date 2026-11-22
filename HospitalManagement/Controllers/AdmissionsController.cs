using System.Security.Claims;
using Hospital.BLL.Common;
using Hospital.BLL.Permissions;
using Hospital.BLL.Services.Abstraction;
using Hospital.DAL.DataBase;
using Hospital.DAL.Entities.Operations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Hospital.BLL.Helpers;

namespace HospitalManagement.Controllers;

/// <summary>Admissions and bed management with database-enforced single-bed occupancy.</summary>
[Authorize(Policy = "perm:" + Permission.AdmissionView)]
public sealed class AdmissionsController(
    IAdmissionService admissions,
    IPatientLookupService lookup,
    HospitalDbContext dbContext,
    IAuditLogger audit) : Controller
{
    private string CurrentUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    public async Task<IActionResult> Index(string? status, string? term, int page = 1, CancellationToken cancellationToken = default)
    {
        AdmissionStatus? parsed = Enum.TryParse<AdmissionStatus>(status, true, out var value) ? value : null;

        ViewData["Title"] = "Admissions & beds";
        ViewData["Section"] = "Clinical";
        ViewData["Subtitle"] = "Inpatient flow, bed occupancy and discharge authorisation.";

        ViewBag.Summary = await admissions.SummaryAsync(cancellationToken);
        ViewBag.Board = await admissions.BedBoardAsync(cancellationToken);
        ViewBag.Admissions = await admissions.QueryAsync(parsed, term, page, 15, cancellationToken);
        ViewBag.Status = status;
        ViewBag.Term = term;
        ViewBag.CanManage = User.HasClaim(AppClaimTypes.Permission, Permission.AdmissionManage);
        ViewBag.CanDischarge = User.HasClaim(AppClaimTypes.Permission, Permission.DischargeApprove);
        ViewBag.CanBed = User.HasClaim(AppClaimTypes.Permission, Permission.BedManage);
        return View();
    }

    public async Task<IActionResult> Request(string? patientId, CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Request admission";
        ViewData["Section"] = "Clinical";
        ViewData["Subtitle"] = "A bed is assigned after the admission is approved.";

        ViewBag.Patients = await lookup.SearchAsync(null, 1, 50, cancellationToken);
        ViewBag.SelectedPatient = patientId;
        ViewBag.Doctors = await dbContext.Doctors.AsNoTracking().Where(d => d.IsActive)
            .Select(d => new { d.Id, Name = "Dr. " + d.FirstName + " " + d.LastName })
            .ToListAsync(cancellationToken);
        return View();
    }

    [HttpPost]
    [Authorize(Policy = "perm:" + Permission.AdmissionManage)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Request(string patientId, string? admittingDoctorId, string? reason, CancellationToken cancellationToken)
    {
        var result = await admissions.RequestAdmissionAsync(patientId, admittingDoctorId, null, reason, cancellationToken);
        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = result.Error;
            return RedirectToAction(nameof(Request));
        }

        await audit.RecordAsync("admission.request", CurrentUserId(), patientId, "Admission", result.Value!.Id.ToString(), cancellationToken);
        TempData["StatusMessage"] = $"Admission {result.Value.Number} requested. Assign a bed to admit the patient.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [Authorize(Policy = "perm:" + Permission.BedManage)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Assign(int id, int bedId, CancellationToken cancellationToken)
    {
        var result = await admissions.AdmitWithBedAsync(id, bedId, CurrentUserId(), cancellationToken);
        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = result.Error;
        }
        else
        {
            await audit.RecordAsync("admission.assign-bed", CurrentUserId(), null, "Admission", id.ToString(), cancellationToken);
            TempData["StatusMessage"] = "Patient admitted; the bed is now occupied.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [Authorize(Policy = "perm:" + Permission.DischargeApprove)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Discharge(int id, string summary, CancellationToken cancellationToken)
    {
        var result = await admissions.DischargeAsync(id, summary, CurrentUserId(), cancellationToken);
        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = result.Error;
        }
        else
        {
            await audit.RecordAsync("admission.discharge", CurrentUserId(), null, "Admission", id.ToString(), cancellationToken);
            TempData["StatusMessage"] = "Patient discharged and the bed released.";
        }

        return RedirectToAction(nameof(Index));
    }
}
