using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hospital.DAL.Entities.config;

internal sealed class AppointmentConfig : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> builder)
    {
        builder.HasOne<Patient>()
            .WithMany(patient => patient.Appointments)
            .HasForeignKey(appointment => appointment.PatientID)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<Doctor>()
            .WithMany(doctor => doctor.Appointments)
            .HasForeignKey(appointment => appointment.DoctorID)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Property(appointment => appointment.RowVersion).IsRowVersion();
        builder.HasIndex(appointment => new { appointment.DoctorID, appointment.AppointmentDate });
        builder.HasIndex(appointment => new { appointment.PatientID, appointment.AppointmentDate });
    }
}
