using System.ComponentModel.DataAnnotations;

namespace Hospital.DAL.Entities.Pharmacy;

/// <summary>Append-only inventory ledger. Quantity is signed: positive in, negative out.</summary>
public class StockMovement
{
    public long Id { get; set; }

    public int MedicationId { get; set; }
    public Medication? Medication { get; set; }

    public int? StockBatchId { get; set; }
    public StockBatch? StockBatch { get; set; }

    public StockMovementType Type { get; set; }

    public int Quantity { get; set; }

    [MaxLength(300)]
    public string? Reason { get; set; }

    public int? PrescriptionId { get; set; }
    public Prescription? Prescription { get; set; }

    [MaxLength(450)]
    public string? PerformedByUserId { get; set; }

    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
}
