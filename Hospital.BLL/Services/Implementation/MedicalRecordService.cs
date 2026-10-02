using System.Linq.Expressions;
using Hospital.BLL.Services.Abstraction;
using Hospital.DAL.Entities;
using Hospital.DAL.Repository.Abstraction;

namespace Hospital.BLL.Services.Implementation;

public sealed class MedicalRecordService(IMedicalRecordRepository repository) : ImedicalRecordService
{
    public Task<bool> AddMedicalRecord(MedicalRecord record) => repository.AddMedicalRecord(record);

    public IEnumerable<MedicalRecord> GetDoctorMedicalRecords(Expression<Func<MedicalRecord, bool>> predicate) =>
        repository.GetDoctorMedicalRecords(predicate);

    public IEnumerable<MedicalRecord> GetMedicalRecordsWithPatientAndDoctor() =>
        repository.GetAllMedicalRecords();

    public IQueryable<MedicalRecord> GetForPatient(string patientId) => repository.GetForPatient(patientId);

    public IQueryable<MedicalRecord> GetForDoctor(string doctorId) => repository.GetForDoctor(doctorId);
}
