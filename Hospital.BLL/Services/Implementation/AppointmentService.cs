using System.Linq.Expressions;
using Hospital.BLL.Services.Abstraction;
using Hospital.DAL.Entities;
using Hospital.DAL.Entities.OwnedTypes;
using Hospital.DAL.Repository.Abstraction;

namespace Hospital.BLL.Services.Implementation;

public sealed class AppointmentService(IAppointmentRepository appointmentRepository) : IAppointmentService
{
    public async Task<bool> AddAppointment(Appointment appointment)
    {
        try
        {
            await appointmentRepository.AddAppointment(appointment);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public IQueryable<Appointment> GetAppointments(Expression<Func<Appointment, bool>> predicate) =>
        appointmentRepository.GetAppointments(predicate);

    public Task<Appointment?> GetAppointmentById(int id) =>
        appointmentRepository.GetAppointmentById(id);

    public Task<int> CountForDoctorOnDateAsync(string doctorId, DateTime date, int? excludeAppointmentId = null) =>
        appointmentRepository.CountForDoctorOnDateAsync(doctorId, date, excludeAppointmentId);

    public async Task<bool> DeleteAppointment(int id)
    {
        try
        {
            return await appointmentRepository.DeleteAppointment(id);
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> UpdateAppointment(Appointment appointment)
    {
        try
        {
            await appointmentRepository.UpdateAppointment(appointment);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task UpdateAppointmentStatus(AppointStatus status)
    {
        await appointmentRepository.UpdateAppointmentStatusAsync(
            appointment => appointment.Status == AppointStatus.Pending && appointment.AppointmentDate < DateTime.Today,
            status);
    }
}
