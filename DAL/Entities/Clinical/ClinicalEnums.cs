namespace Hospital.DAL.Entities.Clinical;

/// <summary>Lifecycle of an outpatient or emergency encounter. Order is meaningful.</summary>
public enum EncounterStatus
{
    Registered = 1,
    Triaged = 2,
    InConsultation = 3,
    Completed = 4,
    Signed = 5,
    Cancelled = 6
}

public enum EncounterType
{
    Outpatient = 1,
    Emergency = 2,
    Inpatient = 3
}

public enum NoteStatus
{
    Draft = 1,
    Signed = 2
}

public enum DiagnosisType
{
    Primary = 1,
    Secondary = 2,
    Differential = 3
}

public enum PatientSex
{
    Unknown = 0,
    Female = 1,
    Male = 2
}
