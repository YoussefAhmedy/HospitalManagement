using Hospital.BLL.Common;
using Hospital.DAL.Entities;
using Hospital.DAL.Entities.OwnedTypes;

namespace Hospital.BLL.Services.Abstraction;

public sealed record DoctorSlotBoard(string DoctorId, string DoctorName, string? Specialty, DateTime Date, IReadOnlyList<int> AvailableSlots, int SlotMinutes);

public sealed record AppointmentListItem(
    int Id, string Number, DateTime Date, int SlotMinutes, string PatientName, string Mrn,
    string DoctorName, AppointStatus Status, string? Notes);

/// <summary>
/// Slot-based scheduling layered on the legacy appointment entity. Availability is derived
/// from the clinician's weekly schedule; conflicts are rejected before they reach the
/// unique database index.
/// </summary>
public interface IAppointmentSchedulingService
{
    Task<IReadOnlyList<DoctorSlotBoard>> AvailabilityAsync(DateTime date, string? doctorId, CancellationToken cancellationToken = default);

    Task<Result<Appointment>> BookAsync(
        string patientId, string doctorId, DateTime date, int slotMinutes,
        string? notes, string actorUserId, CancellationToken cancellationToken = default);

    Task<Result> ConfirmAsync(int appointmentId, string actorUserId, CancellationToken cancellationToken = default);

    Task<Result<Appointment>> CheckInAsync(int appointmentId, string actorUserId, CancellationToken cancellationToken = default);

    Task<Result> CancelAsync(int appointmentId, string actorUserId, string? reason, CancellationToken cancellationToken = default);

    Task<Result> RescheduleAsync(int appointmentId, DateTime newDate, int newSlotMinutes, string actorUserId, CancellationToken cancellationToken = default);

    Task<Result> MarkNoShowAsync(int appointmentId, string actorUserId, CancellationToken cancellationToken = default);

    Task<PagedResult<AppointmentListItem>> QueryAsync(
        DateTime? date, AppointStatus? status, string? doctorId, string? patientId,
        string? term, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<DepartmentQueue> QueueAsync(DateTime date, CancellationToken cancellationToken = default);
}

public sealed record DepartmentQueue(int CheckedIn, int Waiting, int Confirmed, int Completed, IReadOnlyList<AppointmentListItem> Next);
