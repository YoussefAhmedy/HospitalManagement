using Hospital.DAL.Entities;

namespace Hospital.BLL.ModelVM;

public sealed class DoctorDashboardVm
{
    public IReadOnlyList<Appointment> TodayAppointments { get; init; } = [];
    public IReadOnlyList<Appointment> UpcomingAppointments { get; init; } = [];
    public int PendingAppointments { get; init; }
}
