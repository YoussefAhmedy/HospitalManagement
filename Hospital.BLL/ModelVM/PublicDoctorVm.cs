namespace Hospital.BLL.ModelVM;

/// <summary>Minimal directory data approved for display outside an authenticated workspace.</summary>
public sealed class PublicDoctorVm
{
    public string Id { get; init; } = string.Empty;
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string? SpecializationName { get; init; }
}
