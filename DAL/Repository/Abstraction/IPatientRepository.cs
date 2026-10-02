using Hospital.DAL.Entities;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Hospital.DAL.Repository.Abstraction
{
    public interface IPatientRepository
    {
        List<Patient> GetAllPatients();
        IQueryable<Patient> GetPatientsByIds(IReadOnlyCollection<string> patientIds);
        Task<Patient> GetPatientById(string id);
        Task AddPatient(Patient patient);
        Task UpdatePatient(Patient patient);
        Task DeletePatient(string id);

        Task<Patient> GetPatient(Expression<Func<Patient,bool>> expression);
        void Dispose();
    }
}
