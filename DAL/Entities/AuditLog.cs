using System.ComponentModel.DataAnnotations;

namespace Hospital.DAL.Entities;

/// <summary>
/// Records security-sensitive activity metadata only. Medical content, credentials,
/// tokens, and request bodies must never be copied into this table.
/// </summary>
public sealed class AuditLog
{
    public long Id { get; set; }

    [Required, MaxLength(64)]
    public string EventType { get; set; } = string.Empty;

    [MaxLength(450)]
    public string? ActorUserId { get; set; }

    [MaxLength(450)]
    public string? SubjectUserId { get; set; }

    [MaxLength(64)]
    public string? ResourceType { get; set; }

    [MaxLength(64)]
    public string? ResourceId { get; set; }

    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
}
