using System.ComponentModel.DataAnnotations;

namespace Hospital.DAL.Entities.Billing;

public class InvoiceLine
{
    public int Id { get; set; }

    public int InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }

    public int? ServiceItemId { get; set; }
    public ServiceItem? ServiceItem { get; set; }

    [Required, MaxLength(200)]
    public string Description { get; set; } = string.Empty;

    public int Quantity { get; set; } = 1;

    public decimal UnitPrice { get; set; }

    public decimal Amount => Quantity * UnitPrice;
}
