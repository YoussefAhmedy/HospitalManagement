using Hospital.BLL.Helpers;

namespace Hospital.BLL.Permissions;

/// <summary>
/// Least-privilege role → permission map. This is the single source of truth used to seed
/// Identity role claims at start-up, so the database always reflects this file.
/// </summary>
public static class RolePermissions
{
    private static readonly string[] ClinicalDocumenter =
    [
        Permission.PatientView, Permission.AppointmentView, Permission.AppointmentManage,
        Permission.EncounterView, Permission.EncounterCreate, Permission.EncounterDocument,
        Permission.EncounterSign, Permission.TriageRecord,
        Permission.LabOrderCreate, Permission.ImagingView, Permission.ImagingOrder,
        Permission.PrescriptionCreate, Permission.AdmissionView, Permission.ReportView
    ];

    private static readonly Dictionary<Role, string[]> Map = new()
    {
        [Role.Admin] =
        [
            Permission.PatientView, Permission.PatientCreate, Permission.PatientEdit,
            Permission.AppointmentView, Permission.AppointmentCreate, Permission.AppointmentManage,
            Permission.AppointmentCheckIn, Permission.EncounterView, Permission.LabOrderCreate,
            Permission.ImagingView, Permission.ImagingOrder, Permission.ImagingManage,
            Permission.PrescriptionCreate, Permission.AdmissionView,
            Permission.AdmissionManage, Permission.BedManage, Permission.BillingView,
            Permission.ReportView, Permission.ReportExport, Permission.UserManage
        ],
        [Role.SuperAdmin] = [.. Permission.All],
        [Role.HospitalAdmin] = [.. Permission.All],
        [Role.Doctor] = ClinicalDocumenter,
        [Role.Nurse] =
        [
            Permission.PatientView, Permission.AppointmentView, Permission.TriageRecord,
            Permission.EncounterView, Permission.EncounterDocument, Permission.LabCollect,
            Permission.AdmissionView, Permission.BedManage, Permission.ReportView
        ],
        [Role.Receptionist] =
        [
            Permission.PatientView, Permission.PatientCreate, Permission.PatientEdit,
            Permission.AppointmentView, Permission.AppointmentCreate,
            Permission.AppointmentManage, Permission.AppointmentCheckIn,
            Permission.AdmissionView, Permission.ReportView
        ],
        [Role.LabTechnician] =
        [
            Permission.PatientView, Permission.LabCollect, Permission.LabEnterResult,
            Permission.LabVerify, Permission.LabRelease, Permission.ImagingView,
            Permission.ReportView
        ],
        [Role.Radiologist] =
        [
            Permission.PatientView, Permission.ImagingView, Permission.ImagingManage,
            Permission.ImagingReport, Permission.ImagingVerify, Permission.ImagingRelease,
            Permission.ReportView
        ],
        [Role.Pharmacist] =
        [
            Permission.PatientView, Permission.PrescriptionReview, Permission.PrescriptionDispense,
            Permission.InventoryManage, Permission.ReportView
        ],
        [Role.BillingOfficer] =
        [
            Permission.PatientView, Permission.BillingView, Permission.BillingManage,
            Permission.InsuranceManage, Permission.ReportView, Permission.ReportFinancial,
            Permission.ReportExport
        ],
        [Role.MedicalRecordsOfficer] =
        [
            Permission.PatientView, Permission.PatientCreate, Permission.PatientEdit,
            Permission.EncounterView, Permission.ReportView, Permission.ReportExport
        ],
        [Role.Management] =
        [
            Permission.PatientView, Permission.EncounterView, Permission.AdmissionView,
            Permission.BillingView, Permission.ReportView, Permission.ReportFinancial,
            Permission.ReportExport
        ],
        [Role.Auditor] =
        [
            Permission.AuditView, Permission.ReportView, Permission.EncounterView
        ],
        [Role.Patient] = []
    };

    public static IReadOnlyCollection<string> For(Role role) =>
        Map.TryGetValue(role, out var permissions) ? permissions : [];

    public static IReadOnlyCollection<string> ForName(string? roleName) =>
        Enum.TryParse<Role>(roleName, ignoreCase: true, out var role) ? For(role) : [];

    /// <summary>True when the role's grant set contains at least one write capability.</summary>
    public static bool IsClinician(Role role) =>
        For(role).Contains(Permission.EncounterDocument);
}
