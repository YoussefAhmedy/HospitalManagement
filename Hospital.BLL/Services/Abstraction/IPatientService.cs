using Hospital.DAL.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Hospital.BLL.Services.Abstraction
{
    public interface IPatientService
    {
        Task<Patient> GetPatientById(string id);

        Task<bool> AddPatient(Patient patient);

        Task<bool> Delete(Patient patient);

        IEnumerable<Patient> GetAllPatients();
        IQueryable<Patient> GetPatientsByIds(IReadOnlyCollection<string> patientIds);
        IEnumerable<Patient> GetPatients(Func<Patient, bool> predicate);
        Task<bool> UpdatePatient(Patient patient);
        Task<Patient> GetPatient(Expression<Func<Patient, bool>> expression);


    }
}
