using System.Globalization;
using System.Text;
using Hospital.BLL.Common;
using Hospital.BLL.Permissions;
using Hospital.BLL.Services.Abstraction;
using HospitalManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HospitalManagement.Controllers;

/// <summary>
/// Parameterised reporting. The HTML view, the print layout and the CSV export all consume
/// the same <see cref="ReportResult"/> produced by live queries.
/// </summary>
[Authorize(Policy = "perm:" + Permission.ReportView)]
public sealed class ReportsController(
    IReportService reports,
    IOrganizationDirectoryService directory,
    IAuditLogger audit) : Controller
{
    private string CurrentUserId() => User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;

    public IActionResult Index()
    {
        ViewData["Title"] = "Reports";
        ViewData["Section"] = "Administration";
        ViewData["Subtitle"] = "Operational, clinical and financial reporting generated from live data.";

        var allowed = reports.Catalogue
            .Where(report => User.HasClaim(AppClaimTypes.Permission, report.Permission))
            .ToList();

        return View(allowed);
    }

    public async Task<IActionResult> Run(
        string key,
        DateOnly? from = null,
        DateOnly? to = null,
        string? status = null,
        string? term = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var descriptor = reports.Find(key);
        if (descriptor is null)
        {
            return NotFound();
        }

        if (!User.HasClaim(AppClaimTypes.Permission, descriptor.Permission))
        {
            return Forbid();
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var range = new DateRange(from ?? today.AddDays(-29), to ?? today);

        var filter = new ReportFilter(range, Status: status, Search: term, Page: page, PageSize: pageSize);
        var result = await reports.RunAsync(key, filter, cancellationToken);

        ViewData["Title"] = result.Title;
        ViewData["Section"] = "Reports";
        ViewData["Subtitle"] = descriptor.Description;

        ViewBag.Descriptor = descriptor;
        ViewBag.Filters = new { from = range.From?.ToString("yyyy-MM-dd"), to = range.To?.ToString("yyyy-MM-dd"), status, term, pageSize };
        ViewBag.Directory = await directory.SnapshotAsync(cancellationToken);
        return View("Run", result);
    }

    [Authorize(Policy = "perm:" + Permission.ReportExport)]
    public async Task<IActionResult> Export(
        string key,
        DateOnly? from = null,
        DateOnly? to = null,
        string? status = null,
        string? term = null,
        CancellationToken cancellationToken = default)
    {
        var descriptor = reports.Find(key);
        if (descriptor is null)
        {
            return NotFound();
        }

        if (!User.HasClaim(AppClaimTypes.Permission, descriptor.Permission))
        {
            return Forbid();
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var range = new DateRange(from ?? today.AddDays(-29), to ?? today);
        // Exports are intentionally capped; large extracts belong in a scheduled job.
        var result = await reports.RunAsync(key, new ReportFilter(range, Status: status, Search: term, Page: 1, PageSize: 200), cancellationToken);

        await audit.RecordAsync("report.export", CurrentUserId(), null, "Report", key, cancellationToken);

        var csv = reports.ToCsv(result);
        var fileName = $"careaxis-{key}-{DateTime.UtcNow:yyyyMMdd-HHmm}.csv";
        return File(new UTF8Encoding(true).GetBytes(csv), "text/csv", fileName);
    }
}
