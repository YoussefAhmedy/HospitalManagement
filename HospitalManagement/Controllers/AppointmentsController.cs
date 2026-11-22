using System.Security.Claims;
using Hospital.BLL.Common;
using Hospital.BLL.Permissions;
using Hospital.BLL.Services.Abstraction;
using Hospital.DAL.DataBase;
using Hospital.DAL.Entities.OwnedTypes;
using HospitalManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HospitalManagement.Controllers;

/// <summary>Scheduling workspace: availability, booking, check-in and queue management.</summary>
[Authorize(Policy = "perm:" + Permission.AppointmentView)]
public sealed class AppointmentsController(
    IAppointmentSchedulingService scheduling,
    IPatientLookupService patients,
    IOrganizationDirectoryService directory,
    HospitalDbContext dbContext,
    IAuditLogger audit) : Controller
{
    private string CurrentUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    public async Task<IActionResult> Index(DateTime? date, string? status, string? doctorId, string? term, int page = 1, CancellationToken cancellationToken = default)
    {
        AppointStatus? parsed = Enum.TryParse<AppointStatus>(status, true, out var value) ? value : null;
        var result = await scheduling.QueryAsync(date, parsed, doctorId, null, term, page, 20, cancellationToken);

        ViewData["Title"] = "Appointments";
        ViewData["Section"] = "Scheduling";
        ViewData["Subtitle"] = "Slot-based scheduling with conflict protection and a live check-in queue.";

        ViewBag.Date = date?.ToString("yyyy-MM-dd");
        ViewBag.Status = status;
        ViewBag.DoctorId = doctorId;
        ViewBag.Term = term;
        ViewBag.Directory = await directory.SnapshotAsync(cancellationToken);
        ViewBag.Queue = await scheduling.QueueAsync(date?.Date ?? DateTime.UtcNow.Date, cancellationToken);
        return View(result);
    }

    public async Task<IActionResult> Book(DateTime? date, string? doctorId, string? patientId, CancellationToken cancellationToken)
    {
        var target = (date ?? DateTime.UtcNow.Date.AddDays(1)).Date;
        var boards = await scheduling.AvailabilityAsync(target, doctorId, cancellationToken);

        ViewData["Title"] = "Book appointment";
        ViewData["Section"] = "Scheduling";
        ViewData["Subtitle"] = "Pick a clinician and a free slot. Availability is computed from the weekly clinic schedule.";

        ViewBag.Date = target;
        ViewBag.Boards = boards;
        ViewBag.Directory = await directory.SnapshotAsync(cancellationToken);
        ViewBag.Patients = await patients.SearchAsync(null, 1, 50, cancellationToken);
        ViewBag.SelectedPatient = patientId;
        return View();
    }

    [HttpPost]
    [Authorize(Policy = "perm:" + Permission.AppointmentCreate)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Book(string patientId, string doctorId, DateTime date, int slotMinutes, string? notes, CancellationToken cancellationToken)
    {
        var result = await scheduling.BookAsync(patientId, doctorId, date, slotMinutes, notes, CurrentUserId(), cancellationToken);
        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = result.Error;
            return RedirectToAction(nameof(Book), new { date, doctorId, patientId });
        }

        await audit.RecordAsync("appointment.book", CurrentUserId(), patientId, "Appointment", result.Value!.AppointmentID.ToString(), cancellationToken);
        TempData["StatusMessage"] = $"Appointment {result.Value.Number} requested. Confirm it once the clinician reviews the request.";
        return RedirectToAction(nameof(Index), new { date = date.ToString("yyyy-MM-dd") });
    }

    [HttpPost]
    [Authorize(Policy = "perm:" + Permission.AppointmentManage)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Confirm(int id, DateTime? date, CancellationToken cancellationToken)
    {
        var result = await scheduling.ConfirmAsync(id, CurrentUserId(), cancellationToken);
        await Flash(result, "appointment.confirm", id, "Appointment confirmed.");
        return RedirectToAction(nameof(Index), new { date = date?.ToString("yyyy-MM-dd") });
    }

    [HttpPost]
    [Authorize(Policy = "perm:" + Permission.AppointmentCheckIn)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CheckIn(int id, DateTime? date, CancellationToken cancellationToken)
    {
        var result = await scheduling.CheckInAsync(id, CurrentUserId(), cancellationToken);
        if (result.Succeeded)
        {
            await audit.RecordAsync("appointment.checkin", CurrentUserId(), null, "Appointment", id.ToString(), cancellationToken);
            TempData["StatusMessage"] = "Patient checked in and an encounter was opened.";
            return RedirectToAction("Details", "Encounters", new { id = result.Value!.EncounterId });
        }

        TempData["ErrorMessage"] = result.Error;
        return RedirectToAction(nameof(Index), new { date = date?.ToString("yyyy-MM-dd") });
    }

    [HttpPost]
    [Authorize(Policy = "perm:" + Permission.AppointmentManage)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id, string? reason, DateTime? date, CancellationToken cancellationToken)
    {
        var result = await scheduling.CancelAsync(id, CurrentUserId(), reason, cancellationToken);
        await Flash(result, "appointment.cancel", id, "Appointment cancelled and the slot released.");
        return RedirectToAction(nameof(Index), new { date = date?.ToString("yyyy-MM-dd") });
    }

    [HttpPost]
    [Authorize(Policy = "perm:" + Permission.AppointmentManage)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> NoShow(int id, DateTime? date, CancellationToken cancellationToken)
    {
        var result = await scheduling.MarkNoShowAsync(id, CurrentUserId(), cancellationToken);
        await Flash(result, "appointment.noshow", id, "Appointment marked as a no-show.");
        return RedirectToAction(nameof(Index), new { date = date?.ToString("yyyy-MM-dd") });
    }

    private async Task Flash(BLL.Common.Result result, string eventType, int id, string successMessage)
    {
        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = result.Error;
            return;
        }

        await audit.RecordAsync(eventType, CurrentUserId(), null, "Appointment", id.ToString());
        TempData["StatusMessage"] = successMessage;
    }
}
