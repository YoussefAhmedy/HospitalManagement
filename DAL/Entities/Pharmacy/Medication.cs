using System.ComponentModel.DataAnnotations;

namespace Hospital.DAL.Entities.Pharmacy;

public class Medication
{
    public int Id { get; set; }

    [Required, MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(120)]
    public string? GenericName { get; set; }

    [MaxLength(60)]
    public string? Form { get; set; }

    [MaxLength(40)]
    public string? Strength { get; set; }

    [Required, MaxLength(40)]
    public string Unit { get; set; } = "tablet";

    public decimal UnitPrice { get; set; }

    public int ReorderLevel { get; set; } = 20;

    public bool IsActive { get; set; } = true;

    public List<StockBatch> Batches { get; set; } = [];
}
