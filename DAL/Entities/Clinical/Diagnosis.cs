using System.ComponentModel.DataAnnotations;

namespace Hospital.DAL.Entities.Clinical;

public class Diagnosis
{
    public int Id { get; set; }

    public int EncounterId { get; set; }
    public Encounter? Encounter { get; set; }

    [MaxLength(16)]
    public string? Code { get; set; }

    [Required, MaxLength(300)]
    public string Description { get; set; } = string.Empty;

    public DiagnosisType Type { get; set; } = DiagnosisType.Primary;

    [MaxLength(450)]
    public string? RecordedByUserId { get; set; }

    public DateTime RecordedAtUtc { get; set; } = DateTime.UtcNow;
}
