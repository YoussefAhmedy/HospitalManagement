using Hospital.BLL.Services.Abstraction;
using Hospital.DAL.Repository.Abstraction;
using Hospital.DAL.Entities;
using System.Linq.Expressions;

namespace Hospital.BLL.Services.Implementation
{
    public class PatientService : IPatientService
    {
        private readonly IPatientRepository _patientRepository;

        public PatientService(IPatientRepository patientRepository)
        {
            this._patientRepository = patientRepository;
        }
        public async Task<bool> AddPatient(Patient patient)
        {
            try
            {
                await _patientRepository.AddPatient(patient);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> Delete(Patient patient)
        {
            try
            {
                await _patientRepository.DeletePatient(patient.Id);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<Patient> GetPatientById(string id)
        {
            var result = await _patientRepository.GetPatientById(id);
            return result;
        }

        public IEnumerable<Patient> GetAllPatients()
        {
            return _patientRepository.GetAllPatients();
        }

        public IQueryable<Patient> GetPatientsByIds(IReadOnlyCollection<string> patientIds)
        {
            return _patientRepository.GetPatientsByIds(patientIds);
        }

        public async Task<bool> UpdatePatient(Patient patient)
        {
            try
            {

                await _patientRepository.UpdatePatient(patient);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<Patient> GetPatient(Expression<Func<Patient, bool>> expression)
        {
            return await _patientRepository.GetPatient(expression);
        }

        public IEnumerable<Patient> GetPatients(Func<Patient, bool> predicate)
        {
            return GetAllPatients().Where(predicate);
        }
    }
}

