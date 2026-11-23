using Hospital.DAL.Entities.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hospital.DAL.Entities.config;

internal sealed class LabTestConfig : IEntityTypeConfiguration<LabTest>
{
    public void Configure(EntityTypeBuilder<LabTest> builder)
    {
        builder.ToTable("LabTests");
        builder.HasIndex(t => t.Code).IsUnique();
        builder.Property(t => t.NormalLow).HasPrecision(12, 4);
        builder.Property(t => t.NormalHigh).HasPrecision(12, 4);
        builder.Property(t => t.CriticalLow).HasPrecision(12, 4);
        builder.Property(t => t.CriticalHigh).HasPrecision(12, 4);
        builder.Property(t => t.Price).HasPrecision(18, 2);
    }
}

internal sealed class LabOrderConfig : IEntityTypeConfiguration<LabOrder>
{
    public void Configure(EntityTypeBuilder<LabOrder> builder)
    {
        builder.ToTable("LabOrders");
        builder.HasIndex(o => o.Number).IsUnique();
        builder.HasIndex(o => new { o.Status, o.CreatedAtUtc });
        builder.HasIndex(o => new { o.PatientId, o.CreatedAtUtc });
        builder.HasOne(o => o.Encounter).WithMany().HasForeignKey(o => o.EncounterId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(o => o.Items).WithOne(i => i.LabOrder!).HasForeignKey(i => i.LabOrderId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class LabOrderItemConfig : IEntityTypeConfiguration<LabOrderItem>
{
    public void Configure(EntityTypeBuilder<LabOrderItem> builder)
    {
        builder.ToTable("LabOrderItems");
        builder.Property(i => i.NumericResult).HasPrecision(12, 4);
        builder.Property(i => i.ResultPrice).HasPrecision(18, 2);
        builder.HasOne(i => i.LabTest).WithMany().HasForeignKey(i => i.LabTestId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(i => new { i.LabOrderId, i.LabTestId }).IsUnique();
    }
}

internal sealed class ImagingOrderConfig : IEntityTypeConfiguration<ImagingOrder>
{
    public void Configure(EntityTypeBuilder<ImagingOrder> builder)
    {
        builder.ToTable("ImagingOrders");
        builder.HasIndex(o => o.Number).IsUnique();
        builder.HasIndex(o => new { o.Status, o.ScheduledForUtc });
        builder.HasIndex(o => new { o.PatientId, o.CreatedAtUtc });
        builder.Property(o => o.Price).HasPrecision(18, 2);
        builder.HasOne(o => o.Encounter).WithMany().HasForeignKey(o => o.EncounterId).OnDelete(DeleteBehavior.Restrict);
    }
}
