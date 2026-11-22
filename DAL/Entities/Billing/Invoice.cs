using System.ComponentModel.DataAnnotations;

namespace Hospital.DAL.Entities.Billing;

public class Invoice
{
    public int Id { get; set; }

    [Required, MaxLength(24)]
    public string Number { get; set; } = string.Empty;

    [Required]
    public string PatientId { get; set; } = string.Empty;
    public Patient? Patient { get; set; }

    public int? EncounterId { get; set; }
    public Clinical.Encounter? Encounter { get; set; }

    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;

    public DateTime IssuedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? DueAtUtc { get; set; }

    [MaxLength(300)]
    public string? Notes { get; set; }

    public decimal InsuranceCoveredAmount { get; set; }

    public List<InvoiceLine> Lines { get; set; } = [];
    public List<Payment> Payments { get; set; } = [];

    public decimal Total => Lines.Sum(line => line.Amount);
    public decimal Paid => Payments.Sum(payment => payment.Amount);
    public decimal Balance => Total - InsuranceCoveredAmount - Paid;
}
