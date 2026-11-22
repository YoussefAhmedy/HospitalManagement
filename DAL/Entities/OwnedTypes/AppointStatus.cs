namespace Hospital.DAL.Entities.OwnedTypes
{
    /// <summary>
    /// Appointment lifecycle. Names preserved for existing rows; new states appended so
    /// existing database values remain valid.
    /// </summary>
    public enum AppointStatus
    {
        Approved = 1,
        NotApproved = 2,
        Pending = 3,
        Cancelled = 4,
        CheckedIn = 5,
        Completed = 6,
        NoShow = 7
    }
}
