using Hospital.DAL.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Hospital.BLL.Services.Abstraction
{
    public interface ImedicalRecordService
    {
        public IEnumerable<MedicalRecord> GetMedicalRecordsWithPatientAndDoctor();

        public Task<bool> AddMedicalRecord(MedicalRecord record);

        IEnumerable<MedicalRecord> GetDoctorMedicalRecords(Expression<Func<MedicalRecord, bool>> predicate);
        IQueryable<MedicalRecord> GetForPatient(string patientId);
        IQueryable<MedicalRecord> GetForDoctor(string doctorId);
    }
}
