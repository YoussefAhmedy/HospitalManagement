using Hospital.DAL.Entities;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Hospital.DAL.Repository.Abstraction
{
    public interface IMedicalRecordRepository
    {
        IEnumerable<MedicalRecord> GetAllMedicalRecords();
        MedicalRecord? GetMedicalRecordById(int id);
        Task<bool> AddMedicalRecord(MedicalRecord medicalRecord);
        void UpdateMedicalRecord(MedicalRecord medicalRecord);
        void DeleteMedicalRecord(int id);
        IEnumerable<MedicalRecord> GetDoctorMedicalRecords(Expression<Func<MedicalRecord, bool>> predicate);
        IQueryable<MedicalRecord> GetForPatient(string patientId);
        IQueryable<MedicalRecord> GetForDoctor(string doctorId);
        void Dispose();
    }
}
