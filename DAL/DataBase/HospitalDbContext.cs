using Hospital.DAL.Entities;
using Hospital.DAL.Entities.Billing;
using Hospital.DAL.Entities.Clinical;
using Hospital.DAL.Entities.config;
using Hospital.DAL.Entities.Diagnostics;
using Hospital.DAL.Entities.Operations;
using Hospital.DAL.Entities.Organization;
using Hospital.DAL.Entities.Pharmacy;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Hospital.DAL.DataBase;

public class HospitalDbContext(DbContextOptions<HospitalDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    // Existing aggregates
    public DbSet<Doctor> Doctors => Set<Doctor>();
    public DbSet<Admin> Admins => Set<Admin>();
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<Allergy> Allergies => Set<Allergy>();
    public DbSet<MedicalRecord> MedicalRecords => Set<MedicalRecord>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<Schedule> Schedules => Set<Schedule>();
    public DbSet<Shift> Shifts => Set<Shift>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    // Organisation
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Department> Departments => Set<Department>();

    // Clinical
    public DbSet<Encounter> Encounters => Set<Encounter>();
    public DbSet<VitalSign> VitalSigns => Set<VitalSign>();
    public DbSet<Diagnosis> Diagnoses => Set<Diagnosis>();
    public DbSet<ClinicalNote> ClinicalNotes => Set<ClinicalNote>();

    // Diagnostics
    public DbSet<LabTest> LabTests => Set<LabTest>();
    public DbSet<LabOrder> LabOrders => Set<LabOrder>();
    public DbSet<LabOrderItem> LabOrderItems => Set<LabOrderItem>();
    public DbSet<ImagingOrder> ImagingOrders => Set<ImagingOrder>();

    // Pharmacy
    public DbSet<Medication> Medications => Set<Medication>();
    public DbSet<StockBatch> StockBatches => Set<StockBatch>();
    public DbSet<Prescription> Prescriptions => Set<Prescription>();
    public DbSet<PrescriptionItem> PrescriptionItems => Set<PrescriptionItem>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();

    // Operations
    public DbSet<Ward> Wards => Set<Ward>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<Bed> Beds => Set<Bed>();
    public DbSet<Admission> Admissions => Set<Admission>();
    public DbSet<BedAssignment> BedAssignments => Set<BedAssignment>();
    public DbSet<NumberSequence> NumberSequences => Set<NumberSequence>();

    // Billing
    public DbSet<ServiceItem> ServiceItems => Set<ServiceItem>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceLine> InvoiceLines => Set<InvoiceLine>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<InsurancePolicy> InsurancePolicies => Set<InsurancePolicy>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(HospitalDbContext).Assembly);
    }
}
