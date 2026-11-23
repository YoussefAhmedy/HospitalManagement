using Hospital.DAL.Entities.Pharmacy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hospital.DAL.Entities.config;

internal sealed class MedicationConfig : IEntityTypeConfiguration<Medication>
{
    public void Configure(EntityTypeBuilder<Medication> builder)
    {
        builder.ToTable("Medications");
        builder.Property(m => m.UnitPrice).HasPrecision(18, 2);
        builder.HasIndex(m => m.Name);
        builder.HasMany(m => m.Batches).WithOne(b => b.Medication!).HasForeignKey(b => b.MedicationId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class StockBatchConfig : IEntityTypeConfiguration<StockBatch>
{
    public void Configure(EntityTypeBuilder<StockBatch> builder)
    {
        builder.ToTable("StockBatches");
        builder.HasIndex(b => new { b.MedicationId, b.ExpiryDate });
        builder.HasIndex(b => b.BatchNumber);
    }
}

internal sealed class PrescriptionConfig : IEntityTypeConfiguration<Prescription>
{
    public void Configure(EntityTypeBuilder<Prescription> builder)
    {
        builder.ToTable("Prescriptions");
        builder.HasIndex(p => p.Number).IsUnique();
        builder.HasIndex(p => new { p.Status, p.CreatedAtUtc });
        builder.HasIndex(p => new { p.PatientId, p.CreatedAtUtc });
        builder.HasOne(p => p.Encounter).WithMany().HasForeignKey(p => p.EncounterId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(p => p.Items).WithOne(i => i.Prescription!).HasForeignKey(i => i.PrescriptionId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class PrescriptionItemConfig : IEntityTypeConfiguration<PrescriptionItem>
{
    public void Configure(EntityTypeBuilder<PrescriptionItem> builder)
    {
        builder.ToTable("PrescriptionItems");
        builder.HasOne(i => i.Medication).WithMany().HasForeignKey(i => i.MedicationId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class StockMovementConfig : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> builder)
    {
        builder.ToTable("StockMovements");
        builder.HasIndex(m => new { m.MedicationId, m.OccurredAtUtc });
        builder.HasOne(m => m.StockBatch).WithMany().HasForeignKey(m => m.StockBatchId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(m => m.Prescription).WithMany().HasForeignKey(m => m.PrescriptionId).OnDelete(DeleteBehavior.SetNull);
    }
}
