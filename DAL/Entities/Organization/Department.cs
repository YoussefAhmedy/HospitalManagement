using System.ComponentModel.DataAnnotations;

namespace Hospital.DAL.Entities.Organization;

public class Department
{
    public int Id { get; set; }

    [Required, MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    public string Code { get; set; } = string.Empty;

    public int? BranchId { get; set; }
    public Branch? Branch { get; set; }

    public bool IsActive { get; set; } = true;
}
