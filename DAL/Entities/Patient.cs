using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Hospital.DAL.Entities
{
    public class Patient : ApplicationUser
    {
        /// <summary>Human-readable medical record number, unique, allocated by NumberSequence.</summary>
        [MaxLength(20)]
        public string? Mrn { get; set; }

        [MaxLength(255, ErrorMessage = "Address cannot exceed 255 characters.")]
        public string? Address { get; set; }

        [MaxLength(100, ErrorMessage = "Emergency contact cannot exceed 100 characters.")]
        public string? EmergencyContact { get; set; }

        /// <summary>Synthetic demo marker. Never set for a real patient.</summary>
        public bool IsSynthetic { get; set; }

        /// <summary>Free-text documented medical history. Allergy list is a separate table.</summary>
        [MaxLength(2000)]
        public string? MedicalHistory { get; set; }

        [MaxLength(10)]
        public string? BloodGroup { get; set; }

        public List<Allergy> Allergies { get; set; } = [];
        public List<Appointment>? Appointments { get; set; }
        public List<MedicalRecord>? MedicalRecords { get; set; }
        public List<Clinical.Encounter>? Encounters { get; set; }
    }

    public class Allergy
    {
        public int Id { get; set; }

        public string PatientId { get; set; } = string.Empty;
        public Patient? Patient { get; set; }

        [Required, MaxLength(120)]
        public string Substance { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? Reaction { get; set; }

        public AllergySeverity Severity { get; set; } = AllergySeverity.Moderate;

        [MaxLength(450)]
        public string? RecordedByUserId { get; set; }

        public DateTime RecordedAtUtc { get; set; } = DateTime.UtcNow;
    }

    public enum AllergySeverity
    {
        Mild = 1,
        Moderate = 2,
        Severe = 3
    }
}
