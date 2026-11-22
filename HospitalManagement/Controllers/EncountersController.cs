using System.Security.Claims;
using Hospital.BLL.Common;
using Hospital.BLL.Common.Rules;
using Hospital.BLL.Permissions;
using Hospital.BLL.Services.Abstraction;
using Hospital.DAL.DataBase;
using Hospital.DAL.Entities.Clinical;
using BLL = Hospital.BLL;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Hospital.BLL.Helpers;

namespace HospitalManagement.Controllers;

/// <summary>Clinical encounter workspace: triage, documentation, diagnoses and signing.</summary>
[Authorize(Policy = "perm:" + Permission.EncounterView)]
public sealed class EncountersController(
    IEncounterService encounters,
    IOrganizationDirectoryService directory,
    HospitalDbContext dbContext,
    IAuditLogger audit) : Controller
{
    private string CurrentUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    public async Task<IActionResult> Index(string? status, string? term, string? patientId, int page = 1, CancellationToken cancellationToken = default)
    {
        EncounterStatus? parsed = Enum.TryParse<EncounterStatus>(status, true, out var value) ? value : null;
        var result = await encounters.QueryAsync(parsed, patientId, null, new DateRange(null, null), term, page, 20, cancellationToken);

        ViewData["Title"] = "Encounters";
        ViewData["Section"] = "Clinical";
        ViewData["Subtitle"] = "Every registered visit with its lifecycle state and responsible clinician.";

        ViewBag.Status = status;
        ViewBag.Term = term;
        ViewBag.PatientId = patientId;
        ViewBag.Queue = await encounters.QueueCountsAsync(cancellationToken);
        return View(result);
    }

    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var encounter = await dbContext.Encounters
            .AsNoTracking()
            .Include(e => e.Patient).ThenInclude(p => p!.Allergies)
            .Include(e => e.Doctor)
            .Include(e => e.Department)
            .Include(e => e.Vitals.OrderByDescending(v => v.RecordedAtUtc))
            .Include(e => e.Diagnoses)
            .Include(e => e.Notes.OrderByDescending(n => n.CreatedAtUtc))
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        if (encounter is null)
        {
            return NotFound();
        }

        await audit.RecordAsync("encounter.view", CurrentUserId(), encounter.PatientId, "Encounter", id.ToString(), cancellationToken);

        ViewData["Title"] = $"Encounter {encounter.Number}";
        ViewData["Section"] = "Clinical";
        ViewData["Subtitle"] = $"{encounter.Patient?.FirstName} {encounter.Patient?.LastName} · MRN {encounter.Patient?.Mrn ?? "—"}";

        ViewBag.Directory = await directory.SnapshotAsync(cancellationToken);
        ViewBag.CanDocument = User.HasClaim(AppClaimTypes.Permission, Permission.EncounterDocument);
        ViewBag.CanSign = User.HasClaim(AppClaimTypes.Permission, Permission.EncounterSign);
        ViewBag.CanTriage = User.HasClaim(AppClaimTypes.Permission, Permission.TriageRecord);
        ViewBag.CanOrderLabs = User.HasClaim(AppClaimTypes.Permission, Permission.LabOrderCreate);
        ViewBag.CanPrescribe = User.HasClaim(AppClaimTypes.Permission, Permission.PrescriptionCreate);
        ViewBag.CanOrderImaging = User.HasClaim(AppClaimTypes.Permission, Permission.ImagingOrder);
        ViewBag.LabOrders = await dbContext.LabOrders.AsNoTracking()
            .Include(o => o.Items).ThenInclude(i => i.LabTest)
            .Where(o => o.EncounterId == id)
            .OrderByDescending(o => o.CreatedAtUtc)
            .ToListAsync(cancellationToken);
        ViewBag.Prescriptions = await dbContext.Prescriptions.AsNoTracking()
            .Include(p => p.Items).ThenInclude(i => i.Medication)
            .Where(p => p.EncounterId == id)
            .OrderByDescending(p => p.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return View(encounter);
    }

    [HttpPost]
    [Authorize(Policy = "perm:" + Permission.TriageRecord)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Vitals(int id, VitalSign vitals, CancellationToken cancellationToken)
    {
        var result = await encounters.RecordVitalsAsync(id, vitals, CurrentUserId(), cancellationToken);
        await Flash(result, "encounter.vitals.record", id.ToString(), "Vital signs recorded.");
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [Authorize(Policy = "perm:" + Permission.EncounterDocument)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StartConsultation(int id, CancellationToken cancellationToken)
    {
        var result = await encounters.StartConsultationAsync(id, CurrentUserId(), CurrentUserId(), cancellationToken);
        await Flash(result, "encounter.consult.start", id.ToString(), "Consultation started.");
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [Authorize(Policy = "perm:" + Permission.EncounterDocument)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddNote(int id, string content, int? amendsNoteId, CancellationToken cancellationToken)
    {
        var result = await encounters.AddNoteAsync(id, content, CurrentUserId(), User.FindFirstValue(ClaimTypes.Role), amendsNoteId, cancellationToken);
        await Flash(result.As(r => (object?)r), "encounter.note.add", id.ToString(), "Clinical note saved as a draft.");
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [Authorize(Policy = "perm:" + Permission.EncounterSign)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SignNote(int id, int noteId, CancellationToken cancellationToken)
    {
        var result = await encounters.SignNoteAsync(noteId, CurrentUserId(), cancellationToken);
        await Flash(result, "encounter.note.sign", noteId.ToString(), "Note signed. It is now immutable.");
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [Authorize(Policy = "perm:" + Permission.EncounterDocument)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddDiagnosis(int id, string description, string? code, DiagnosisType type, CancellationToken cancellationToken)
    {
        var result = await encounters.AddDiagnosisAsync(id, description, type, code, CurrentUserId(), cancellationToken);
        await Flash(result.As(r => (object?)r), "encounter.diagnosis.add", id.ToString(), "Diagnosis recorded.");
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [Authorize(Policy = "perm:" + Permission.EncounterDocument)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkComplete(int id, CancellationToken cancellationToken)
    {
        var result = await encounters.CompleteAsync(id, CurrentUserId(), cancellationToken);
        await Flash(result, "encounter.complete", id.ToString(), "Encounter marked complete. Add a diagnosis and sign a note before signing the encounter.");
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [Authorize(Policy = "perm:" + Permission.EncounterSign)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Sign(int id, CancellationToken cancellationToken)
    {
        var result = await encounters.SignEncounterAsync(id, CurrentUserId(), cancellationToken);
        await Flash(result, "encounter.sign", id.ToString(), "Encounter signed and closed.");
        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task Flash<T>(Hospital.BLL.Common.Result<T> result, string eventType, string resourceId, string successMessage) =>
        await Flash(result.As(), eventType, resourceId, successMessage);

    private async Task Flash(Hospital.BLL.Common.Result result, string eventType, string resourceId, string successMessage)
    {
        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = result.Error ?? "The action could not be completed.";
            return;
        }

        await audit.RecordAsync(eventType, CurrentUserId(), null, "Encounter", resourceId);
        TempData["StatusMessage"] = successMessage;
    }

}

internal static class ResultExtensions
{
    /// <summary>Drops the payload so a generic result can flow through the shared flash helper.</summary>
    public static Hospital.BLL.Common.Result As<T>(this Hospital.BLL.Common.Result<T> result) =>
        result.Succeeded
            ? Hospital.BLL.Common.Result.Ok()
            : Hospital.BLL.Common.Result.Fail(result.Error ?? "The action could not be completed.", result.Code);
}
