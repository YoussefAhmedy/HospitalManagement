using Hospital.BLL.Common;
using Hospital.BLL.Common.Rules;
using Hospital.BLL.Services.Abstraction;
using Hospital.DAL.DataBase;
using Hospital.DAL.Entities;
using Hospital.DAL.Entities.OwnedTypes;
using Microsoft.EntityFrameworkCore;

namespace Hospital.BLL.Services.Implementation;

public sealed class AppointmentSchedulingService(
    HospitalDbContext dbContext,
    IEncounterService encounterService,
    INumberSequenceService numbers) : IAppointmentSchedulingService
{
    public async Task<IReadOnlyList<DoctorSlotBoard>> AvailabilityAsync(
        DateTime date,
        string? doctorId,
        CancellationToken cancellationToken = default)
    {
        var day = date.DayOfWeek;

        var doctors = await dbContext.Doctors.AsNoTracking()
            .Include(d => d.Specialization)
            .Include(d => d.Schedules).ThenInclude(s => s.Shift)
            .Where(d => d.IsActive && (doctorId == null || d.Id == doctorId))
            .ToListAsync(cancellationToken);

        var taken = await dbContext.Appointments.AsNoTracking()
            .Where(a => a.AppointmentDate == date.Date && a.DoctorID != null)
            .Select(a => new { a.DoctorID, a.SlotMinutes, a.Status })
            .ToListAsync(cancellationToken);

        var boards = new List<DoctorSlotBoard>();
        foreach (var doctor in doctors)
        {
            var schedule = doctor.Schedules?
                .FirstOrDefault(s => s.Day == day && s.Status == Status.Assigned && s.Shift is not null);

            if (schedule?.Shift is null)
            {
                continue;
            }

            var generated = SlotRules.GenerateSlots(schedule.Shift.StartTIme, schedule.Shift.EndTIme, doctor.SlotMinutes);
            var takenSlots = taken
                .Where(a => a.DoctorID == doctor.Id && SlotRules.OccupiesSlot(a.Status))
                .Select(a => a.SlotMinutes);

            boards.Add(new DoctorSlotBoard(
                doctor.Id,
                $"{doctor.FirstName} {doctor.LastName}",
                doctor.Specialization?.Name,
                date.Date,
                SlotRules.AvailableSlots(generated, takenSlots),
                doctor.SlotMinutes));
        }

        return boards;
    }

    public async Task<Result<Appointment>> BookAsync(
        string patientId, string doctorId, DateTime date, int slotMinutes,
        string? notes, string actorUserId, CancellationToken cancellationToken = default)
    {
        var plantDay = DateTime.UtcNow.Date;
        if (!AppointmentRules.IsBookableDate(date, plantDay))
        {
            return Result<Appointment>.Fail("Appointments must be booked for a future date.", "invalid_date");
        }

        if (slotMinutes is < 0 or >= SlotRules.DayMinutes)
        {
            return Result<Appointment>.Fail("Select a valid time slot.", "invalid_slot");
        }

        var doctor = await dbContext.Doctors
            .Include(d => d.Schedules).ThenInclude(s => s.Shift)
            .FirstOrDefaultAsync(d => d.Id == doctorId && d.IsActive, cancellationToken);

        if (doctor is null)
        {
            return Result<Appointment>.Fail("Clinician not found or inactive.", "doctor_not_found");
        }

        var schedule = doctor.Schedules?
            .FirstOrDefault(s => s.Day == date.DayOfWeek && s.Status == Status.Assigned && s.Shift is not null);

        if (schedule?.Shift is null)
        {
            return Result<Appointment>.Fail("This clinician does not hold a clinic on the selected day.", "no_schedule");
        }

        var generated = SlotRules.GenerateSlots(schedule.Shift.StartTIme, schedule.Shift.EndTIme, doctor.SlotMinutes);
        if (!generated.Contains(slotMinutes))
        {
            return Result<Appointment>.Fail("The selected time is outside the clinician's clinic hours.", "outside_hours");
        }

        var conflict = await dbContext.Appointments.AnyAsync(
            a => a.DoctorID == doctorId
                 && a.AppointmentDate == date.Date
                 && a.SlotMinutes == slotMinutes
                 && (a.Status == AppointStatus.Pending || a.Status == AppointStatus.Approved
                     || a.Status == AppointStatus.CheckedIn || a.Status == AppointStatus.Completed
                     || a.Status == AppointStatus.NoShow),
            cancellationToken);

        if (conflict)
        {
            return Result<Appointment>.Fail("That slot has just been taken. Refresh availability and choose another time.", "slot_taken");
        }

        var count = await dbContext.Appointments.CountAsync(
            a => a.DoctorID == doctorId && a.AppointmentDate == date.Date && a.Status != AppointStatus.Cancelled,
            cancellationToken);

        if (!AppointmentRules.HasDailyCapacity(count))
        {
            return Result<Appointment>.Fail("This clinician's clinic is fully booked for that day.", "capacity");
        }

        var appointment = new Appointment
        {
            Number = await numbers.NextFormattedAsync(ClinicalNumber.Appointment, "appointment", cancellationToken),
            PatientID = patientId,
            DoctorID = doctorId,
            AppointmentDate = date.Date,
            SlotMinutes = slotMinutes,
            DurationMinutes = doctor.SlotMinutes,
            ScheduleId = schedule.Id,
            Status = AppointStatus.Pending,
            Notes = notes?.Length > 500 ? notes[..500] : notes,
            CreatedAtUtc = DateTime.UtcNow
        };

        dbContext.Appointments.Add(appointment);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // The unique slot index rejected a concurrent booking.
            return Result<Appointment>.Fail("That slot has just been taken. Refresh availability and choose another time.", "slot_taken");
        }

        return Result<Appointment>.Ok(appointment);
    }

    public async Task<Result> ConfirmAsync(int appointmentId, string actorUserId, CancellationToken cancellationToken = default)
    {
        var appointment = await dbContext.Appointments.FirstOrDefaultAsync(a => a.AppointmentID == appointmentId, cancellationToken);
        if (appointment is null)
        {
            return Result.Fail("Appointment not found.", "not_found");
        }

        if (appointment.Status != AppointStatus.Pending)
        {
            return Result.Fail($"Only pending requests can be confirmed (current: {appointment.Status}).", "invalid_state");
        }

        appointment.Status = AppointStatus.Approved;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }

    public async Task<Result<Appointment>> CheckInAsync(int appointmentId, string actorUserId, CancellationToken cancellationToken = default)
    {
        var appointment = await dbContext.Appointments
            .FirstOrDefaultAsync(a => a.AppointmentID == appointmentId, cancellationToken);

        if (appointment is null)
        {
            return Result<Appointment>.Fail("Appointment not found.", "not_found");
        }

        if (!SlotRules.CanCheckIn(appointment.Status, appointment.AppointmentDate, DateTime.UtcNow.Date))
        {
            return Result<Appointment>.Fail(
                "Check-in requires a confirmed appointment scheduled for today.", "invalid_state");
        }

        var encounter = await encounterService.RegisterAsync(
            appointment.PatientID!, appointment.DoctorID, null,
            DAL.Entities.Clinical.EncounterType.Outpatient, appointment.Notes, actorUserId, cancellationToken);

        if (!encounter.Succeeded || encounter.Value is null)
        {
            return Result<Appointment>.Fail(encounter.Error ?? "Could not open an encounter.", encounter.Code);
        }

        appointment.Status = AppointStatus.CheckedIn;
        appointment.CheckedInAtUtc = DateTime.UtcNow;
        appointment.EncounterId = encounter.Value.Id;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<Appointment>.Ok(appointment);
    }

    public async Task<Result> CancelAsync(int appointmentId, string actorUserId, string? reason, CancellationToken cancellationToken = default)
    {
        var appointment = await dbContext.Appointments.FirstOrDefaultAsync(a => a.AppointmentID == appointmentId, cancellationToken);
        if (appointment is null)
        {
            return Result.Fail("Appointment not found.", "not_found");
        }

        if (!SlotRules.CanCancel(appointment.Status))
        {
            return Result.Fail("This appointment can no longer be cancelled.", "invalid_state");
        }

        appointment.Status = AppointStatus.Cancelled;
        if (!string.IsNullOrWhiteSpace(reason))
        {
            var combined = string.IsNullOrWhiteSpace(appointment.Notes)
                ? $"Cancelled: {reason}"
                : $"{appointment.Notes} | Cancelled: {reason}";
            appointment.Notes = combined.Length > 500 ? combined[..500] : combined;
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }

    public async Task<Result> RescheduleAsync(int appointmentId, DateTime newDate, int newSlotMinutes, string actorUserId, CancellationToken cancellationToken = default)
    {
        var appointment = await dbContext.Appointments.FirstOrDefaultAsync(a => a.AppointmentID == appointmentId, cancellationToken);
        if (appointment is null)
        {
            return Result.Fail("Appointment not found.", "not_found");
        }

        if (!SlotRules.CanReschedule(appointment.Status))
        {
            return Result.Fail("This appointment can no longer be rescheduled.", "invalid_state");
        }

        var conflict = await dbContext.Appointments.AnyAsync(
            a => a.AppointmentID != appointmentId
                 && a.DoctorID == appointment.DoctorID
                 && a.AppointmentDate == newDate.Date
                 && a.SlotMinutes == newSlotMinutes
                 && a.Status != AppointStatus.Cancelled
                 && a.Status != AppointStatus.NotApproved,
            cancellationToken);

        if (conflict)
        {
            return Result.Fail("The new slot is already taken.", "slot_taken");
        }

        appointment.AppointmentDate = newDate.Date;
        appointment.SlotMinutes = newSlotMinutes;
        appointment.Status = AppointStatus.Pending;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }

    public async Task<Result> MarkNoShowAsync(int appointmentId, string actorUserId, CancellationToken cancellationToken = default)
    {
        var appointment = await dbContext.Appointments.FirstOrDefaultAsync(a => a.AppointmentID == appointmentId, cancellationToken);
        if (appointment is null)
        {
            return Result.Fail("Appointment not found.", "not_found");
        }

        if (!SlotRules.CanMarkNoShow(appointment.Status, appointment.AppointmentDate, DateTime.UtcNow.Date))
        {
            return Result.Fail("Only a confirmed appointment from a past day can be marked as a no-show.", "invalid_state");
        }

        appointment.Status = AppointStatus.NoShow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }

    public async Task<PagedResult<AppointmentListItem>> QueryAsync(
        DateTime? date, AppointStatus? status, string? doctorId, string? patientId,
        string? term, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = dbContext.Appointments.AsNoTracking().AsQueryable();

        if (date is not null)
        {
            query = query.Where(a => a.AppointmentDate == date.Value.Date);
        }

        if (status is not null)
        {
            query = query.Where(a => a.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(doctorId))
        {
            query = query.Where(a => a.DoctorID == doctorId);
        }

        if (!string.IsNullOrWhiteSpace(patientId))
        {
            query = query.Where(a => a.PatientID == patientId);
        }

        if (!string.IsNullOrWhiteSpace(term))
        {
            var trimmed = term.Trim();
            query = query.Where(a =>
                (a.Number != null && a.Number.Contains(trimmed)) ||
                (a.Patient != null && (a.Patient.FirstName.Contains(trimmed) || a.Patient.LastName.Contains(trimmed))) ||
                (a.Patient != null && a.Patient.Mrn != null && a.Patient.Mrn.Contains(trimmed)));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(a => a.AppointmentDate).ThenBy(a => a.SlotMinutes)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(a => new AppointmentListItem(
                a.AppointmentID,
                a.Number ?? "—",
                a.AppointmentDate,
                a.SlotMinutes,
                a.Patient == null ? "—" : a.Patient.FirstName + " " + a.Patient.LastName,
                a.Patient == null ? "—" : a.Patient.Mrn ?? "—",
                a.Doctor == null ? "—" : a.Doctor.FirstName + " " + a.Doctor.LastName,
                a.Status,
                a.Notes))
            .ToListAsync(cancellationToken);

        return new PagedResult<AppointmentListItem>(items, total, page, pageSize);
    }

    public async Task<DepartmentQueue> QueueAsync(DateTime date, CancellationToken cancellationToken = default)
    {
        var day = date.Date;
        var rows = await dbContext.Appointments.AsNoTracking()
            .Where(a => a.AppointmentDate == day)
            .OrderBy(a => a.SlotMinutes)
            .Select(a => new AppointmentListItem(
                a.AppointmentID,
                a.Number ?? "—",
                a.AppointmentDate,
                a.SlotMinutes,
                a.Patient == null ? "—" : a.Patient.FirstName + " " + a.Patient.LastName,
                a.Patient == null ? "—" : a.Patient.Mrn ?? "—",
                a.Doctor == null ? "—" : a.Doctor.FirstName + " " + a.Doctor.LastName,
                a.Status,
                a.Notes))
            .ToListAsync(cancellationToken);

        return new DepartmentQueue(
            rows.Count(r => r.Status == AppointStatus.CheckedIn),
            rows.Count(r => r.Status == AppointStatus.Approved),
            rows.Count(r => r.Status == AppointStatus.Pending),
            rows.Count(r => r.Status == AppointStatus.Completed),
            rows.Where(r => r.Status == AppointStatus.CheckedIn || r.Status == AppointStatus.Approved).Take(12).ToList());
    }
}
