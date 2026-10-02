using Hospital.DAL.DataBase;
using Hospital.DAL.Entities;

namespace HospitalManagement.Services;

public sealed class AuditLogger(
    HospitalDbContext dbContext,
    ILogger<AuditLogger> logger) : IAuditLogger
{
    public async Task RecordAsync(
        string eventType,
        string? actorUserId,
        string? subjectUserId = null,
        string? resourceType = null,
        string? resourceId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);

        var entry = new AuditLog
        {
            EventType = eventType,
            ActorUserId = actorUserId,
            SubjectUserId = subjectUserId,
            ResourceType = resourceType,
            ResourceId = resourceId,
            OccurredAtUtc = DateTime.UtcNow
        };

        dbContext.AuditLogs.Add(entry);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            // The exception is retained for server-side diagnosis; event fields contain
            // identifiers only and no medical payload, email, token, or request body.
            logger.LogError(exception, "Audit event persistence failed for event type {EventType}.", eventType);
            throw;
        }
    }
}
