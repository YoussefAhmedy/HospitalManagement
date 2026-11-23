using Hospital.BLL.Common;

namespace Hospital.BLL.Services.Abstraction;

public sealed record ReportColumn(string Key, string Label, string? Format = null);

/// <summary>
/// A report is a genuine database query plus summary rows and column metadata. Both the
/// HTML view and the CSV export consume the same object, so the two can never diverge.
/// </summary>
public sealed record ReportResult(
    string Key,
    string Title,
    string? Description,
    IReadOnlyList<ReportColumn> Columns,
    IReadOnlyList<IReadOnlyList<string>> Rows,
    IReadOnlyList<KeyValuePair<string, string>> Summary,
    int TotalRows,
    int Page,
    int PageSize,
    DateTime GeneratedAtUtc,
    string? FilterSummary)
{
    public bool HasMore => Page * PageSize < TotalRows;
    public bool HasPrevious => Page > 1;

    public static ReportResult Empty(string key, string title, string? description = null) =>
        new(key, title, description, [], [], [], 0, 1, 50, DateTime.UtcNow, null);
}

public sealed record ReportFilter(
    DateRange Range,
    int? DepartmentId = null,
    string? DoctorId = null,
    string? Status = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 50,
    CancellationToken CancellationToken = default);

public sealed record ReportDescriptor(string Key, string Title, string Description, string Permission);

/// <summary>
/// Reporting engine. Every method executes real LINQ against the live dataset; nothing is
/// hard-coded. The report catalogue is permission-gated per report.
/// </summary>
public interface IReportService
{
    IReadOnlyList<ReportDescriptor> Catalogue { get; }

    Task<ReportResult> RunAsync(string key, ReportFilter filter, CancellationToken cancellationToken = default);

    string ToCsv(ReportResult report);

    /// <summary>Resolves a report descriptor, or null when the key is unknown.</summary>
    ReportDescriptor? Find(string key);
}
