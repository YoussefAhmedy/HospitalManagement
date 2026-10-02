namespace HospitalManagement.Services;

/// <summary>Persists minimal audit metadata; never pass clinical content or credentials.</summary>
public interface IAuditLogger
{
    Task RecordAsync(
        string eventType,
        string? actorUserId,
        string? subjectUserId = null,
        string? resourceType = null,
        string? resourceId = null,
        CancellationToken cancellationToken = default);
}
