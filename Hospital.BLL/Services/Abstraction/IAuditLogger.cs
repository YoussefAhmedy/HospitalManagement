namespace Hospital.BLL.Services.Abstraction;

/// <summary>
/// Persists minimal audit metadata for security-relevant operations. Implementations must
/// never receive clinical content, credentials, tokens or request bodies.
/// </summary>
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
