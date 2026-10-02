using Hospital.DAL.Entities;
using Hospital.DAL.Entities.OwnedTypes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Hospital.BLL.Services.Abstraction
{
    public interface IAppointmentService
    {
        Task<bool> AddAppointment(Appointment appointment);
        IQueryable<Appointment> GetAppointments(Expression<Func<Appointment, bool>> predicate);

        Task<Appointment?> GetAppointmentById(int id);

        Task<bool> DeleteAppointment(int id);
        Task<int> CountForDoctorOnDateAsync(string doctorId, DateTime date, int? excludeAppointmentId = null);
        Task<bool> UpdateAppointment(Appointment appointment);

        Task UpdateAppointmentStatus(AppointStatus status);

        
    }
}
