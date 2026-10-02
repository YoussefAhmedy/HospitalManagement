using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hospital.DAL.Entities.config;

internal sealed class AuditLogConfig : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");
        builder.HasKey(entry => entry.Id);
        builder.Property(entry => entry.EventType).HasMaxLength(64).IsRequired();
        builder.Property(entry => entry.ActorUserId).HasMaxLength(450);
        builder.Property(entry => entry.SubjectUserId).HasMaxLength(450);
        builder.Property(entry => entry.ResourceType).HasMaxLength(64);
        builder.Property(entry => entry.ResourceId).HasMaxLength(64);
        builder.HasIndex(entry => entry.OccurredAtUtc);
        builder.HasIndex(entry => new { entry.ActorUserId, entry.OccurredAtUtc });
        builder.HasIndex(entry => new { entry.ResourceType, entry.ResourceId, entry.OccurredAtUtc });
    }
}
