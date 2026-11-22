using System.Security.Claims;
using Hospital.BLL.Common;
using Hospital.BLL.Permissions;
using Hospital.BLL.Services.Abstraction;
using Hospital.DAL.DataBase;
using Hospital.DAL.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Hospital.BLL.Helpers;

namespace HospitalManagement.Controllers;

/// <summary>Patient directory, master data and the consolidated patient chart.</summary>
[Authorize(Policy = "perm:" + Permission.PatientView)]
public sealed class PatientsController(
    HospitalDbContext dbContext,
    IPatientLookupService lookup,
    IEncounterService encounters,
    ILabService lab,
    IBillingService billing,
    INumberSequenceService numbers,
    UserManager<ApplicationUser> userManager,
    IAuditLogger audit) : Controller
{
    private string CurrentUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    public async Task<IActionResult> Index(string? term, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        pageSize = pageSize is < 10 or > 100 ? 20 : pageSize;
        var result = await lookup.SearchAsync(term, Math.Max(1, page), pageSize, cancellationToken);

        ViewData["Title"] = "Patient directory";
        ViewData["Section"] = "Clinical";
        ViewData["Subtitle"] = "Search by name, medical record number, phone or email.";

        ViewBag.Term = term;
        ViewBag.PageSize = pageSize;
        return View(result);
    }

    public async Task<IActionResult> Create()
    {
        ViewData["Title"] = "Register patient";
        ViewData["Section"] = "Clinical";
        ViewData["Subtitle"] = "A medical record number is allocated automatically and is permanent.";
        ViewBag.Departments = (await dbContext.Departments.Where(d => d.IsActive).OrderBy(d => d.Name).ToListAsync()).Select(d => new { d.Id, d.Name });
        return View(new Hospital.BLL.ModelVM.RegisterPatientVm());
    }

    [HttpPost]
    [Authorize(Policy = "perm:" + Permission.PatientCreate)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Hospital.BLL.ModelVM.RegisterPatientVm model, CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Register patient";
        ViewData["Section"] = "Clinical";

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        DateOnly? dob = model.DateOfBirth;
        var duplicate = await lookup.HasSimilarAsync(model.FirstName, model.LastName, dob, model.PhoneNumber, null, cancellationToken);
        if (duplicate && !model.ConfirmDuplicate)
        {
            ModelState.AddModelError(nameof(model.ConfirmDuplicate),
                "A patient with the same name and date of birth or phone number already exists. Confirm to register anyway.");
            return View(model);
        }

        var patient = new Patient
        {
            FirstName = model.FirstName.Trim(),
            LastName = model.LastName.Trim(),
            UserName = model.Email,
            Email = model.Email,
            EmailConfirmed = true,
            PhoneNumber = model.PhoneNumber?.Trim(),
            Gender = model.Sex.ToString(),
            DateOfBirth = dob?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            Address = model.Address?.Trim(),
            EmergencyContact = model.EmergencyContact?.Trim(),
            BloodGroup = model.BloodGroup,
            MedicalHistory = model.MedicalHistory?.Trim(),
            Mrn = await numbers.NextFormattedAsync(ClinicalNumber.Patient, "patient", cancellationToken),
            IsSynthetic = false
        };

        // Patients registered by staff receive a random temporary password and must reset it.
        var password = $"Cx{Guid.NewGuid():N}!9a";
        var created = await userManager.CreateAsync(patient, password);
        if (!created.Succeeded)
        {
            foreach (var error in created.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return View(model);
        }

        await userManager.AddToRoleAsync(patient, Hospital.BLL.Helpers.Role.Patient.ToString());
        await audit.RecordAsync("patient.create", CurrentUserId(), patient.Id, "Patient", patient.Id, cancellationToken);
        TempData["StatusMessage"] = $"Patient registered with MRN {patient.Mrn}.";
        return RedirectToAction(nameof(Details), new { id = patient.Id });
    }

    public async Task<IActionResult> Details(string id, CancellationToken cancellationToken)
    {
        var patient = await dbContext.Patients.AsNoTracking()
            .Include(p => p.Allergies)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (patient is null)
        {
            return NotFound();
        }

        var encounters = await encounters.QueryAsync(null, id, null, new DateRange(null, null), null, 1, 8, cancellationToken);
        var labs = await lab.QueryAsync(null, id, new DateRange(null, null), null, 1, 8, cancellationToken);
        var invoices = await billing.QueryAsync(null, new DateRange(null, null), null, 1, 8, cancellationToken);
        var openInvoices = invoices.Items.Where(i => i.PatientName.Contains(patient.LastName, StringComparison.OrdinalIgnoreCase)).ToList();

        await audit.RecordAsync("patient.chart.view", CurrentUserId(), patient.Id, "Patient", patient.Id, cancellationToken);

        ViewData["Title"] = $"{patient.FirstName} {patient.LastName}";
        ViewData["Section"] = "Clinical";
        ViewData["Subtitle"] = $"MRN {patient.Mrn ?? "—"} · registered {patient.CreatedAt:yyyy-MM-dd}";

        ViewBag.Encounters = encounters;
        ViewBag.Labs = labs;
        ViewBag.Invoices = openInvoices;
        ViewBag.CanCreateEncounter = User.HasClaim(AppClaimTypes.Permission, Permission.EncounterCreate);
        return View(patient);
    }

    [HttpPost]
    [Authorize(Policy = "perm:" + Permission.PatientEdit)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddAllergy(string id, string substance, string? reaction, Hospital.DAL.Entities.AllergySeverity severity, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(substance))
        {
            TempData["ErrorMessage"] = "An allergy needs a substance.";
            return RedirectToAction(nameof(Details), new { id });
        }

        dbContext.Allergies.Add(new Allergy
        {
            PatientId = id,
            Substance = substance.Trim(),
            Reaction = reaction?.Trim(),
            Severity = severity,
            RecordedByUserId = CurrentUserId(),
            RecordedAtUtc = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        await audit.RecordAsync("patient.allergy.add", CurrentUserId(), id, "Patient", id, cancellationToken);
        TempData["StatusMessage"] = "Allergy recorded.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [Authorize(Policy = "perm:" + Permission.EncounterCreate)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StartEncounter(string id, Hospital.DAL.Entities.Clinical.EncounterType type, string? complaint, CancellationToken cancellationToken)
    {
        var result = await encounters.RegisterAsync(id, null, null, type, complaint, CurrentUserId(), cancellationToken);
        if (!result.Succeeded || result.Value is null)
        {
            TempData["ErrorMessage"] = result.Error ?? "The encounter could not be created.";
            return RedirectToAction(nameof(Details), new { id });
        }

        await audit.RecordAsync("encounter.create", CurrentUserId(), id, "Encounter", result.Value.Id.ToString(), cancellationToken);
        return RedirectToAction("Details", "Encounters", new { id = result.Value.Id });
    }
}
