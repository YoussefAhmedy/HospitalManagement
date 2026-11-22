using System.ComponentModel.DataAnnotations;
using Hospital.DAL.Entities.Clinical;

namespace Hospital.DAL.Entities.Diagnostics;

public class LabOrder
{
    public int Id { get; set; }

    [Required, MaxLength(24)]
    public string Number { get; set; } = string.Empty;

    public int EncounterId { get; set; }
    public Encounter? Encounter { get; set; }

    [Required]
    public string PatientId { get; set; } = string.Empty;
    public Patient? Patient { get; set; }

    [MaxLength(450)]
    public string? OrderedByUserId { get; set; }

    [MaxLength(500)]
    public string? ClinicalNotes { get; set; }

    public LabOrderStatus Status { get; set; } = LabOrderStatus.Requested;

    [MaxLength(20)]
    public string? SpecimenId { get; set; }

    public DateTime? CollectedAtUtc { get; set; }

    [MaxLength(450)]
    public string? CollectedByUserId { get; set; }

    public DateTime? ReceivedAtUtc { get; set; }
    public DateTime? ReleasedAtUtc { get; set; }

    [MaxLength(450)]
    public string? ReleasedByUserId { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public List<LabOrderItem> Items { get; set; } = [];
}
