using System.ComponentModel.DataAnnotations;
using Hospital.DAL.Entities.Clinical;

namespace Hospital.DAL.Entities.Operations;

public class Ward
{
    public int Id { get; set; }

    [Required, MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? Code { get; set; }

    public int? DepartmentId { get; set; }
    public Organization.Department? Department { get; set; }

    public bool IsActive { get; set; } = true;

    public List<Room> Rooms { get; set; } = [];
}

public class Room
{
    public int Id { get; set; }

    public int WardId { get; set; }
    public Ward? Ward { get; set; }

    [Required, MaxLength(40)]
    public string Number { get; set; } = string.Empty;

    [MaxLength(40)]
    public string? Class { get; set; }

    public decimal DailyRate { get; set; }

    public List<Bed> Beds { get; set; } = [];
}

public class Bed
{
    public int Id { get; set; }

    public int RoomId { get; set; }
    public Room? Room { get; set; }

    [Required, MaxLength(20)]
    public string Label { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public List<BedAssignment> Assignments { get; set; } = [];
}

public enum AdmissionStatus
{
    Requested = 1,
    Admitted = 2,
    Discharged = 3,
    Cancelled = 4
}

/// <summary>
/// Inpatient stay. Exactly one open <see cref="BedAssignment"/> may exist per bed at a
/// time; this is enforced by a filtered unique index in the database, not only in code.
/// </summary>
public class Admission
{
    public int Id { get; set; }

    [Required, MaxLength(24)]
    public string Number { get; set; } = string.Empty;

    [Required]
    public string PatientId { get; set; } = string.Empty;
    public Patient? Patient { get; set; }

    public int? EncounterId { get; set; }
    public Encounter? Encounter { get; set; }

    [MaxLength(450)]
    public string? AdmittingDoctorId { get; set; }
    public Doctor? AdmittingDoctor { get; set; }

    public AdmissionStatus Status { get; set; } = AdmissionStatus.Requested;

    [MaxLength(500)]
    public string? Reason { get; set; }

    public DateTime RequestedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? AdmittedAtUtc { get; set; }
    public DateTime? DischargedAtUtc { get; set; }

    [MaxLength(450)]
    public string? DischargeApprovedByUserId { get; set; }

    [MaxLength(2000)]
    public string? DischargeSummary { get; set; }

    public List<BedAssignment> BedAssignments { get; set; } = [];
}

public class BedAssignment
{
    public int Id { get; set; }

    public int AdmissionId { get; set; }
    public Admission? Admission { get; set; }

    public int BedId { get; set; }
    public Bed? Bed { get; set; }

    public DateTime AssignedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ReleasedAtUtc { get; set; }

    public bool IsActive => ReleasedAtUtc is null;
}
