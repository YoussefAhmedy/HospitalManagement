using Hospital.DAL.Entities.OwnedTypes;

namespace Hospital.BLL.Common;

/// <summary>Maps domain states to the design-system badge tones. One place, so the same
/// status always renders identically across every workspace and report.</summary>
public static class StatusTone
{
    public const string Neutral = "muted";
    public const string Info = "info";
    public const string Success = "success";
    public const string Warning = "warning";
    public const string Danger = "danger";
    public const string Teal = "teal";
    public const string Purple = "purple";

    public static (string Label, string Tone) Appointment(AppointStatus status) => status switch
    {
        AppointStatus.Pending => ("Requested", Warning),
        AppointStatus.Approved => ("Confirmed", Info),
        AppointStatus.CheckedIn => ("Checked in", Teal),
        AppointStatus.Completed => ("Completed", Success),
        AppointStatus.Cancelled => ("Cancelled", Neutral),
        AppointStatus.NotApproved => ("Declined", Danger),
        AppointStatus.NoShow => ("No-show", Danger),
        _ => (status.ToString(), Neutral)
    };

    public static (string Label, string Tone) Encounter(DAL.Entities.Clinical.EncounterStatus status) => status switch
    {
        DAL.Entities.Clinical.EncounterStatus.Registered => ("Registered", Warning),
        DAL.Entities.Clinical.EncounterStatus.Triaged => ("Triaged", Info),
        DAL.Entities.Clinical.EncounterStatus.InConsultation => ("In consultation", Teal),
        DAL.Entities.Clinical.EncounterStatus.Completed => ("Completed", Info),
        DAL.Entities.Clinical.EncounterStatus.Signed => ("Signed", Success),
        DAL.Entities.Clinical.EncounterStatus.Cancelled => ("Cancelled", Neutral),
        _ => (status.ToString(), Neutral)
    };

    public static (string Label, string Tone) Lab(DAL.Entities.Diagnostics.LabOrderStatus status) => status switch
    {
        DAL.Entities.Diagnostics.LabOrderStatus.Requested => ("Requested", Warning),
        DAL.Entities.Diagnostics.LabOrderStatus.AwaitingCollection => ("Awaiting collection", Warning),
        DAL.Entities.Diagnostics.LabOrderStatus.Collected => ("Collected", Info),
        DAL.Entities.Diagnostics.LabOrderStatus.Received => ("Received", Info),
        DAL.Entities.Diagnostics.LabOrderStatus.Processing => ("Processing", Teal),
        DAL.Entities.Diagnostics.LabOrderStatus.AwaitingVerification => ("Awaiting verification", Warning),
        DAL.Entities.Diagnostics.LabOrderStatus.Released => ("Released", Success),
        DAL.Entities.Diagnostics.LabOrderStatus.Cancelled => ("Cancelled", Neutral),
        _ => (status.ToString(), Neutral)
    };

    public static (string Label, string Tone) Imaging(DAL.Entities.Diagnostics.ImagingOrderStatus status) => status switch
    {
        DAL.Entities.Diagnostics.ImagingOrderStatus.Requested => ("Requested", Warning),
        DAL.Entities.Diagnostics.ImagingOrderStatus.Scheduled => ("Scheduled", Info),
        DAL.Entities.Diagnostics.ImagingOrderStatus.Performed => ("Performed", Teal),
        DAL.Entities.Diagnostics.ImagingOrderStatus.Reported => ("Reported", Info),
        DAL.Entities.Diagnostics.ImagingOrderStatus.Verified => ("Verified", Purple),
        DAL.Entities.Diagnostics.ImagingOrderStatus.Released => ("Released", Success),
        DAL.Entities.Diagnostics.ImagingOrderStatus.Cancelled => ("Cancelled", Neutral),
        _ => (status.ToString(), Neutral)
    };

    public static (string Label, string Tone) Prescription(DAL.Entities.Pharmacy.PrescriptionStatus status) => status switch
    {
        DAL.Entities.Pharmacy.PrescriptionStatus.Draft => ("Draft", Neutral),
        DAL.Entities.Pharmacy.PrescriptionStatus.Pending => ("Awaiting review", Warning),
        DAL.Entities.Pharmacy.PrescriptionStatus.Reviewed => ("Reviewed", Info),
        DAL.Entities.Pharmacy.PrescriptionStatus.PartiallyDispensed => ("Partially dispensed", Teal),
        DAL.Entities.Pharmacy.PrescriptionStatus.Dispensed => ("Dispensed", Success),
        DAL.Entities.Pharmacy.PrescriptionStatus.Cancelled => ("Cancelled", Neutral),
        DAL.Entities.Pharmacy.PrescriptionStatus.Rejected => ("Rejected", Danger),
        _ => (status.ToString(), Neutral)
    };

    public static (string Label, string Tone) Admission(DAL.Entities.Operations.AdmissionStatus status) => status switch
    {
        DAL.Entities.Operations.AdmissionStatus.Requested => ("Awaiting bed", Warning),
        DAL.Entities.Operations.AdmissionStatus.Admitted => ("Admitted", Teal),
        DAL.Entities.Operations.AdmissionStatus.Discharged => ("Discharged", Success),
        DAL.Entities.Operations.AdmissionStatus.Cancelled => ("Cancelled", Neutral),
        _ => (status.ToString(), Neutral)
    };

    public static (string Label, string Tone) Invoice(DAL.Entities.Billing.InvoiceStatus status) => status switch
    {
        DAL.Entities.Billing.InvoiceStatus.Draft => ("Draft", Neutral),
        DAL.Entities.Billing.InvoiceStatus.Issued => ("Issued", Info),
        DAL.Entities.Billing.InvoiceStatus.PartiallyPaid => ("Partially paid", Warning),
        DAL.Entities.Billing.InvoiceStatus.Paid => ("Paid", Success),
        DAL.Entities.Billing.InvoiceStatus.Void => ("Void", Neutral),
        _ => (status.ToString(), Neutral)
    };

    public static (string Label, string Tone) Result(DAL.Entities.Diagnostics.ResultFlag flag) => flag switch
    {
        DAL.Entities.Diagnostics.ResultFlag.Critical => ("Critical", Danger),
        DAL.Entities.Diagnostics.ResultFlag.High => ("High", Warning),
        DAL.Entities.Diagnostics.ResultFlag.Low => ("Low", Warning),
        DAL.Entities.Diagnostics.ResultFlag.Abnormal => ("Abnormal", Warning),
        _ => ("Normal", Success)
    };

    public static (string Label, string Tone) Kpi(string? tone) => tone switch
    {
        "critical" => ("Critical", Danger),
        "danger" => ("Attention", Danger),
        "attention" => ("Attention", Warning),
        "success" => ("On track", Success),
        _ => ("", Neutral)
    };
}
