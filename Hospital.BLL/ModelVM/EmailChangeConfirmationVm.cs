using System.ComponentModel.DataAnnotations;

namespace Hospital.BLL.ModelVM;

public sealed class EmailChangeConfirmationVm
{
    [Required]
    [EmailAddress]
    public string NewEmail { get; set; } = string.Empty;

    [Required]
    public string Code { get; set; } = string.Empty;
}
