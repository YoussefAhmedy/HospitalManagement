using Hospital.BLL.Common;
using Hospital.BLL.Common.Rules;
using Hospital.BLL.Services.Abstraction;
using Hospital.DAL.DataBase;
using Hospital.DAL.Entities.Clinical;
using Microsoft.EntityFrameworkCore;

namespace Hospital.BLL.Services.Implementation;

public sealed class EncounterService(
    HospitalDbContext dbContext,
    INumberSequenceService numbers) : IEncounterService
{
    public async Task<Result<Encounter>> RegisterAsync(
        string patientId, string? doctorId, int? departmentId,
        EncounterType type, string? chiefComplaint, string actorUserId,
        CancellationToken cancellationToken = default)
    {
        var patientExists = await dbContext.Patients.AnyAsync(p => p.Id == patientId, cancellationToken);
        if (!patientExists)
        {
            return Result<Encounter>.Fail("Patient not found.", "patient_not_found");
        }

        if (doctorId is not null &&
            !await dbContext.Doctors.AnyAsync(d => d.Id == doctorId && d.IsActive, cancellationToken))
        {
            return Result<Encounter>.Fail("Clinician not found or inactive.", "doctor_not_found");
        }

        var encounter = new Encounter
        {
            Number = await numbers.NextFormattedAsync(ClinicalNumber.Encounter, "encounter", cancellationToken),
            PatientId = patientId,
            DoctorId = doctorId,
            DepartmentId = departmentId,
            Type = type,
            Status = EncounterStatus.Registered,
            ChiefComplaint = Truncate(chiefComplaint, 500),
            RegisteredAtUtc = DateTime.UtcNow
        };

        dbContext.Encounters.Add(encounter);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<Encounter>.Ok(encounter);
    }

    public async Task<Result<VitalSign>> RecordVitalsAsync(
        int encounterId, VitalSign vitals, string actorUserId,
        CancellationToken cancellationToken = default)
    {
        var encounter = await dbContext.Encounters.FirstOrDefaultAsync(e => e.Id == encounterId, cancellationToken);
        if (encounter is null)
        {
            return Result<VitalSign>.Fail("Encounter not found.", "not_found");
        }

        if (!EncounterRules.IsEditable(encounter.Status))
        {
            return Result<VitalSign>.Fail("Vitals cannot be added to a signed or cancelled encounter.", "immutable");
        }

        vitals.Id = 0;
        vitals.EncounterId = encounterId;
        vitals.RecordedByUserId = actorUserId;
        vitals.RecordedAtUtc = DateTime.UtcNow;

        dbContext.VitalSigns.Add(vitals);

        if (encounter.Status == EncounterStatus.Registered &&
            EncounterRules.CanTransition(encounter.Status, EncounterStatus.Triaged))
        {
            encounter.Status = EncounterStatus.Triaged;
            encounter.TriagedAtUtc = DateTime.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<VitalSign>.Ok(vitals);
    }

    public async Task<Result> StartConsultationAsync(
        int encounterId, string? doctorId, string actorUserId,
        CancellationToken cancellationToken = default)
    {
        var encounter = await dbContext.Encounters.FirstOrDefaultAsync(e => e.Id == encounterId, cancellationToken);
        if (encounter is null)
        {
            return Result.Fail("Encounter not found.", "not_found");
        }

        if (encounter.Status == EncounterStatus.Signed || encounter.Status == EncounterStatus.Cancelled)
        {
            return Result.Fail("This encounter is closed.", "closed");
        }

        if (doctorId is not null)
        {
            encounter.DoctorId = doctorId;
        }

        if (!EncounterRules.CanTransition(encounter.Status, EncounterStatus.InConsultation))
        {
            return Result.Fail($"Cannot start a consultation from state {encounter.Status}.", "invalid_state");
        }

        encounter.Status = EncounterStatus.InConsultation;
        encounter.StartedAtUtc ??= DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }

    public async Task<Result<ClinicalNote>> AddNoteAsync(
        int encounterId, string content, string authorUserId, string? role,
        int? amendsNoteId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return Result<ClinicalNote>.Fail("Note content is required.", "empty");
        }

        var encounter = await dbContext.Encounters.FirstOrDefaultAsync(e => e.Id == encounterId, cancellationToken);
        if (encounter is null)
        {
            return Result<ClinicalNote>.Fail("Encounter not found.", "not_found");
        }

        if (!EncounterRules.IsEditable(encounter.Status))
        {
            return Result<ClinicalNote>.Fail("Signed encounters cannot receive new notes. Record an amendment instead.", "immutable");
        }

        if (amendsNoteId is not null)
        {
            var original = await dbContext.ClinicalNotes
                .FirstOrDefaultAsync(n => n.Id == amendsNoteId && n.EncounterId == encounterId, cancellationToken);
            if (original is null)
            {
                return Result<ClinicalNote>.Fail("The note being amended does not belong to this encounter.", "invalid_amendment");
            }
        }

        var note = new ClinicalNote
        {
            EncounterId = encounterId,
            Content = Truncate(content, 4000)!,
            AuthorUserId = authorUserId,
            AuthorRole = role,
            Status = NoteStatus.Draft,
            CreatedAtUtc = DateTime.UtcNow,
            AmendsNoteId = amendsNoteId
        };

        dbContext.ClinicalNotes.Add(note);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<ClinicalNote>.Ok(note);
    }

    public async Task<Result> SignNoteAsync(int noteId, string actorUserId, CancellationToken cancellationToken = default)
    {
        var note = await dbContext.ClinicalNotes.FirstOrDefaultAsync(n => n.Id == noteId, cancellationToken);
        if (note is null)
        {
            return Result.Fail("Note not found.", "not_found");
        }

        if (!EncounterRules.CanEditNote(note))
        {
            return Result.Fail("This note is already signed and cannot be changed.", "immutable");
        }

        note.Status = NoteStatus.Signed;
        note.SignedAtUtc = DateTime.UtcNow;
        note.SignedByUserId = actorUserId;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }

    public async Task<Result<Diagnosis>> AddDiagnosisAsync(
        int encounterId, string description, DiagnosisType type, string? code,
        string actorUserId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return Result<Diagnosis>.Fail("Diagnosis description is required.", "empty");
        }

        var encounter = await dbContext.Encounters.FirstOrDefaultAsync(e => e.Id == encounterId, cancellationToken);
        if (encounter is null)
        {
            return Result<Diagnosis>.Fail("Encounter not found.", "not_found");
        }

        if (!EncounterRules.IsEditable(encounter.Status))
        {
            return Result<Diagnosis>.Fail("Signed encounters cannot be changed.", "immutable");
        }

        var diagnosis = new Diagnosis
        {
            EncounterId = encounterId,
            Description = Truncate(description, 300)!,
            Code = Truncate(code, 16),
            Type = type,
            RecordedByUserId = actorUserId,
            RecordedAtUtc = DateTime.UtcNow
        };

        dbContext.Diagnoses.Add(diagnosis);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<Diagnosis>.Ok(diagnosis);
    }

    public async Task<Result> CompleteAsync(int encounterId, string actorUserId, CancellationToken cancellationToken = default)
    {
        var encounter = await dbContext.Encounters.FirstOrDefaultAsync(e => e.Id == encounterId, cancellationToken);
        if (encounter is null)
        {
            return Result.Fail("Encounter not found.", "not_found");
        }

        if (!EncounterRules.CanTransition(encounter.Status, EncounterStatus.Completed))
        {
            return Result.Fail($"Cannot complete an encounter in state {encounter.Status}.", "invalid_state");
        }

        encounter.Status = EncounterStatus.Completed;
        encounter.CompletedAtUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }

    public async Task<Result> SignEncounterAsync(int encounterId, string actorUserId, CancellationToken cancellationToken = default)
    {
        var encounter = await dbContext.Encounters
            .Include(e => e.Diagnoses)
            .Include(e => e.Notes)
            .FirstOrDefaultAsync(e => e.Id == encounterId, cancellationToken);

        if (encounter is null)
        {
            return Result.Fail("Encounter not found.", "not_found");
        }

        var hasDiagnosis = encounter.Diagnoses.Any();
        var hasSignedNote = encounter.Notes.Any(n => n.Status == NoteStatus.Signed);

        if (!EncounterRules.CanSign(encounter.Status, hasDiagnosis, hasSignedNote))
        {
            return Result.Fail(
                "An encounter can only be signed when completed, with at least one diagnosis and one signed clinical note.",
                "not_signable");
        }

        encounter.Status = EncounterStatus.Signed;
        encounter.SignedAtUtc = DateTime.UtcNow;
        encounter.SignedByUserId = actorUserId;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }

    public Task<Encounter?> GetAsync(int id, CancellationToken cancellationToken = default) =>
        dbContext.Encounters
            .AsNoTracking()
            .Include(e => e.Patient)
            .Include(e => e.Doctor)
            .Include(e => e.Department)
            .Include(e => e.Vitals.OrderByDescending(v => v.RecordedAtUtc))
            .Include(e => e.Diagnoses)
            .Include(e => e.Notes.OrderByDescending(n => n.CreatedAtUtc))
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public async Task<PagedResult<EncounterListItem>> QueryAsync(
        EncounterStatus? status, string? patientId, string? doctorId,
        DateRange range, string? term, int page, int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Encounters.AsNoTracking().AsQueryable();

        if (status is not null)
        {
            query = query.Where(e => e.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(patientId))
        {
            query = query.Where(e => e.PatientId == patientId);
        }

        if (!string.IsNullOrWhiteSpace(doctorId))
        {
            query = query.Where(e => e.DoctorId == doctorId);
        }

        if (range.FromUtc is not null)
        {
            query = query.Where(e => e.RegisteredAtUtc >= range.FromUtc);
        }

        if (range.ToUtcExclusive is not null)
        {
            query = query.Where(e => e.RegisteredAtUtc < range.ToUtcExclusive);
        }

        if (!string.IsNullOrWhiteSpace(term))
        {
            var trimmed = term.Trim();
            query = query.Where(e =>
                e.Number.Contains(trimmed) ||
                (e.Patient != null && (e.Patient.FirstName.Contains(trimmed) || e.Patient.LastName.Contains(trimmed))) ||
                (e.Patient != null && e.Patient.Mrn != null && e.Patient.Mrn.Contains(trimmed)));
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(e => e.RegisteredAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new EncounterListItem(
                e.Id,
                e.Number,
                e.PatientId,
                e.Patient!.Mrn ?? "—",
                e.Patient.FirstName + " " + e.Patient.LastName,
                e.Doctor == null ? null : e.Doctor.FirstName + " " + e.Doctor.LastName,
                e.Department == null ? null : e.Department.Name,
                e.Type,
                e.Status,
                e.RegisteredAtUtc))
            .ToListAsync(cancellationToken);

        return new PagedResult<EncounterListItem>(items, total, page, pageSize);
    }

    public async Task<EncounterQueueCounts> QueueCountsAsync(CancellationToken cancellationToken = default)
    {
        var today = DateTime.UtcNow.Date;
        var counts = await dbContext.Encounters.AsNoTracking()
            .GroupBy(e => e.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        int Get(EncounterStatus status) => counts.FirstOrDefault(c => c.Status == status)?.Count ?? 0;

        var completedToday = await dbContext.Encounters.AsNoTracking()
            .CountAsync(e => e.Status == EncounterStatus.Completed && e.CompletedAtUtc >= today, cancellationToken);

        return new EncounterQueueCounts(
            Get(EncounterStatus.Registered),
            Get(EncounterStatus.Triaged),
            Get(EncounterStatus.InConsultation),
            completedToday);
    }

    private static string? Truncate(string? value, int max) =>
        value is null ? null : value.Length <= max ? value : value[..max];
}
