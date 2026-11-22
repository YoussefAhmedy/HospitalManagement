namespace Hospital.BLL.Services.Abstraction;

public sealed record Kpi(string Label, string Value, string? Hint = null, string? Tone = "neutral");

public sealed record ActivityItem(string Title, string Detail, string When, string? Tone = null);

/// <summary>
/// Aggregated, role-specific workspace snapshots. Each figure is computed from live data in
/// one query round-trip so dashboards never display invented or stale numbers.
/// </summary>
public interface IWorkspaceService
{
    Task<IReadOnlyList<Kpi>> AdminAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Kpi>> DoctorAsync(string doctorId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Kpi>> ReceptionAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Kpi>> LaboratoryAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Kpi>> PharmacyAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Kpi>> BillingAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Kpi>> PatientAsync(string patientId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ActivityItem>> RecentActivityAsync(int take = 8, CancellationToken cancellationToken = default);
}
