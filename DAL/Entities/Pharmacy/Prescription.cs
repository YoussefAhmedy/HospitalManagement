using System.ComponentModel.DataAnnotations;
using Hospital.DAL.Entities.Clinical;

namespace Hospital.DAL.Entities.Pharmacy;

public class Prescription
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
    public string? PrescribedByUserId { get; set; }

    public PrescriptionStatus Status { get; set; } = PrescriptionStatus.Pending;

    [MaxLength(450)]
    public string? ReviewedByUserId { get; set; }

    public DateTime? ReviewedAtUtc { get; set; }

    [MaxLength(500)]
    public string? PharmacistNote { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public List<PrescriptionItem> Items { get; set; } = [];
}
