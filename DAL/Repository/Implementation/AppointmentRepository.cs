using System.Data;
using System.Linq.Expressions;
using Hospital.DAL.DataBase;
using Hospital.DAL.Entities;
using Hospital.DAL.Entities.OwnedTypes;
using Hospital.DAL.Repository.Abstraction;
using Microsoft.EntityFrameworkCore;

namespace Hospital.DAL.Repository.Implementation;

public sealed class AppointmentRepository(HospitalDbContext context) : IAppointmentRepository
{
    public List<Appointment> GetAllAppointments() =>
        context.Appointments
            .AsNoTracking()
            .Include(appointment => appointment.Patient)
            .Include(appointment => appointment.Doctor)
            .ToList();

    public Task<Appointment?> GetAppointmentById(int id) =>
        context.Appointments
            .Include(appointment => appointment.Patient)
            .Include(appointment => appointment.Doctor).ThenInclude(doctor => doctor!.Specialization)
            .Include(appointment => appointment.Schedule).ThenInclude(schedule => schedule!.Shift)
            .SingleOrDefaultAsync(appointment => appointment.AppointmentID == id);

    public async Task AddAppointment(Appointment appointment)
    {
        if (string.IsNullOrWhiteSpace(appointment.DoctorID))
        {
            throw new InvalidOperationException("An appointment must have a doctor.");
        }

        var date = appointment.AppointmentDate.Date;
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var currentCount = await CountForDoctorOnDateAsync(appointment.DoctorID, date);
        if (!AppointmentRules.HasDailyCapacity(currentCount))
        {
            throw new InvalidOperationException("The doctor has reached the daily appointment capacity.");
        }

        context.Appointments.Add(appointment);
        await context.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    public async Task UpdateAppointment(Appointment appointment)
    {
        if (string.IsNullOrWhiteSpace(appointment.DoctorID))
        {
            throw new InvalidOperationException("An appointment must have a doctor.");
        }

        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        if (appointment.Status is not (AppointStatus.Cancelled or AppointStatus.NotApproved))
        {
            var currentCount = await CountForDoctorOnDateAsync(
                appointment.DoctorID, appointment.AppointmentDate.Date, appointment.AppointmentID);
            if (!AppointmentRules.HasDailyCapacity(currentCount))
            {
                throw new InvalidOperationException("The doctor has reached the daily appointment capacity.");
            }
        }

        context.Appointments.Update(appointment);
        await context.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    public async Task<bool> DeleteAppointment(int id)
    {
        var appointment = await GetAppointmentById(id);
        if (appointment is null)
        {
            return false;
        }

        context.Appointments.Remove(appointment);
        await context.SaveChangesAsync();
        return true;
    }

    public Task<int> CountForDoctorOnDateAsync(string doctorId, DateTime date, int? excludeAppointmentId = null)
    {
        var start = date.Date;
        var end = start.AddDays(1);
        var query = context.Appointments.Where(appointment =>
            appointment.DoctorID == doctorId &&
            appointment.AppointmentDate >= start &&
            appointment.AppointmentDate < end &&
            appointment.Status != AppointStatus.Cancelled &&
            appointment.Status != AppointStatus.NotApproved);

        if (excludeAppointmentId.HasValue)
        {
            query = query.Where(appointment => appointment.AppointmentID != excludeAppointmentId.Value);
        }

        return query.CountAsync();
    }

    public IQueryable<Appointment> GetAppointments(Expression<Func<Appointment, bool>> predicate) =>
        context.Appointments
            .AsNoTracking()
            .Include(appointment => appointment.Doctor)
            .Include(appointment => appointment.Patient)
            .Include(appointment => appointment.Schedule)!.ThenInclude(schedule => schedule!.Shift)
            .Where(predicate);

    public Task<int> UpdateAppointmentStatusAsync(
        Expression<Func<Appointment, bool>> predicate,
        AppointStatus status) =>
        context.Appointments
            .Where(predicate)
            .ExecuteUpdateAsync(update => update.SetProperty(appointment => appointment.Status, status));
}
