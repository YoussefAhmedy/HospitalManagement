namespace Hospital.BLL.Permissions;

/// <summary>
/// Fine-grained capability catalogue. Roles are granted sets of permissions; controllers
/// authorise on policies built from these names, never on raw role strings for new code.
/// </summary>
public static class Permission
{
    // Patient master data
    public const string PatientView = "patient.view";
    public const string PatientCreate = "patient.create";
    public const string PatientEdit = "patient.edit";
    public const string PatientDelete = "patient.delete";
    public const string PatientViewSensitive = "patient.view.sensitive";

    // Scheduling
    public const string AppointmentView = "appointment.view";
    public const string AppointmentCreate = "appointment.create";
    public const string AppointmentManage = "appointment.manage";
    public const string AppointmentCheckIn = "appointment.checkin";

    // Clinical
    public const string EncounterView = "encounter.view";
    public const string EncounterCreate = "encounter.create";
    public const string EncounterDocument = "encounter.document";
    public const string EncounterSign = "encounter.sign";
    public const string TriageRecord = "triage.record";

    // Diagnostics
    public const string LabOrderCreate = "lab.order";
    public const string LabCollect = "lab.collect";
    public const string LabEnterResult = "lab.result.enter";
    public const string LabVerify = "lab.result.verify";
    public const string LabRelease = "lab.result.release";
    public const string ImagingOrder = "imaging.order";
    public const string ImagingReport = "imaging.report";
    public const string ImagingVerify = "imaging.verify";

    // Pharmacy
    public const string PrescriptionCreate = "prescription.create";
    public const string PrescriptionReview = "prescription.review";
    public const string PrescriptionDispense = "prescription.dispense";
    public const string InventoryManage = "inventory.manage";

    // Operations
    public const string AdmissionView = "admission.view";
    public const string AdmissionManage = "admission.manage";
    public const string BedManage = "bed.manage";
    public const string DischargeApprove = "discharge.approve";

    // Billing
    public const string BillingView = "billing.view";
    public const string BillingManage = "billing.manage";
    public const string BillingRefund = "billing.refund";
    public const string InsuranceManage = "insurance.manage";

    // Administration and reporting
    public const string ReportView = "report.view";
    public const string ReportFinancial = "report.financial";
    public const string ReportExport = "report.export";
    public const string AuditView = "audit.view";
    public const string UserManage = "user.manage";
    public const string SettingsManage = "settings.manage";

    public const string PolicyPrefix = "perm:";

    public static string Policy(string permission) => PolicyPrefix + permission;

    public static IReadOnlyList<string> All { get; } =
    [
        PatientView, PatientCreate, PatientEdit, PatientDelete, PatientViewSensitive,
        AppointmentView, AppointmentCreate, AppointmentManage, AppointmentCheckIn,
        EncounterView, EncounterCreate, EncounterDocument, EncounterSign, TriageRecord,
        LabOrderCreate, LabCollect, LabEnterResult, LabVerify, LabRelease,
        ImagingOrder, ImagingReport, ImagingVerify,
        PrescriptionCreate, PrescriptionReview, PrescriptionDispense, InventoryManage,
        AdmissionView, AdmissionManage, BedManage, DischargeApprove,
        BillingView, BillingManage, BillingRefund, InsuranceManage,
        ReportView, ReportFinancial, ReportExport, AuditView, UserManage, SettingsManage
    ];
}
