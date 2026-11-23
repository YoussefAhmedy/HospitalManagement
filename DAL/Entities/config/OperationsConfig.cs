using Hospital.DAL.Entities.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hospital.DAL.Entities.config;

internal sealed class WardConfig : IEntityTypeConfiguration<Ward>
{
    public void Configure(EntityTypeBuilder<Ward> builder)
    {
        builder.ToTable("Wards");
        builder.HasIndex(w => w.Name);
        builder.HasMany(w => w.Rooms).WithOne(r => r.Ward!).HasForeignKey(r => r.WardId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class RoomConfig : IEntityTypeConfiguration<Room>
{
    public void Configure(EntityTypeBuilder<Room> builder)
    {
        builder.ToTable("Rooms");
        builder.Property(r => r.DailyRate).HasPrecision(18, 2);
        builder.HasIndex(r => new { r.WardId, r.Number }).IsUnique();
        builder.HasMany(r => r.Beds).WithOne(b => b.Room!).HasForeignKey(b => b.RoomId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class BedConfig : IEntityTypeConfiguration<Bed>
{
    public void Configure(EntityTypeBuilder<Bed> builder)
    {
        builder.ToTable("Beds");
        builder.HasIndex(b => new { b.RoomId, b.Label }).IsUnique();
        builder.HasMany(b => b.Assignments).WithOne(a => a.Bed!).HasForeignKey(a => a.BedId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class AdmissionConfig : IEntityTypeConfiguration<Admission>
{
    public void Configure(EntityTypeBuilder<Admission> builder)
    {
        builder.ToTable("Admissions");
        builder.HasIndex(a => a.Number).IsUnique();
        builder.HasIndex(a => new { a.Status, a.RequestedAtUtc });
        builder.HasIndex(a => a.PatientId);
        builder.HasOne(a => a.Encounter).WithMany().HasForeignKey(a => a.EncounterId).OnDelete(DeleteBehavior.SetNull);
        builder.HasMany(a => a.BedAssignments).WithOne(b => b.Admission!).HasForeignKey(b => b.AdmissionId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class BedAssignmentConfig : IEntityTypeConfiguration<BedAssignment>
{
    public void Configure(EntityTypeBuilder<BedAssignment> builder)
    {
        builder.ToTable("BedAssignments");
        builder.Ignore(a => a.IsActive);
        // THE critical occupancy invariant: a bed can have at most one open assignment.
        builder.HasIndex(a => a.BedId)
            .IsUnique()
            .HasFilter("[ReleasedAtUtc] IS NULL")
            .HasDatabaseName("UX_BedAssignments_ActiveOccupancy");
        builder.HasIndex(a => new { a.AdmissionId, a.AssignedAtUtc });
    }
}

internal sealed class NumberSequenceConfig : IEntityTypeConfiguration<NumberSequence>
{
    public void Configure(EntityTypeBuilder<NumberSequence> builder)
    {
        builder.ToTable("NumberSequences");
        builder.HasKey(s => s.Key);
        builder.Property(s => s.RowVersion).IsRowVersion();
    }
}
