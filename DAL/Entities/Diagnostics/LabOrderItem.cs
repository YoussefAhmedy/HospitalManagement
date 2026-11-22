using System.ComponentModel.DataAnnotations;

namespace Hospital.DAL.Entities.Diagnostics;

public class LabOrderItem
{
    public int Id { get; set; }

    public int LabOrderId { get; set; }
    public LabOrder? LabOrder { get; set; }

    public int LabTestId { get; set; }
    public LabTest? LabTest { get; set; }

    [MaxLength(40)]
    public string? ResultValue { get; set; }

    public decimal? NumericResult { get; set; }

    [Range(0, 1000)]
    public decimal? ResultPrice { get; set; }

    public ResultFlag Flag { get; set; } = ResultFlag.Normal;

    [MaxLength(450)]
    public string? EnteredByUserId { get; set; }

    public DateTime? EnteredAtUtc { get; set; }

    [MaxLength(450)]
    public string? VerifiedByUserId { get; set; }

    public DateTime? VerifiedAtUtc { get; set; }

    [MaxLength(500)]
    public string? Comment { get; set; }
}
