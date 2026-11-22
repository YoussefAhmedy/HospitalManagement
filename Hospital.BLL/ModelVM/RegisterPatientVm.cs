using System.ComponentModel.DataAnnotations;
using Hospital.DAL.Entities.Clinical;

namespace Hospital.BLL.ModelVM;

/// <summary>Staff-facing patient registration. Deliberately minimal; clinical detail is
/// captured in the chart, not in the registration form.</summary>
public sealed class RegisterPatientVm
{
    [Required(ErrorMessage = "First name is required.")]
    [StringLength(50, ErrorMessage = "First name cannot exceed 50 characters.")]
    [Display(Name = "First name")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Last name is required.")]
    [StringLength(50, ErrorMessage = "Last name cannot exceed 50 characters.")]
    [Display(Name = "Last name")]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Phone(ErrorMessage = "Enter a valid phone number.")]
    [Display(Name = "Phone")]
    [StringLength(30)]
    public string? PhoneNumber { get; set; }

    [Display(Name = "Date of birth")]
    [DataType(DataType.Date)]
    public DateOnly? DateOfBirth { get; set; }

    [Display(Name = "Sex")]
    public PatientSex Sex { get; set; } = PatientSex.Unknown;

    [Display(Name = "Blood group")]
    public string? BloodGroup { get; set; }

    [StringLength(255)]
    [Display(Name = "Address")]
    public string? Address { get; set; }

    [StringLength(100)]
    [Display(Name = "Emergency contact")]
    public string? EmergencyContact { get; set; }

    [StringLength(2000)]
    [Display(Name = "Documented history")]
    public string? MedicalHistory { get; set; }

    [Display(Name = "Confirm possible duplicate")]
    public bool ConfirmDuplicate { get; set; }
}
