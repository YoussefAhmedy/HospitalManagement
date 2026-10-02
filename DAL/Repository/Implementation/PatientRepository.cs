using Hospital.DAL.DataBase;
using Hospital.DAL.Entities;
using Hospital.DAL.Repository.Abstraction;

using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Hospital.DAL.Repository.Implementation
{
    public class PatientRepository : IPatientRepository
    {
        private readonly HospitalDbContext _context;
        private bool disposed;

        public PatientRepository(HospitalDbContext context)
        {
            _context = context;
        }

        public List<Patient> GetAllPatients()
        {
            return _context.Patients.AsNoTracking().ToList();
        }

        public IQueryable<Patient> GetPatientsByIds(IReadOnlyCollection<string> patientIds)
        {
            return _context.Patients.AsNoTracking().Where(patient => patientIds.Contains(patient.Id));
        }

        public async Task<Patient> GetPatientById(string id)
        {
            return (await _context.Patients.FindAsync(id));
        }

        public async Task AddPatient(Patient patient)
        {
            _context.Patients.Add(patient);
            await _context.SaveChangesAsync();
        }

        public async Task UpdatePatient(Patient patient)
        {
            _context.Patients.Update(patient);
            await _context.SaveChangesAsync();
        }

        public async Task DeletePatient(string id)
        {
            var patient = await GetPatientById(id);
            if (patient != null)
            {
                _context.Patients.Remove(patient);
                await _context.SaveChangesAsync();
            }
        }
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);

        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
                return;
            if (disposing)
            {
                _context.Dispose();
            }

            disposed = true;
        }

        public  async Task<Patient> GetPatient(Expression<Func<Patient, bool>> expression)
        {
            return await _context.Patients.FirstOrDefaultAsync(expression);
        }
    }
}
