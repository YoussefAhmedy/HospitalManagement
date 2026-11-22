namespace Hospital.BLL.Services.Abstraction;

public sealed record LookupOption(int Id, string Label, string? Group = null);

/// <summary>
/// A person reference. Clinicians and staff are Identity users, so their identifier is the
/// string user id (never a synthetic ordinal).
/// </summary>
public sealed record PersonOption(string Id, string Label, string? Group = null);

public sealed record OrganizationSnapshot(
    IReadOnlyList<LookupOption> Branches,
    IReadOnlyList<LookupOption> Departments,
    IReadOnlyList<PersonOption> Doctors,
    IReadOnlyList<LookupOption> LabTests,
    IReadOnlyList<LookupOption> Medications,
    IReadOnlyList<LookupOption> ServiceItems,
    IReadOnlyList<PersonOption> Staff);

/// <summary>Read-only reference data for form selects and report filters.</summary>
public interface IOrganizationDirectoryService
{
    Task<OrganizationSnapshot> SnapshotAsync(CancellationToken cancellationToken = default);
}
