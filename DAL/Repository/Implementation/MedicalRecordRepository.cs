using System.Linq.Expressions;
using Hospital.DAL.DataBase;
using Hospital.DAL.Entities;
using Hospital.DAL.Repository.Abstraction;
using Microsoft.EntityFrameworkCore;

namespace Hospital.DAL.Repository.Implementation;

public sealed class MedicalRecordRepository(HospitalDbContext context) : IMedicalRecordRepository
{
    private bool _disposed;

    public IEnumerable<MedicalRecord> GetAllMedicalRecords() =>
        context.MedicalRecords
            .AsNoTracking()
            .Include(record => record.Patient)
            .Include(record => record.Doctor).ThenInclude(doctor => doctor!.Specialization);

    public MedicalRecord? GetMedicalRecordById(int id) =>
        context.MedicalRecords
            .Include(record => record.Patient)
            .Include(record => record.Doctor)
            .FirstOrDefault(record => record.MedicalRecordID == id);

    public async Task<bool> AddMedicalRecord(MedicalRecord medicalRecord)
    {
        try
        {
            await context.MedicalRecords.AddAsync(medicalRecord);
            await context.SaveChangesAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void UpdateMedicalRecord(MedicalRecord medicalRecord)
    {
        context.MedicalRecords.Update(medicalRecord);
        context.SaveChanges();
    }

    public void DeleteMedicalRecord(int id)
    {
        var medicalRecord = GetMedicalRecordById(id);
        if (medicalRecord is not null)
        {
            context.MedicalRecords.Remove(medicalRecord);
            context.SaveChanges();
        }
    }

    public IEnumerable<MedicalRecord> GetDoctorMedicalRecords(Expression<Func<MedicalRecord, bool>> predicate) =>
        context.MedicalRecords
            .AsNoTracking()
            .Include(record => record.Patient)
            .Where(predicate);

    public IQueryable<MedicalRecord> GetForPatient(string patientId) =>
        context.MedicalRecords
            .AsNoTracking()
            .Where(record => record.PatientID == patientId)
            .Include(record => record.Doctor).ThenInclude(doctor => doctor!.Specialization);

    public IQueryable<MedicalRecord> GetForDoctor(string doctorId) =>
        context.MedicalRecords
            .AsNoTracking()
            .Where(record => record.DoctorID == doctorId)
            .Include(record => record.Patient);

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }
        if (disposing)
        {
            context.Dispose();
        }
        _disposed = true;
    }
}
