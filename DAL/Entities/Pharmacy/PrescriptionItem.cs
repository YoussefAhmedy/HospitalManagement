using System.ComponentModel.DataAnnotations;

namespace Hospital.DAL.Entities.Pharmacy;

public class PrescriptionItem
{
    public int Id { get; set; }

    public int PrescriptionId { get; set; }
    public Prescription? Prescription { get; set; }

    public int MedicationId { get; set; }
    public Medication? Medication { get; set; }

    [Required, MaxLength(120)]
    public string Dose { get; set; } = string.Empty;

    [Required, MaxLength(120)]
    public string Frequency { get; set; } = string.Empty;

    [Required, MaxLength(80)]
    public string Duration { get; set; } = string.Empty;

    [MaxLength(60)]
    public string? Route { get; set; }

    public int Quantity { get; set; } = 1;

    public int DispensedQuantity { get; set; }

    [MaxLength(300)]
    public string? Instructions { get; set; }
}
