namespace Hospital.BLL.Helpers;

/// <summary>Custom claim types issued at sign-in from the role → permission map.</summary>
public static class AppClaimTypes
{
    public const string Permission = "careaxis:perm";
    public const string Clinical = "careaxis:clinical";
    public const string Branch = "careaxis:branch";
    public const string DoctorId = "careaxis:doctor";
}
