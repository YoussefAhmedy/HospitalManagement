namespace Hospital.DAL.Entities.Pharmacy;

public enum PrescriptionStatus
{
    Draft = 1,
    Pending = 2,
    Reviewed = 3,
    PartiallyDispensed = 4,
    Dispensed = 5,
    Cancelled = 6,
    Rejected = 7
}

public enum StockMovementType
{
    Receipt = 1,
    Dispense = 2,
    Adjustment = 3,
    Return = 4,
    Expiry = 5
}
