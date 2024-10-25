﻿using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hospital.DAL.Entities
{
    public class Doctor : ApplicationUser
    {
        [Required]
        [Range(15000,20000)]
        public decimal Salary { get; set; }

        
        public decimal SessionPrice { get; set; }

        [ForeignKey("Specialization")]
        public int? SpecializationId { get; set; }
        public Specialization? Specialization { get; set; }

        public List<Schedule>? Schedules { get; set; }
        public List<Appointment>? Appointments { get; set; }
        public List<MedicalRecord>? MedicalRecords { get; set; }
    }
}
