namespace Hospital.BLL.Permissions;

/// <summary>
/// Fine-grained capability catalogue. Roles are granted sets of permissions; controllers
/// authorise on policies built from these names, never on raw role strings for new code.
/// <para>
/// The catalogue contains 43 permissions. Four of them — <see cref="PatientDelete"/>,
/// <see cref="PatientViewSensitive"/>, <see cref="InsuranceManage"/> and
/// <see cref="SettingsManage"/> — are <em>reserved</em>: they describe intended capability and are
/// listed in the grant matrix, but no authorisation attribute references them, so holding one
/// grants nothing today. That distinction is asserted by <c>AuthorizationCoverageTests</c> so the
/// number cannot drift unnoticed.
/// </para>
/// </summary>
public static class Permission
{
    // Patient master data
    public const string PatientView = "patient.view";
    public const string PatientCreate = "patient.create";
    public const string PatientEdit = "patient.edit";

    // Reserved. Both are defined so the grant matrix and the documentation can name the intended
    // capability, but neither is enforced by any authorisation attribute yet: patient records are
    // not deletable (retention is a legal decision, not a UI one) and sensitive-field redaction is
    // backend work. A permission that gates nothing must never be presented as a control.
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
    public const string ImagingView = "imaging.view";
    public const string ImagingOrder = "imaging.order";
    public const string ImagingManage = "imaging.manage";
    public const string ImagingReport = "imaging.report";
    public const string ImagingVerify = "imaging.verify";
    public const string ImagingRelease = "imaging.release";

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
    /// <summary>Reserved: no endpoint is gated on this yet. See the note above.</summary>
    public const string InsuranceManage = "insurance.manage";

    // Administration and reporting
    public const string ReportView = "report.view";
    public const string ReportFinancial = "report.financial";
    public const string ReportExport = "report.export";
    public const string AuditView = "audit.view";
    public const string UserManage = "user.manage";
    /// <summary>Reserved: no endpoint is gated on this yet. See the note above.</summary>
    public const string SettingsManage = "settings.manage";

    public const string PolicyPrefix = "perm:";

    public static string Policy(string permission) => PolicyPrefix + permission;

    public static IReadOnlyList<string> All { get; } =
    [
        PatientView, PatientCreate, PatientEdit, PatientDelete, PatientViewSensitive,
        AppointmentView, AppointmentCreate, AppointmentManage, AppointmentCheckIn,
        EncounterView, EncounterCreate, EncounterDocument, EncounterSign, TriageRecord,
        LabOrderCreate, LabCollect, LabEnterResult, LabVerify, LabRelease,
        ImagingView, ImagingOrder, ImagingManage, ImagingReport, ImagingVerify, ImagingRelease,
        PrescriptionCreate, PrescriptionReview, PrescriptionDispense, InventoryManage,
        AdmissionView, AdmissionManage, BedManage, DischargeApprove,
        BillingView, BillingManage, BillingRefund, InsuranceManage,
        ReportView, ReportFinancial, ReportExport, AuditView, UserManage, SettingsManage
    ];
}
