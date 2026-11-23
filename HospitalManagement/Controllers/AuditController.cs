using System.Security.Claims;
using Hospital.BLL.Common;
using Hospital.BLL.Permissions;
using Hospital.BLL.Services.Abstraction;
using Hospital.DAL.DataBase;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HospitalManagement.Controllers;

/// <summary>
/// Audit trail viewer. The trail is append-only: there is no create, edit or delete route,
/// and reads are themselves audited.
/// </summary>
[Authorize(Policy = "perm:" + Permission.AuditView)]
public sealed class AuditController(
    HospitalDbContext dbContext,
    IOrganizationDirectoryService directory,
    IAuditLogger audit) : Controller
{
    private string CurrentUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    public async Task<IActionResult> Index(string? term, string? eventType, string? resourceType, DateTime? from, DateTime? to, int page = 1, CancellationToken cancellationToken = default)
    {
        var query = dbContext.AuditLogs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(eventType))
        {
            query = query.Where(l => l.EventType == eventType);
        }

        if (!string.IsNullOrWhiteSpace(resourceType))
        {
            query = query.Where(l => l.ResourceType == resourceType);
        }

        if (from is not null)
        {
            var start = from.Value.Date;
            query = query.Where(l => l.OccurredAtUtc >= start);
        }

        if (to is not null)
        {
            var end = to.Value.Date.AddDays(1);
            query = query.Where(l => l.OccurredAtUtc < end);
        }

        if (!string.IsNullOrWhiteSpace(term))
        {
            var trimmed = term.Trim();
            query = query.Where(l => l.EventType.Contains(trimmed)
                || l.ResourceId == trimmed
                || l.ActorUserId == trimmed
                || l.ResourceType == trimmed);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(l => l.OccurredAtUtc)
            .Skip((Math.Max(1, page) - 1) * 50).Take(50)
            .ToListAsync(cancellationToken);

        ViewData["Title"] = "Audit trail";
        ViewData["Section"] = "Governance";
        ViewData["Subtitle"] = "Append-only record of security-relevant activity. Metadata only — no clinical content is stored here.";

        ViewBag.Total = total;
        ViewBag.Term = term;
        ViewBag.EventType = eventType;
        ViewBag.ResourceType = resourceType;
        ViewBag.From = from?.ToString("yyyy-MM-dd");
        ViewBag.To = to?.ToString("yyyy-MM-dd");
        ViewBag.Page = Math.Max(1, page);
        ViewBag.PageSize = 50;
        ViewBag.EventTypes = await dbContext.AuditLogs.AsNoTracking().Select(l => l.EventType).Distinct().OrderBy(t => t).ToListAsync(cancellationToken);
        ViewBag.ResourceTypes = await dbContext.AuditLogs.AsNoTracking().Where(l => l.ResourceType != null).Select(l => l.ResourceType!).Distinct().OrderBy(t => t).ToListAsync(cancellationToken);
        ViewBag.Directory = await directory.SnapshotAsync(cancellationToken);

        await audit.RecordAsync("audit.view", CurrentUserId(), null, "AuditLog", null, cancellationToken);
        return View(new PagedResult<Hospital.DAL.Entities.AuditLog>(items, total, Math.Max(1, page), 50));
    }
}
