using Hospital.DAL.Entities.Operations;

namespace Hospital.BLL.Common.Rules;

public static class BedRules
{
    /// <summary>
    /// A bed is occupied when it has an assignment that has not been released. Callers must
    /// still rely on the filtered unique index because two concurrent requests can both
    /// read "free" before either writes.
    /// </summary>
    public static bool IsOccupied(IEnumerable<BedAssignment> assignments) =>
        assignments.Any(a => a.ReleasedAtUtc is null);

    public static int OccupiedCount(IEnumerable<Bed> beds) =>
        beds.Count(bed => IsOccupied(bed.Assignments));

    public static decimal OccupancyRate(IEnumerable<Bed> beds)
    {
        var list = beds.Where(bed => bed.IsActive).ToList();
        return list.Count == 0 ? 0 : Math.Round(OccupiedCount(list) * 100m / list.Count, 1);
    }

    public static readonly IReadOnlyDictionary<AdmissionStatus, AdmissionStatus[]> Transitions =
        new Dictionary<AdmissionStatus, AdmissionStatus[]>
        {
            [AdmissionStatus.Requested] = [AdmissionStatus.Admitted, AdmissionStatus.Cancelled],
            [AdmissionStatus.Admitted] = [AdmissionStatus.Discharged],
            [AdmissionStatus.Discharged] = [],
            [AdmissionStatus.Cancelled] = []
        };

    public static bool CanTransition(AdmissionStatus from, AdmissionStatus to) =>
        Transitions.TryGetValue(from, out var allowed) && allowed.Contains(to);

    public static bool CanAssignBed(AdmissionStatus status) => status == AdmissionStatus.Admitted;
}
