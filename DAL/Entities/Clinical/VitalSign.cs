using System.ComponentModel.DataAnnotations;

namespace Hospital.DAL.Entities.Clinical;

public class VitalSign
{
    public int Id { get; set; }

    public int EncounterId { get; set; }
    public Encounter? Encounter { get; set; }

    public DateTime RecordedAtUtc { get; set; } = DateTime.UtcNow;

    [MaxLength(450)]
    public string? RecordedByUserId { get; set; }

    [Range(25, 45)]
    public decimal? TemperatureC { get; set; }

    [Range(40, 300)]
    public int? SystolicBp { get; set; }

    [Range(20, 200)]
    public int? DiastolicBp { get; set; }

    [Range(20, 240)]
    public int? PulseBpm { get; set; }

    [Range(4, 80)]
    public int? RespiratoryRate { get; set; }

    [Range(40, 100)]
    public decimal? OxygenSaturation { get; set; }

    [Range(50, 300)]
    public decimal? HeightCm { get; set; }

    [Range(0.5m, 500)]
    public decimal? WeightKg { get; set; }

    [Range(0, 10)]
    public int? PainScore { get; set; }
}
