using System.ComponentModel.DataAnnotations;

namespace Hospital.DAL.Entities.Organization;

/// <summary>Physical hospital site. Tenant scope is the database, not the row.</summary>
public class Branch
{
    public int Id { get; set; }

    [Required, MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    public string Code { get; set; } = string.Empty;

    [MaxLength(120)]
    public string? City { get; set; }

    [MaxLength(40)]
    public string? Phone { get; set; }

    public bool IsActive { get; set; } = true;
}
