namespace Hospital.DAL.Entities.Diagnostics;

/// <summary>
/// Laboratory order lifecycle. A result is only clinically usable once it reaches
/// <see cref="LabOrderStatus.Released"/>; the UI must never present an earlier state as final.
/// </summary>
public enum LabOrderStatus
{
    Requested = 1,
    AwaitingCollection = 2,
    Collected = 3,
    Received = 4,
    Processing = 5,
    AwaitingVerification = 6,
    Released = 7,
    Cancelled = 8
}

public enum SpecimenType
{
    Blood = 1,
    Serum = 2,
    Urine = 3,
    Stool = 4,
    Swab = 5,
    Tissue = 6,
    Other = 9
}

public enum ResultFlag
{
    Normal = 0,
    Low = 1,
    High = 2,
    Abnormal = 3,
    Critical = 4
}

public enum ImagingOrderStatus
{
    Requested = 1,
    Scheduled = 2,
    Performed = 3,
    Reported = 4,
    Verified = 5,
    Released = 6,
    Cancelled = 7
}
