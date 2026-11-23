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

        builder.HasOne(appointment => appointment.Encounter)
            .WithMany()
            .HasForeignKey(appointment => appointment.EncounterId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Property(appointment => appointment.RowVersion).IsRowVersion();
        builder.Property(appointment => appointment.Number).HasMaxLength(24);
        builder.HasIndex(appointment => new { appointment.DoctorID, appointment.AppointmentDate });
        builder.HasIndex(appointment => new { appointment.PatientID, appointment.AppointmentDate });
        builder.HasIndex(appointment => new { appointment.AppointmentDate, appointment.Status });

        // A clinician cannot hold two appointments in the same slot on the same date.
        // Cancelled / declined rows are excluded so a released slot can be rebooked.
        builder.HasIndex(appointment => new { appointment.DoctorID, appointment.AppointmentDate, appointment.SlotMinutes })
            .IsUnique()
            .HasFilter("[DoctorID] IS NOT NULL AND [Status] NOT IN (2, 4)")
            .HasDatabaseName("UX_Appointments_DoctorSlot");
    }
}
