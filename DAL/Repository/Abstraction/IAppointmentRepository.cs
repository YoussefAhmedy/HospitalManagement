using System.Linq.Expressions;
using Hospital.DAL.Entities;
using Hospital.DAL.Entities.OwnedTypes;

namespace Hospital.DAL.Repository.Abstraction;

public interface IAppointmentRepository
{
    List<Appointment> GetAllAppointments();
    Task<Appointment?> GetAppointmentById(int id);
    Task AddAppointment(Appointment appointment);
    Task UpdateAppointment(Appointment appointment);
    Task<bool> DeleteAppointment(int id);
    Task<int> CountForDoctorOnDateAsync(string doctorId, DateTime date, int? excludeAppointmentId = null);
    IQueryable<Appointment> GetAppointments(Expression<Func<Appointment, bool>> predicate);
    Task<int> UpdateAppointmentStatusAsync(Expression<Func<Appointment, bool>> predicate, AppointStatus status);
}
