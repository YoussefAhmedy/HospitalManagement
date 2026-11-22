using Hospital.DAL.Entities.Clinical;

namespace Hospital.BLL.Common.Rules;

public static class EncounterRules
{
    public static readonly IReadOnlyDictionary<EncounterStatus, EncounterStatus[]> Transitions =
        new Dictionary<EncounterStatus, EncounterStatus[]>
        {
            [EncounterStatus.Registered] = [EncounterStatus.Triaged, EncounterStatus.InConsultation, EncounterStatus.Cancelled],
            [EncounterStatus.Triaged] = [EncounterStatus.InConsultation, EncounterStatus.Cancelled],
            [EncounterStatus.InConsultation] = [EncounterStatus.Completed, EncounterStatus.Cancelled],
            [EncounterStatus.Completed] = [EncounterStatus.Signed],
            [EncounterStatus.Signed] = [],
            [EncounterStatus.Cancelled] = []
        };

    public static bool CanTransition(EncounterStatus from, EncounterStatus to) =>
        Transitions.TryGetValue(from, out var allowed) && allowed.Contains(to);

    /// <summary>Signed encounters are legally closed: no edits, only amendments.</summary>
    public static bool IsEditable(EncounterStatus status) =>
        status is EncounterStatus.Registered or EncounterStatus.Triaged or EncounterStatus.InConsultation;

    public static bool CanSign(EncounterStatus status, bool hasDiagnosis, bool hasNote) =>
        status == EncounterStatus.Completed && hasDiagnosis && hasNote;

    /// <summary>A signed note is immutable; corrections must create an amendment.</summary>
    public static bool CanEditNote(ClinicalNote note) => note.Status == NoteStatus.Draft;
}
