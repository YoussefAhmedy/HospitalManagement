namespace Hospital.DAL.Entities.Billing;

public enum ServiceCategory
{
    Consultation = 1,
    Procedure = 2,
    Laboratory = 3,
    Radiology = 4,
    Medication = 5,
    Room = 6,
    Other = 9
}

public enum InvoiceStatus
{
    Draft = 1,
    Issued = 2,
    PartiallyPaid = 3,
    Paid = 4,
    Void = 5
}

public enum PaymentMethod
{
    Cash = 1,
    Card = 2,
    BankTransfer = 3,
    Insurance = 4,
    Other = 9
}
