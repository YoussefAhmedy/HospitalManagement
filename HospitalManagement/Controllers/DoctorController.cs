using System.Security.Claims;
using AutoMapper;
using Hospital.BLL.Helpers;
using Hospital.BLL.ModelVM;
using Hospital.BLL.Notifications;
using Hospital.BLL.Services.Abstraction;
using Hospital.DAL.Entities;
using Hospital.DAL.Entities.OwnedTypes;
using HospitalManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace HospitalManagement.Controllers;

[Authorize(Roles = "Doctor")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class DoctorController : Controller
{
    private readonly IAppointmentService _appointmentService;
    private readonly IScheduleService _scheduleService;
    private readonly IEmailQueue _emailQueue;
    private readonly IPatientService _patientService;
    private readonly ImedicalRecordService _medicalRecordService;
    private readonly IMapper _mapper;
    private readonly IAuditLogger _auditLogger;
    private readonly ILogger<DoctorController> _logger;

    public DoctorController(
        IAppointmentService appointmentService,
        IScheduleService scheduleService,
        IEmailQueue emailQueue,
        IPatientService patientService,
        ImedicalRecordService medicalRecordService,
        IMapper mapper,
        IAuditLogger auditLogger,
        ILogger<DoctorController> logger)
    {
        _appointmentService = appointmentService;
        _scheduleService = scheduleService;
        _emailQueue = emailQueue;
        _patientService = patientService;
        _medicalRecordService = medicalRecordService;
        _mapper = mapper;
        _auditLogger = auditLogger;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var doctorId = CurrentUserId();
        if (doctorId is null)
        {
            return Challenge();
        }

        var today = DateTime.Today;
        var appointments = _appointmentService.GetAppointments(appointment =>
            appointment.DoctorID == doctorId && appointment.Status != AppointStatus.Cancelled);
        var todayAppointments = await appointments
            .Where(appointment => appointment.AppointmentDate >= today && appointment.AppointmentDate < today.AddDays(1))
            .OrderBy(appointment => appointment.AppointmentDate)
            .Take(20)
            .ToListAsync();
        var upcomingAppointments = await appointments
            .Where(appointment => appointment.AppointmentDate >= today)
            .OrderBy(appointment => appointment.AppointmentDate)
            .Take(8)
            .ToListAsync();

        await _auditLogger.RecordAsync("doctor.dashboard-viewed", doctorId, doctorId, "doctor-dashboard", doctorId);
        return View(new DoctorDashboardVm
        {
            TodayAppointments = todayAppointments,
            UpcomingAppointments = upcomingAppointments,
            PendingAppointments = appointments.Count(appointment => appointment.Status == AppointStatus.Pending)
        });
    }

    [HttpGet]
    public async Task<IActionResult> GetAppointments()
    {
        var doctorId = CurrentUserId();
        if (doctorId is null)
        {
            return Challenge();
        }

        await _auditLogger.RecordAsync("doctor.appointments-viewed", doctorId, doctorId, "appointments", doctorId);
        var appointments = await _appointmentService.GetAppointments(appointment =>
                appointment.DoctorID == doctorId && appointment.AppointmentDate >= DateTime.Today &&
                appointment.Status != AppointStatus.Cancelled)
            .OrderBy(appointment => appointment.AppointmentDate)
            .Take(100)
            .ToListAsync();
        return View(appointments);
    }

    [HttpGet]
    public async Task<IActionResult> EditAppointment(int id)
    {
        var doctorId = CurrentUserId();
        if (doctorId is null)
        {
            return Challenge();
        }

        var appointment = await _appointmentService.GetAppointmentById(id);
        if (appointment is null || !ResourceOwnership.IsOwner(doctorId, appointment.DoctorID))
        {
            await _auditLogger.RecordAsync("security.doctor-appointment-access-denied", doctorId,
                appointment?.PatientID, "appointment", id.ToString());
            return NotFound();
        }

        await _auditLogger.RecordAsync("doctor.appointment-viewed", doctorId,
            appointment.PatientID, "appointment", appointment.AppointmentID.ToString());
        return View(new DoctorAppointmentApprovalVm
        {
            Status = (int)appointment.Status,
            Id = appointment.AppointmentID
        });
    }

    [HttpPost]
    public async Task<IActionResult> EditAppointment(DoctorAppointmentApprovalVm model)
    {
        var doctorId = CurrentUserId();
        if (doctorId is null)
        {
            return Challenge();
        }
        if (!ModelState.IsValid || model.Id <= 0 || !AppointmentRules.IsValidStatus(model.Status))
        {
            ModelState.AddModelError(string.Empty, "Choose a valid appointment action.");
            return View(model);
        }

        var appointment = await _appointmentService.GetAppointmentById(model.Id);
        if (appointment is null || !ResourceOwnership.IsOwner(doctorId, appointment.DoctorID))
        {
            await _auditLogger.RecordAsync("security.doctor-appointment-update-denied", doctorId,
                appointment?.PatientID, "appointment", model.Id.ToString());
            return NotFound();
        }
        if (appointment.Status == AppointStatus.Cancelled || appointment.AppointmentDate.Date < DateTime.Today)
        {
            return Conflict("This appointment is no longer editable.");
        }

        var requestedStatus = (AppointStatus)model.Status;
        if (requestedStatus is not (AppointStatus.Approved or AppointStatus.NotApproved))
        {
            ModelState.AddModelError(nameof(model.Status), "Choose approve or decline.");
            return View(model);
        }

        if (requestedStatus == AppointStatus.Approved)
        {
            var schedule = _scheduleService.GetDoctorSchedulesById(doctorId)
                .FirstOrDefault(candidate => candidate.Status == Status.Assigned &&
                    AppointmentRules.IsScheduledForDate(appointment.AppointmentDate, candidate.Day));
            if (schedule is null)
            {
                ModelState.AddModelError(string.Empty, "No assigned schedule matches this appointment date.");
                return View(model);
            }

            var otherBookings = await _appointmentService.CountForDoctorOnDateAsync(
                doctorId, appointment.AppointmentDate, appointment.AppointmentID);
            if (!AppointmentRules.HasDailyCapacity(otherBookings))
            {
                ModelState.AddModelError(string.Empty, "The clinician's daily appointment capacity has been reached.");
                return View(model);
            }

            appointment.Schedule = schedule;
            appointment.ScheduleId = schedule.Id;
        }

        appointment.Status = requestedStatus;
        if (!await _appointmentService.UpdateAppointment(appointment))
        {
            ModelState.AddModelError(string.Empty, "The appointment changed while you were viewing it. Refresh and try again.");
            return View(model);
        }

        var eventType = requestedStatus == AppointStatus.Approved
            ? "doctor.appointment-approved"
            : "doctor.appointment-declined";
        await _auditLogger.RecordAsync(eventType, doctorId, appointment.PatientID,
            "appointment", appointment.AppointmentID.ToString());

        var emailQueued = false;
        if (appointment.Patient?.Email is { Length: > 0 } patientEmail)
        {
            emailQueued = _emailQueue.TryEnqueue(
                patientEmail,
                "Appointment status updated",
                EmailTemplates.AppointmentUpdate(
                    appointment.Patient.FirstName,
                    requestedStatus == AppointStatus.Approved ? "approved" : "not approved",
                    appointment.AppointmentDate.ToString("D")));
        }

        TempData["StatusMessage"] = emailQueued
            ? "Appointment status updated. The patient notification has been queued."
            : "Appointment status updated. Email delivery is not configured; the patient will see the update in their dashboard.";
        return RedirectToAction(nameof(GetAppointments));
    }

    [HttpGet]
    public async Task<IActionResult> GetMedicalRecords()
    {
        var doctorId = CurrentUserId();
        if (doctorId is null)
        {
            return Challenge();
        }

        await _auditLogger.RecordAsync("doctor.medical-records-viewed", doctorId, doctorId,
            "medical-records", doctorId);
        var records = await _medicalRecordService.GetForDoctor(doctorId)
            .OrderByDescending(record => record.RecordDate)
            .Take(100)
            .ToListAsync();
        return View(records);
    }

    [HttpGet]
    public async Task<IActionResult> AddMedicalRecord()
    {
        var doctorId = CurrentUserId();
        if (doctorId is null)
        {
            return Challenge();
        }

        var model = new AddMedicalRecordByDoctorVm
        {
            patients = await GetAssignedPatientsAsync(doctorId)
        };
        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> AddMedicalRecord(AddMedicalRecordByDoctorVm model)
    {
        var doctorId = CurrentUserId();
        if (doctorId is null)
        {
            return Challenge();
        }

        if (!ModelState.IsValid)
        {
            model.patients = await GetAssignedPatientsAsync(doctorId);
            return View(model);
        }

        var hasAuthorizedRelationship = await _appointmentService.GetAppointments(appointment =>
                appointment.DoctorID == doctorId &&
                appointment.PatientID == model.PatientId &&
                appointment.Status == AppointStatus.Approved)
            .AnyAsync();
        if (!hasAuthorizedRelationship)
        {
            await _auditLogger.RecordAsync("security.doctor-record-create-denied", doctorId,
                model.PatientId, "medical-record", null);
            return NotFound();
        }

        if (model.RecordDate.Date > DateTime.Today)
        {
            ModelState.AddModelError(nameof(model.RecordDate), "A medical note cannot be dated in the future.");
            model.patients = await GetAssignedPatientsAsync(doctorId);
            return View(model);
        }

        var record = _mapper.Map<MedicalRecord>(model);
        record.RecordDate = model.RecordDate.Date;
        record.DoctorID = doctorId;
        record.PatientID = model.PatientId;
        if (!await _medicalRecordService.AddMedicalRecord(record))
        {
            _logger.LogError("A medical record could not be saved.");
            ModelState.AddModelError(string.Empty, "The note could not be saved. Please try again.");
            model.patients = await GetAssignedPatientsAsync(doctorId);
            return View(model);
        }

        await _auditLogger.RecordAsync("doctor.medical-record-created", doctorId,
            record.PatientID, "medical-record", record.MedicalRecordID.ToString());
        TempData["StatusMessage"] = "Clinical note saved.";
        return RedirectToAction(nameof(GetMedicalRecords));
    }

    [HttpGet]
    [EnableRateLimiting("public-search")]
    public async Task<IActionResult> GetRecords(int num)
    {
        var doctorId = CurrentUserId();
        if (doctorId is null)
        {
            return Challenge();
        }

        var limit = Math.Clamp(num, 1, 50);
        var records = await _medicalRecordService.GetForDoctor(doctorId)
            .OrderByDescending(record => record.RecordDate)
            .Take(limit)
            .Select(record => new MedicalRecordVMByAj
            {
                Diagnosis = record.Diagnosis,
                Treatment = record.Treatment,
                RecordDate = record.RecordDate.Date,
                FullName = $"{record.Patient!.FirstName} {record.Patient.LastName}"
            })
            .ToListAsync();
        return Ok(records);
    }

    [HttpGet]
    [EnableRateLimiting("public-search")]
    public async Task<IActionResult> GetAppoints(int num)
    {
        var doctorId = CurrentUserId();
        if (doctorId is null)
        {
            return Challenge();
        }

        var limit = Math.Clamp(num, 1, 50);
        var appointments = await _appointmentService.GetAppointments(appointment =>
                appointment.DoctorID == doctorId && appointment.AppointmentDate >= DateTime.Today)
            .OrderBy(appointment => appointment.AppointmentDate)
            .Take(limit)
            .Select(appointment => new AppointVmByAj
            {
                FullName = $"{appointment.Patient!.FirstName} {appointment.Patient.LastName}",
                Date = appointment.AppointmentDate.Date,
                Notes = appointment.Notes,
                AppointId = appointment.Status == AppointStatus.Approved
                    ? appointment.AppointmentID.ToString()
                    : appointment.Status.ToString(),
                Status = appointment.Status.ToString(),
                Id = appointment.AppointmentID
            })
            .ToListAsync();
        return Ok(appointments);
    }

    private async Task<IReadOnlyList<Patient>> GetAssignedPatientsAsync(string doctorId)
    {
        var patientIds = await _appointmentService.GetAppointments(appointment =>
                appointment.DoctorID == doctorId && appointment.Status == AppointStatus.Approved)
            .Select(appointment => appointment.PatientID!)
            .Where(patientId => patientId != null)
            .Distinct()
            .ToListAsync();
        return await _patientService.GetPatientsByIds(patientIds).ToListAsync();
    }

    private string? CurrentUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier);
}
