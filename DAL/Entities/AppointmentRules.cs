using Hospital.DAL.Entities.OwnedTypes;

namespace Hospital.DAL.Entities;

/// <summary>
/// Shared rules for the legacy appointment workflow. Dates are clinic-local calendar
/// dates (not instants); production deployments must configure and document the clinic
/// time zone before accepting bookings.
/// </summary>
public static class AppointmentRules
{
    public const int DailyCapacity = 50;

    public static bool IsBookableDate(DateTime requestedDate, DateTime clinicToday) =>
        requestedDate != default && requestedDate.Date > clinicToday.Date;

    public static bool HasDailyCapacity(int currentBookingCount, int capacity = DailyCapacity) =>
        currentBookingCount >= 0 && capacity > 0 && currentBookingCount < capacity;

    public static bool IsScheduledForDate(DateTime appointmentDate, DayOfWeek scheduledDay) =>
        appointmentDate.DayOfWeek == scheduledDay;

    public static bool IsValidStatus(int status) =>
        Enum.IsDefined((AppointStatus)status);
}
