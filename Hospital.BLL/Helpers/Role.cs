namespace Hospital.BLL.Helpers
{
    /// <summary>
    /// Operational roles. The three legacy roles are preserved first so existing role rows
    /// and user assignments remain valid after deployment.
    /// </summary>
    public enum Role
    {
        Admin,
        Patient,
        Doctor,
        SuperAdmin,
        HospitalAdmin,
        Nurse,
        Receptionist,
        LabTechnician,
        Radiologist,
        Pharmacist,
        BillingOfficer,
        MedicalRecordsOfficer,
        Management,
        Auditor
    }
}
