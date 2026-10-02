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

        [Required(ErrorMessage = "Status is required.")]
        
        public AppointStatus Status { get; set; } 

        [MaxLength(500, ErrorMessage = "Notes cannot exceed 500 characters.")]
        public string? Notes { get; set; }

        public int? ScheduleId {  get; set; }
        public Schedule? Schedule { get; set; }

        public string? PatientID { get; set; }

        public Patient? Patient { get; set; }
        
        public string? DoctorID { get; set; }

        public Doctor? Doctor { get; set; }

        /// <summary>SQL Server row version used to reject concurrent appointment edits.</summary>
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    }
}
