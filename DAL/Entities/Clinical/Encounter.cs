using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Hospital.DAL.Entities.Organization;

namespace Hospital.DAL.Entities.Clinical;

/// <summary>
/// A single clinical visit. Owns vitals, diagnoses and notes, and is the anchor for
/// laboratory, imaging and pharmacy orders. Signed encounters are immutable at the
/// service layer; amendments are recorded as new notes referencing the original.
/// </summary>
public class Encounter
{
    public int Id { get; set; }

    [Required, MaxLength(24)]
    public string Number { get; set; } = string.Empty;

    [Required]
    public string PatientId { get; set; } = string.Empty;
    public Patient? Patient { get; set; }

    public string? DoctorId { get; set; }
    public Doctor? Doctor { get; set; }

    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }

    public EncounterType Type { get; set; } = EncounterType.Outpatient;
    public EncounterStatus Status { get; set; } = EncounterStatus.Registered;

    [MaxLength(500)]
    public string? ChiefComplaint { get; set; }

    public DateTime RegisteredAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? TriagedAtUtc { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public DateTime? SignedAtUtc { get; set; }

    [MaxLength(450)]
    public string? SignedByUserId { get; set; }

    public List<VitalSign> Vitals { get; set; } = [];
    public List<Diagnosis> Diagnoses { get; set; } = [];
    public List<ClinicalNote> Notes { get; set; } = [];
}
