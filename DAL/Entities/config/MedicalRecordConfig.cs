using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hospital.DAL.Entities.config;

internal sealed class MedicalRecordConfig : IEntityTypeConfiguration<MedicalRecord>
{
    public void Configure(EntityTypeBuilder<MedicalRecord> builder)
    {
        builder.HasOne<Patient>()
            .WithMany(patient => patient.MedicalRecords)
            .HasForeignKey(record => record.PatientID)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<Doctor>()
            .WithMany(doctor => doctor.MedicalRecords)
            .HasForeignKey(record => record.DoctorID)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(record => new { record.PatientID, record.RecordDate });
        builder.HasIndex(record => new { record.DoctorID, record.RecordDate });
    }
}
