namespace Hospital.BLL.Services.Abstraction;

public sealed record LookupOption(int Id, string Label, string? Group = null);

public sealed record OrganizationSnapshot(
    IReadOnlyList<LookupOption> Branches,
    IReadOnlyList<LookupOption> Departments,
    IReadOnlyList<LookupOption> Doctors,
    IReadOnlyList<LookupOption> LabTests,
    IReadOnlyList<LookupOption> Medications,
    IReadOnlyList<LookupOption> ServiceItems);

/// <summary>Read-only reference data for form selects and report filters.</summary>
public interface IOrganizationDirectoryService
{
    Task<OrganizationSnapshot> SnapshotAsync(CancellationToken cancellationToken = default);
}
