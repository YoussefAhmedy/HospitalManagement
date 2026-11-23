using Hospital.DAL.Entities.OwnedTypes;

namespace Hospital.BLL.Common.Rules;

/// <summary>
/// Pure scheduling rules shared by the booking service and its tests. Times are minutes
/// from clinic-local midnight; an appointment with no slot keeps the legacy date-only
/// behaviour.
/// </summary>
public static class SlotRules
{
    public const int DayMinutes = 24 * 60;

    /// <summary>Generates bookable start offsets for a shift, respecting slot granularity.</summary>
    public static IReadOnlyList<int> GenerateSlots(TimeSpan shiftStart, TimeSpan shiftEnd, int slotMinutes)
    {
        if (slotMinutes < 5 || shiftEnd <= shiftStart)
        {
            return [];
        }

        var slots = new List<int>();
        var cursor = (int)shiftStart.TotalMinutes;
        var end = (int)shiftEnd.TotalMinutes;
        while (cursor + slotMinutes <= end)
        {
            slots.Add(cursor);
            cursor += slotMinutes;
        }

        return slots;
    }

    /// <summary>Returns the free slots after removing already-taken ones.</summary>
    public static IReadOnlyList<int> AvailableSlots(
        IEnumerable<int> generatedSlots,
        IEnumerable<int> takenSlots)
    {
        var taken = takenSlots.ToHashSet();
        return generatedSlots.Where(slot => !taken.Contains(slot)).ToList();
    }

    /// <summary>Two intervals overlap when each starts before the other ends.</summary>
    public static bool Overlaps(int startA, int durationA, int startB, int durationB) =>
        startA < startB + durationB && startB < startA + durationA;

    /// <summary>
    /// Only these states occupy a slot. Cancelled and declined appointments release it so
    /// the same slot can be booked again; no-show keeps the slot because the clinician's
    /// time was still committed.
    /// </summary>
    public static bool OccupiesSlot(AppointStatus status) =>
        status is AppointStatus.Pending or AppointStatus.Approved
            or AppointStatus.CheckedIn or AppointStatus.Completed
            or AppointStatus.NoShow;

    public static bool CanBook(AppointStatus status) => OccupiesSlot(status);

    /// <summary>Reception may check in only a confirmed appointment on the scheduled day.</summary>
    public static bool CanCheckIn(AppointStatus status, DateTime appointmentDate, DateTime clinicToday) =>
        status == AppointStatus.Approved && appointmentDate.Date == clinicToday.Date;

    public static bool CanCancel(AppointStatus status) =>
        status is AppointStatus.Pending or AppointStatus.Approved;

    public static bool CanReschedule(AppointStatus status) =>
        status is AppointStatus.Pending or AppointStatus.Approved;

    public static bool CanMarkNoShow(AppointStatus status, DateTime appointmentDate, DateTime clinicToday) =>
        status == AppointStatus.Approved && appointmentDate.Date < clinicToday.Date;
}
