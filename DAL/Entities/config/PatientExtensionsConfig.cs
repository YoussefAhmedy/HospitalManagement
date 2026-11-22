using Hospital.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hospital.DAL.Entities.config;

internal sealed class AllergyConfig : IEntityTypeConfiguration<Allergy>
{
    public void Configure(EntityTypeBuilder<Allergy> builder)
    {
        builder.ToTable("Allergies");
        builder.HasIndex(a => a.PatientId);
        builder.HasOne(a => a.Patient).WithMany(p => p.Allergies).HasForeignKey(a => a.PatientId).OnDelete(DeleteBehavior.Cascade);
    }
}
