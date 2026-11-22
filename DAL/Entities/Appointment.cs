using Hospital.DAL.Entities.OwnedTypes;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Hospital.DAL.Entities
{
    public class Appointment
    {
        [Key]
        public int AppointmentID { get; set; }

        [Required(ErrorMessage = "Appointment date is required.")]
        [DataType(DataType.Date)]
        public DateTime AppointmentDate { get; set; }

        /// <summary>Minutes from clinic-local midnight. Together with date + doctor this is unique.</summary>
        [Range(0, 1439)]
        public int SlotMinutes { get; set; }

        [Range(5, 240)]
        public int DurationMinutes { get; set; } = 20;

        [Required(ErrorMessage = "Status is required.")]
        public AppointStatus Status { get; set; }

        [MaxLength(500, ErrorMessage = "Notes cannot exceed 500 characters.")]
        public string? Notes { get; set; }

        [MaxLength(24)]
        public string? Number { get; set; }

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime? CheckedInAtUtc { get; set; }
        public DateTime? CompletedAtUtc { get; set; }

        public int? ScheduleId { get; set; }
        public Schedule? Schedule { get; set; }

        public string? PatientID { get; set; }
        public Patient? Patient { get; set; }

        public string? DoctorID { get; set; }
        public Doctor? Doctor { get; set; }

        /// <summary>Encounter created when the patient is checked in.</summary>
        public int? EncounterId { get; set; }
        public Clinical.Encounter? Encounter { get; set; }

        /// <summary>SQL Server row version used to reject concurrent appointment edits.</summary>
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    }
}
