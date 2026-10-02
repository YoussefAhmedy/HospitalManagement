using Hospital.DAL.Entities;
using System.Collections.Generic;

namespace Hospital.DAL.Repository.Abstraction
{
    public interface IDoctorRepository
    {
        List<Doctor> GetAllDoctors();
        IQueryable<Doctor> GetPublicDoctors();
        Task<Doctor> GetDoctorById(string id);
        Task AddDoctor(Doctor doctor);
        Task UpdateDoctor(Doctor doctor);
        Task DeleteDoctor(string id);
        Task<Doctor> GetDoctorAndSchedulesById(string id);
        void Dispose();
    }
}
