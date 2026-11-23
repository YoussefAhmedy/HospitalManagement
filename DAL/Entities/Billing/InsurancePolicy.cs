using System.ComponentModel.DataAnnotations;

namespace Hospital.DAL.Entities.Billing;

/// <summary>Insurance coverage recorded against a patient. Claim adjudication is out of scope.</summary>
public class InsurancePolicy
{
    public int Id { get; set; }

    [Required]
    public string PatientId { get; set; } = string.Empty;
    public Patient? Patient { get; set; }

    [Required, MaxLength(120)]
    public string ProviderName { get; set; } = string.Empty;

    [Required, MaxLength(60)]
    public string PolicyNumber { get; set; } = string.Empty;

    [Range(0, 100)]
    public decimal CoveragePercent { get; set; } = 80;

    [MaxLength(60)]
    public string? MemberNumber { get; set; }

    public DateTime ValidFrom { get; set; } = DateTime.UtcNow.Date;
    public DateTime? ValidTo { get; set; }

    public bool IsActive { get; set; } = true;
}
