using Hospital.DAL.Entities;

namespace Hospital.BLL.ModelVM;

public sealed class PatientDashboardVm
{
    public IReadOnlyList<Appointment> UpcomingAppointments { get; init; } = [];
    public IReadOnlyList<MedicalRecord> RecentRecords { get; init; } = [];
    public int TotalAppointments { get; init; }
    public int UpcomingAppointmentCount { get; init; }
}
