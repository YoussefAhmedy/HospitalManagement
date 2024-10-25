﻿using Hospital.DAL.Entities;
using Hospital.DAL.Entities.OwnedTypes;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Hospital.DAL.Repository.Abstraction
{
    public interface IAppointmentRepository
    {
        List<Appointment> GetAllAppointments();
        Task<Appointment> GetAppointmentById(int id);
        Task AddAppointment(Appointment appointment);
        Task UpdateAppointment(Appointment appointment);
        Task DeleteAppointment(int id);
        IEnumerable<Appointment> GetAppointments(Expression<Func<Appointment, bool>> predicate);
        void UpdateAppointmentStatus(Expression<Func<Appointment, bool>> predicate, AppointStatus Status);
        void Dispose();
    }
}
