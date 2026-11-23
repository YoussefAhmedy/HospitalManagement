using System.ComponentModel.DataAnnotations;

namespace Hospital.DAL.Entities.Billing;

/// <summary>
/// Recorded payment against an invoice. No gateway integration is implemented; entries
/// are recorded by an authorised billing officer and are fully audited.
/// </summary>
public class Payment
{
    public int Id { get; set; }

    public int InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }

    [Required, MaxLength(24)]
    public string ReceiptNumber { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public PaymentMethod Method { get; set; } = PaymentMethod.Cash;

    [MaxLength(120)]
    public string? Reference { get; set; }

    public bool IsRefund { get; set; }

    [Range(0, 100)]
    public int RefundPercentage { get; set; }

    public DateTime PaidAtUtc { get; set; } = DateTime.UtcNow;

    [MaxLength(450)]
    public string? ReceivedByUserId { get; set; }

    public bool RequiresApproval { get; set; }

    [MaxLength(450)]
    public string? ApprovedByUserId { get; set; }

    public DateTime? ApprovedAtUtc { get; set; }
}
