using System.ComponentModel.DataAnnotations;
using Hospital.DAL.Entities.Clinical;

namespace Hospital.DAL.Entities.Diagnostics;

/// <summary>
/// Imaging request and radiologist report. Image binaries and DICOM are intentionally
/// out of scope; the report text and linkage to the correct encounter are in scope.
/// </summary>
public class ImagingOrder
{
    public int Id { get; set; }

    [Required, MaxLength(24)]
    public string Number { get; set; } = string.Empty;

    public int EncounterId { get; set; }
    public Encounter? Encounter { get; set; }

    [Required]
    public string PatientId { get; set; } = string.Empty;
    public Patient? Patient { get; set; }

    [Required, MaxLength(60)]
    public string Modality { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string StudyName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? ClinicalIndication { get; set; }

    public ImagingOrderStatus Status { get; set; } = ImagingOrderStatus.Requested;

    [MaxLength(450)]
    public string? OrderedByUserId { get; set; }

    [MaxLength(450)]
    public string? RadiologistUserId { get; set; }

    public DateTime? ScheduledForUtc { get; set; }
    public DateTime? PerformedAtUtc { get; set; }

    [MaxLength(4000)]
    public string? Findings { get; set; }

    [MaxLength(1000)]
    public string? Impression { get; set; }

    [MaxLength(450)]
    public string? VerifiedByUserId { get; set; }

    public DateTime? VerifiedAtUtc { get; set; }

    public decimal Price { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
