using Hospital.BLL.ModelVM;
using Hospital.BLL.Services.Abstraction;
using Hospital.DAL.Entities;
using Hospital.DAL.Repository.Abstraction;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Hospital.BLL.Services.Implementation
{
    public class DoctorService : IDoctorService
    {
        private readonly IDoctorRepository doctorRepository;

        public DoctorService(IDoctorRepository doctorRepository) 
        {
            this.doctorRepository = doctorRepository;
        }
        public List<Doctor> GetAllDoctors()
        {
           return doctorRepository.GetAllDoctors();
        }

        public List<PublicDoctorVm> GetPublicDoctorProfiles(int maximumResults)
        {
            var take = Math.Clamp(maximumResults, 1, 12);
            return doctorRepository.GetPublicDoctors()
                .OrderBy(doctor => doctor.FirstName)
                .ThenBy(doctor => doctor.LastName)
                .Take(take)
                .Select(doctor => new PublicDoctorVm
                {
                    Id = doctor.Id,
                    FirstName = doctor.FirstName,
                    LastName = doctor.LastName,
                    SpecializationName = doctor.Specialization == null ? null : doctor.Specialization.Name
                })
                .ToList();
        }

        public List<DoctorVm> GetDoctorVms()
        {
            return doctorRepository.GetAllDoctors().Select(d =>
            
                new DoctorVm { Id = d.Id,FirstName = d.FirstName, LastName = d.LastName, Salary = d.Salary, Image = d.Image ,Specialization = d.Specialization?.Name ?? "no sepcialization" }
            ).ToList();
        }

        public async Task<Doctor> DoctorByIdAsync(string id)
        {
            return await doctorRepository.GetDoctorById(id);
        }

        public async Task<Doctor> GetDoctorAndSchedulesById(string id)
        {
           return await  doctorRepository.GetDoctorAndSchedulesById(id);
        }
    }
}
