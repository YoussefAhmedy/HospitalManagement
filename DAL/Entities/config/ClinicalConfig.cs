using Hospital.DAL.Entities.Clinical;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hospital.DAL.Entities.config;

internal sealed class EncounterConfig : IEntityTypeConfiguration<Encounter>
{
    public void Configure(EntityTypeBuilder<Encounter> builder)
    {
        builder.ToTable("Encounters");
        builder.Property(e => e.Number).HasMaxLength(24).IsRequired();
        builder.HasIndex(e => e.Number).IsUnique();
        builder.HasIndex(e => new { e.PatientId, e.RegisteredAtUtc });
        builder.HasIndex(e => new { e.Status, e.RegisteredAtUtc });
        builder.HasOne(e => e.Department).WithMany().HasForeignKey(e => e.DepartmentId).OnDelete(DeleteBehavior.SetNull);
        builder.HasMany(e => e.Vitals).WithOne(v => v.Encounter!).HasForeignKey(v => v.EncounterId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(e => e.Diagnoses).WithOne(d => d.Encounter!).HasForeignKey(d => d.EncounterId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(e => e.Notes).WithOne(n => n.Encounter!).HasForeignKey(n => n.EncounterId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class VitalSignConfig : IEntityTypeConfiguration<VitalSign>
{
    public void Configure(EntityTypeBuilder<VitalSign> builder)
    {
        builder.ToTable("VitalSigns");
        builder.Property(v => v.TemperatureC).HasPrecision(4, 1);
        builder.Property(v => v.OxygenSaturation).HasPrecision(4, 1);
        builder.Property(v => v.HeightCm).HasPrecision(5, 1);
        builder.Property(v => v.WeightKg).HasPrecision(5, 1);
        builder.HasIndex(v => new { v.EncounterId, v.RecordedAtUtc });
    }
}

internal sealed class DiagnosisConfig : IEntityTypeConfiguration<Diagnosis>
{
    public void Configure(EntityTypeBuilder<Diagnosis> builder)
    {
        builder.ToTable("Diagnoses");
        builder.HasIndex(d => d.EncounterId);
    }
}

internal sealed class ClinicalNoteConfig : IEntityTypeConfiguration<ClinicalNote>
{
    public void Configure(EntityTypeBuilder<ClinicalNote> builder)
    {
        builder.ToTable("ClinicalNotes");
        builder.HasOne(n => n.AmendsNote).WithMany().HasForeignKey(n => n.AmendsNoteId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(n => new { n.EncounterId, n.CreatedAtUtc });
    }
}
