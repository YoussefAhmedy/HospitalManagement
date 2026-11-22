using System.ComponentModel.DataAnnotations;

namespace Hospital.DAL.Entities.Billing;

/// <summary>Billable catalogue. Department-scoped so reporting can attribute revenue.</summary>
public class ServiceItem
{
    public int Id { get; set; }

    [Required, MaxLength(20)]
    public string Code { get; set; } = string.Empty;

    [Required, MaxLength(160)]
    public string Name { get; set; } = string.Empty;

    public ServiceCategory Category { get; set; } = ServiceCategory.Consultation;

    public decimal Price { get; set; }

    public int? DepartmentId { get; set; }
    public Organization.Department? Department { get; set; }

    public bool IsActive { get; set; } = true;
}
