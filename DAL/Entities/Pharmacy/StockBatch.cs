using System.ComponentModel.DataAnnotations;

namespace Hospital.DAL.Entities.Pharmacy;

/// <summary>Inventory is tracked per batch so expiry and traceability remain accurate.</summary>
public class StockBatch
{
    public int Id { get; set; }

    public int MedicationId { get; set; }
    public Medication? Medication { get; set; }

    [Required, MaxLength(60)]
    public string BatchNumber { get; set; } = string.Empty;

    public DateTime ExpiryDate { get; set; }

    public int QuantityOnHand { get; set; }

    [MaxLength(120)]
    public string? Supplier { get; set; }

    public DateTime ReceivedAtUtc { get; set; } = DateTime.UtcNow;
}
