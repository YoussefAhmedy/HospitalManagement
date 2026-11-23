using System.ComponentModel.DataAnnotations;

namespace Hospital.DAL.Entities.Diagnostics;

/// <summary>Catalogue entry. Reference ranges drive the abnormal flag; they are configuration,
/// not clinical judgement, and must be approved by the laboratory medical director.</summary>
public class LabTest
{
    public int Id { get; set; }

    [Required, MaxLength(20)]
    public string Code { get; set; } = string.Empty;

    [Required, MaxLength(160)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(60)]
    public string? Category { get; set; }

    public SpecimenType Specimen { get; set; } = SpecimenType.Blood;

    [MaxLength(20)]
    public string Unit { get; set; } = string.Empty;

    public decimal? NormalLow { get; set; }
    public decimal? NormalHigh { get; set; }
    public decimal? CriticalLow { get; set; }
    public decimal? CriticalHigh { get; set; }

    public decimal Price { get; set; }
    public int TurnaroundHours { get; set; } = 24;
    public bool IsActive { get; set; } = true;
}
