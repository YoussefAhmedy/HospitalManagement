using Hospital.BLL.Helpers;
using Hospital.DAL.DataBase;
using Hospital.DAL.Entities;
using Hospital.DAL.Entities.Billing;
using Hospital.DAL.Entities.Clinical;
using Hospital.DAL.Entities.Diagnostics;
using Hospital.DAL.Entities.OwnedTypes;
using Hospital.DAL.Entities.Operations;
using Hospital.DAL.Entities.Organization;
using Hospital.DAL.Entities.Pharmacy;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Hospital.BLL.Services.Seeding;

/// <summary>
/// Idempotent synthetic dataset for demonstration and evaluation. Accounts use synthetic
/// addresses and placeholder credentials that are printed once to the operator; no value
/// here is a secret and every patient row is flagged <c>IsSynthetic</c>.
/// </summary>
public sealed class DemoDataSeeder(
    HospitalDbContext dbContext,
    UserManager<ApplicationUser> userManager) : IDemoDataSeeder
{
    public const string DemoPassword = "Careaxis-Demo-2026!";

    public async Task<string> SeedAsync(CancellationToken cancellationToken = default)
    {
        if (await dbContext.Branches.AnyAsync(cancellationToken))
        {
            return "Demo data already present; nothing to do.";
        }

        var branch = new Branch { Name = "CareAxis Central", Code = "CAX-01", City = "Alexandria", Phone = "+20 3 000 0000" };
        dbContext.Branches.Add(branch);

        var departments = new List<Department>
        {
            new() { Name = "Internal Medicine", Code = "IM", Branch = branch },
            new() { Name = "Cardiology", Code = "CARD", Branch = branch },
            new() { Name = "Paediatrics", Code = "PAED", Branch = branch },
            new() { Name = "Laboratory", Code = "LAB", Branch = branch },
            new() { Name = "Radiology", Code = "RAD", Branch = branch },
            new() { Name = "Pharmacy", Code = "PHM", Branch = branch }
        };
        dbContext.Departments.AddRange(departments);

        var specializations = new List<Specialization>
        {
            new() { Name = "Internal Medicine" },
            new() { Name = "Cardiology" },
            new() { Name = "Paediatrics" }
        };
        dbContext.Specializations.AddRange(specializations);

        var shifts = new List<Shift>
        {
            new() { ShiftType = ShiftType.Morning, StartTIme = new TimeSpan(9, 0, 0), EndTIme = new TimeSpan(13, 0, 0) },
            new() { ShiftType = ShiftType.Afternoon, StartTIme = new TimeSpan(14, 0, 0), EndTIme = new TimeSpan(18, 0, 0) }
        };
        dbContext.Shifts.AddRange(shifts);

        var labTests = new List<LabTest>
        {
            new() { Code = "CBC", Name = "Complete blood count", Category = "Haematology", Unit = "10^9/L", NormalLow = 4, NormalHigh = 11, Price = 25 },
            new() { Code = "HGB", Name = "Haemoglobin", Category = "Haematology", Unit = "g/dL", NormalLow = 12, NormalHigh = 17, CriticalLow = 7, Price = 15 },
            new() { Code = "GLU", Name = "Fasting glucose", Category = "Chemistry", Unit = "mg/dL", NormalLow = 70, NormalHigh = 100, CriticalHigh = 400, Price = 18 },
            new() { Code = "K", Name = "Potassium", Category = "Chemistry", Unit = "mmol/L", NormalLow = 3.5m, NormalHigh = 5.0m, CriticalLow = 2.5m, CriticalHigh = 6.5m, Price = 20 },
            new() { Code = "NA", Name = "Sodium", Category = "Chemistry", Unit = "mmol/L", NormalLow = 135, NormalHigh = 145, Price = 20 },
            new() { Code = "CRP", Name = "C-reactive protein", Category = "Immunology", Unit = "mg/L", NormalHigh = 5, Price = 30 },
            new() { Code = "TSH", Name = "Thyroid stimulating hormone", Category = "Endocrinology", Unit = "mIU/L", NormalLow = 0.4m, NormalHigh = 4.0m, Price = 35 },
            new() { Code = "URIN", Name = "Urinalysis", Category = "Microbiology", Specimen = SpecimenType.Urine, Unit = "dipstick", Price = 12 }
        };
        dbContext.LabTests.AddRange(labTests);

        var medications = new List<Medication>
        {
            new() { Name = "Amoxicillin", GenericName = "Amoxicillin", Form = "capsule", Strength = "500 mg", Unit = "capsule", UnitPrice = 0.8m, ReorderLevel = 100 },
            new() { Name = "Paracetamol", GenericName = "Acetaminophen", Form = "tablet", Strength = "500 mg", Unit = "tablet", UnitPrice = 0.15m, ReorderLevel = 200 },
            new() { Name = "Metformin", GenericName = "Metformin", Form = "tablet", Strength = "850 mg", Unit = "tablet", UnitPrice = 0.3m, ReorderLevel = 100 },
            new() { Name = "Atorvastatin", GenericName = "Atorvastatin", Form = "tablet", Strength = "20 mg", Unit = "tablet", UnitPrice = 0.6m, ReorderLevel = 80 },
            new() { Name = "Salbutamol", GenericName = "Salbutamol", Form = "inhaler", Strength = "100 mcg", Unit = "inhaler", UnitPrice = 4.5m, ReorderLevel = 20 },
            new() { Name = "Omeprazole", GenericName = "Omeprazole", Form = "capsule", Strength = "20 mg", Unit = "capsule", UnitPrice = 0.35m, ReorderLevel = 120 }
        };
        dbContext.Medications.AddRange(medications);

        var serviceItems = new List<ServiceItem>
        {
            new() { Code = "CONS-IM", Name = "Internal medicine consultation", Category = ServiceCategory.Consultation, Price = 60, Department = departments[0] },
            new() { Code = "CONS-CARD", Name = "Cardiology consultation", Category = ServiceCategory.Consultation, Price = 90, Department = departments[1] },
            new() { Code = "LAB-CBC", Name = "Complete blood count", Category = ServiceCategory.Laboratory, Price = 25, Department = departments[3] },
            new() { Code = "RAD-CXR", Name = "Chest X-ray", Category = ServiceCategory.Radiology, Price = 70, Department = departments[4] },
            new() { Code = "ROOM-STD", Name = "Standard room (per day)", Category = ServiceCategory.Room, Price = 150, Department = departments[0] }
        };
        dbContext.ServiceItems.AddRange(serviceItems);

        var ward = new Ward { Name = "Inpatient Ward A", Code = "WA", Department = departments[0] };
        var room1 = new Room { Ward = ward, Number = "101", Class = "Standard", DailyRate = 150 };
        var room2 = new Room { Ward = ward, Number = "102", Class = "Standard", DailyRate = 150 };
        room1.Beds.AddRange([new Bed { Label = "101-A" }, new Bed { Label = "101-B" }]);
        room2.Beds.AddRange([new Bed { Label = "102-A" }, new Bed { Label = "102-B" }]);
        ward.Rooms.AddRange([room1, room2]);
        dbContext.Wards.Add(ward);

        await dbContext.SaveChangesAsync(cancellationToken);

        // ---------------------------------------------------------------- users
        var doctors = new List<Doctor>();
        var doctorSeeds = new[]
        {
            ("sara.hassan", "Sara", "Hassan", 0, departments[0]),
            ("omar.fahmy", "Omar", "Fahmy", 1, departments[1]),
            ("mona.adel", "Mona", "Adel", 2, departments[2])
        };

        foreach (var (handle, first, last, specIndex, department) in doctorSeeds)
        {
            var doctor = new Doctor
            {
                UserName = $"{handle}@example.invalid",
                Email = $"{handle}@example.invalid",
                EmailConfirmed = true,
                FirstName = first,
                LastName = last,
                Gender = specIndex == 2 ? "Female" : "Female",
                EmployeeNumber = $"DR-{specIndex + 1:000}",
                Salary = 18000,
                SessionPrice = 60,
                SlotMinutes = 20,
                Specialization = specializations[specIndex],
                Department = department,
                ClinicRoom = $"C-{200 + specIndex}"
            };

            var created = await userManager.CreateAsync(doctor, DemoPassword);
            if (!created.Succeeded)
            {
                throw new InvalidOperationException($"Demo clinician creation failed: {string.Join(", ", created.Errors.Select(e => e.Code))}");
            }

            await userManager.AddToRoleAsync(doctor, Role.Doctor.ToString());
            doctors.Add(doctor);

            foreach (var day in new[] { DayOfWeek.Sunday, DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday })
            {
                dbContext.Schedules.Add(new Schedule
                {
                    DoctorId = doctor.Id,
                    ShiftId = shifts[0].ShiftId,
                    Day = day,
                    Status = DAL.Entities.OwnedTypes.Status.Assigned
                });
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var staffSeeds = new (string Handle, string First, string Last, Role Role)[]
        {
            ("admin", "Amina", "Farouk", Role.HospitalAdmin),
            ("reception", "Nour", "Samir", Role.Receptionist),
            ("nurse", "Hala", "Ibrahim", Role.Nurse),
            ("lab", "Youssef", "Kamel", Role.LabTechnician),
            ("pharmacy", "Rania", "Mostafa", Role.Pharmacist),
            ("billing", "Tarek", "Aly", Role.BillingOfficer),
            ("records", "Dina", "Rashad", Role.MedicalRecordsOfficer),
            ("management", "Hassan", "Zaki", Role.Management),
            ("auditor", "Laila", "Nabil", Role.Auditor)
        };

        var staff = new List<(ApplicationUser User, Role Role)>();
        foreach (var seed in staffSeeds)
        {
            var user = new ApplicationUser
            {
                UserName = $"{seed.Handle}@example.invalid",
                Email = $"{seed.Handle}@example.invalid",
                EmailConfirmed = true,
                FirstName = seed.First,
                LastName = seed.Last,
                Gender = "Female"
            };

            var created = await userManager.CreateAsync(user, DemoPassword);
            if (!created.Succeeded)
            {
                throw new InvalidOperationException($"Demo staff creation failed: {string.Join(", ", created.Errors.Select(e => e.Code))}");
            }

            await userManager.AddToRoleAsync(user, seed.Role.ToString());
            staff.Add((user, seed.Role));
        }

        // ---------------------------------------------------------------- patients
        var patientSeeds = new (string Handle, string First, string Last, string Sex, int Year, string Blood)[]
        {
            ("patient", "Layla", "Mahmoud", "Female", 1988, "A+"),
            ("ayman.saleh", "Ayman", "Saleh", "Male", 1972, "O-"),
            ("heba.fouad", "Heba", "Fouad", "Female", 1995, "B+"),
            ("kareem.taha", "Kareem", "Taha", "Male", 2014, "A-"),
            ("nadia.hussein", "Nadia", "Hussein", "Female", 1960, "AB+"),
            ("ziad.adel", "Ziad", "Adel", "Male", 1990, "O+")
        };

        var patients = new List<Patient>();
        var index = 1;
        foreach (var seed in patientSeeds)
        {
            var patient = new Patient
            {
                UserName = $"{seed.Handle}@example.invalid",
                Email = $"{seed.Handle}@example.invalid",
                EmailConfirmed = true,
                FirstName = seed.First,
                LastName = seed.Last,
                Gender = seed.Sex,
                DateOfBirth = new DateTime(seed.Year, 4, 12, 0, 0, 0, DateTimeKind.Utc),
                PhoneNumber = $"+20 100 000 {index:0000}",
                Address = "Synthetic address, Alexandria",
                EmergencyContact = "Synthetic contact +20 100 000 9999",
                BloodGroup = seed.Blood,
                Mrn = $"PT-{index:000000}",
                IsSynthetic = true,
                MedicalHistory = index == 2 ? "Hypertension diagnosed 2019; Type 2 diabetes." : null
            };

            var created = await userManager.CreateAsync(patient, DemoPassword);
            if (!created.Succeeded)
            {
                throw new InvalidOperationException($"Demo patient creation failed: {string.Join(", ", created.Errors.Select(e => e.Code))}");
            }

            await userManager.AddToRoleAsync(patient, Role.Patient.ToString());
            patients.Add(patient);
            index++;
        }

        dbContext.Allergies.AddRange(
            new Allergy { Patient = patients[1], Substance = "Penicillin", Reaction = "Urticaria", Severity = AllergySeverity.Severe },
            new Allergy { Patient = patients[4], Substance = "Aspirin", Reaction = "Bronchospasm", Severity = AllergySeverity.Severe },
            new Allergy { Patient = patients[2], Substance = "Dust mites", Reaction = "Rhinitis", Severity = AllergySeverity.Mild });

        dbContext.StockBatches.AddRange(
            new StockBatch { Medication = medications[0], BatchNumber = "AMX-2026-01", ExpiryDate = DateTime.UtcNow.Date.AddDays(120), QuantityOnHand = 400, Supplier = "Synthetic wholesaler" },
            new StockBatch { Medication = medications[1], BatchNumber = "PCM-2026-01", ExpiryDate = DateTime.UtcNow.Date.AddDays(400), QuantityOnHand = 1200, Supplier = "Synthetic wholesaler" },
            new StockBatch { Medication = medications[2], BatchNumber = "MET-2026-01", ExpiryDate = DateTime.UtcNow.Date.AddDays(60), QuantityOnHand = 90, Supplier = "Synthetic wholesaler" },
            new StockBatch { Medication = medications[3], BatchNumber = "ATV-2026-01", ExpiryDate = DateTime.UtcNow.Date.AddDays(300), QuantityOnHand = 150, Supplier = "Synthetic wholesaler" },
            new StockBatch { Medication = medications[4], BatchNumber = "SAL-2026-01", ExpiryDate = DateTime.UtcNow.Date.AddDays(45), QuantityOnHand = 12, Supplier = "Synthetic wholesaler" },
            new StockBatch { Medication = medications[5], BatchNumber = "OMP-2026-01", ExpiryDate = DateTime.UtcNow.Date.AddDays(500), QuantityOnHand = 300, Supplier = "Synthetic wholesaler" });

        dbContext.InsurancePolicies.Add(new InsurancePolicy
        {
            Patient = patients[0],
            ProviderName = "Synthetic Health Assurance",
            PolicyNumber = "SYN-0001",
            CoveragePercent = 80,
            MemberNumber = "M-0001",
            IsActive = true
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        // ---------------------------------------------------------------- activity
        var today = DateTime.UtcNow.Date;
        var random = new Random(20261122);
        var encounterCount = 0;

        for (var dayOffset = 13; dayOffset >= 0; dayOffset--)
        {
            var day = today.AddDays(-dayOffset);
            if (day.DayOfWeek is DayOfWeek.Friday or DayOfWeek.Saturday)
            {
                continue;
            }

            var visits = dayOffset == 0 ? 4 : random.Next(3, 6);
            for (var visit = 0; visit < visits; visit++)
            {
                var patient = patients[random.Next(patients.Count)];
                var doctor = doctors[random.Next(doctors.Count)];
                var slot = 540 + visit * 40;
                if (slot >= 780)
                {
                    break;
                }

                encounterCount++;
                var isToday = dayOffset == 0;
                var status = isToday && visit > 1
                    ? EncounterStatus.InConsultation
                    : isToday ? EncounterStatus.Triaged : EncounterStatus.Signed;

                var appointment = new Appointment
                {
                    Number = $"APT-{encounterCount:000000}",
                    AppointmentDate = day,
                    SlotMinutes = slot,
                    DurationMinutes = doctor.SlotMinutes,
                    Status = dayOffset switch
                    {
                        0 when visit == 0 => AppointStatus.Completed,
                        0 when visit == 1 => AppointStatus.CheckedIn,
                        0 => AppointStatus.Approved,
                        _ => status == EncounterStatus.Signed ? AppointStatus.Completed : AppointStatus.Approved
                    },
                    Patient = patient,
                    Doctor = doctor,
                    Notes = "Synthetic demonstration visit",
                    CreatedAtUtc = day.AddDays(-2)
                };
                dbContext.Appointments.Add(appointment);

                var encounter = new Encounter
                {
                    Number = $"ENC-{encounterCount:000000}",
                    Patient = patient,
                    Doctor = doctor,
                    Department = doctor.Department,
                    Type = EncounterType.Outpatient,
                    Status = status,
                    ChiefComplaint = visit % 3 == 0 ? "Chest discomfort on exertion" : visit % 3 == 1 ? "Persistent dry cough" : "Follow-up review",
                    RegisteredAtUtc = day.AddHours(9).AddMinutes(visit * 40),
                    TriagedAtUtc = day.AddHours(9).AddMinutes(visit * 40 + 5),
                    StartedAtUtc = day.AddHours(9).AddMinutes(visit * 40 + 10),
                    CompletedAtUtc = status is EncounterStatus.Completed or EncounterStatus.Signed ? day.AddHours(9).AddMinutes(visit * 40 + 35) : null,
                    SignedAtUtc = status == EncounterStatus.Signed ? day.AddHours(10).AddMinutes(visit * 40 + 30) : null,
                    SignedByUserId = status == EncounterStatus.Signed ? doctor.Id : null
                };

                encounter.Vitals.Add(new VitalSign
                {
                    RecordedAtUtc = encounter.RegisteredAtUtc.AddMinutes(6),
                    RecordedByUserId = staff[2].User.Id,
                    TemperatureC = 36.4m + (decimal)random.NextDouble() * 1.6m,
                    SystolicBp = 110 + random.Next(0, 40),
                    DiastolicBp = 70 + random.Next(0, 20),
                    PulseBpm = 62 + random.Next(0, 35),
                    RespiratoryRate = 14 + random.Next(0, 6),
                    OxygenSaturation = 95 + (decimal)random.NextDouble() * 4,
                    HeightCm = 160 + random.Next(0, 25),
                    WeightKg = 55 + random.Next(0, 45),
                    PainScore = random.Next(0, 5)
                });

                encounter.Diagnoses.Add(new Diagnosis
                {
                    Code = visit % 3 == 0 ? "I20.9" : visit % 3 == 1 ? "J06.9" : "Z00.0",
                    Description = visit % 3 == 0 ? "Angina pectoris, unspecified" : visit % 3 == 1 ? "Acute upper respiratory infection" : "General medical examination",
                    Type = DiagnosisType.Primary,
                    RecordedByUserId = doctor.Id,
                    RecordedAtUtc = encounter.RegisteredAtUtc.AddMinutes(25)
                });

                encounter.Notes.Add(new ClinicalNote
                {
                    Content = "Synthetic consultation note. Patient reviewed; vitals stable; no acute distress. Plan discussed with patient and safety advice given.",
                    AuthorUserId = doctor.Id,
                    AuthorRole = Role.Doctor.ToString(),
                    Status = status == EncounterStatus.Signed ? NoteStatus.Signed : NoteStatus.Draft,
                    CreatedAtUtc = encounter.RegisteredAtUtc.AddMinutes(30),
                    SignedAtUtc = status == EncounterStatus.Signed ? day.AddHours(10).AddMinutes(visit * 40 + 30) : null,
                    SignedByUserId = status == EncounterStatus.Signed ? doctor.Id : null
                });

                dbContext.Encounters.Add(encounter);

                if (status is EncounterStatus.Signed or EncounterStatus.Completed)
                {
                    var test = labTests[random.Next(labTests.Count)];
                    var orderStatus = dayOffset > 3 ? LabOrderStatus.Released : LabOrderStatus.Processing;
                    var numeric = test.NormalLow.HasValue && test.NormalHigh.HasValue
                        ? Math.Round((test.NormalLow.Value + test.NormalHigh.Value) / 2, 1)
                        : (decimal?)null;

                    dbContext.LabOrders.Add(new LabOrder
                    {
                        Number = $"LAB-{encounterCount:000000}",
                        Patient = patient,
                        Encounter = encounter,
                        OrderedByUserId = doctor.Id,
                        ClinicalNotes = "Synthetic laboratory order",
                        Status = orderStatus,
                        SpecimenId = $"SPC-{encounterCount:000000}",
                        CollectedAtUtc = encounter.RegisteredAtUtc.AddMinutes(15),
                        ReceivedAtUtc = encounter.RegisteredAtUtc.AddMinutes(25),
                        ReleasedAtUtc = orderStatus == LabOrderStatus.Released ? encounter.RegisteredAtUtc.AddHours(3) : null,
                        ReleasedByUserId = orderStatus == LabOrderStatus.Released ? staff[3].User.Id : null,
                        CreatedAtUtc = encounter.RegisteredAtUtc.AddMinutes(12),
                        Items =
                        [
                            new LabOrderItem
                            {
                                LabTest = test,
                                ResultValue = numeric?.ToString("0.0") ?? "Negative",
                                NumericResult = numeric,
                                ResultPrice = test.Price,
                                Flag = DAL.Entities.Diagnostics.ResultFlag.Normal,
                                EnteredByUserId = staff[3].User.Id,
                                EnteredAtUtc = encounter.RegisteredAtUtc.AddHours(2),
                                VerifiedByUserId = orderStatus == LabOrderStatus.Released ? staff[3].User.Id : null,
                                VerifiedAtUtc = orderStatus == LabOrderStatus.Released ? encounter.RegisteredAtUtc.AddHours(3) : null
                            }
                        ]
                    });

                    if (visit == 0 && dayOffset < 10)
                    {
                        dbContext.Prescriptions.Add(new Prescription
                        {
                            Number = $"RX-{encounterCount:000000}",
                            Patient = patient,
                            Encounter = encounter,
                            PrescribedByUserId = doctor.Id,
                            Status = dayOffset > 5 ? PrescriptionStatus.Dispensed : PrescriptionStatus.Reviewed,
                            CreatedAtUtc = encounter.RegisteredAtUtc.AddMinutes(32),
                            ReviewedByUserId = staff[4].User.Id,
                            ReviewedAtUtc = encounter.RegisteredAtUtc.AddMinutes(50),
                            Items =
                            [
                                new PrescriptionItem
                                {
                                    Medication = medications[random.Next(medications.Count)],
                                    Dose = "1 tablet",
                                    Frequency = "Twice daily",
                                    Duration = "5 days",
                                    Route = "Oral",
                                    Quantity = 10,
                                    DispensedQuantity = dayOffset > 5 ? 10 : 0
                                }
                            ]
                        });
                    }

                    dbContext.Invoices.Add(new Invoice
                    {
                        Number = $"INV-{encounterCount:000000}",
                        Patient = patient,
                        Encounter = encounter,
                        Status = InvoiceStatus.Issued,
                        IssuedAtUtc = encounter.RegisteredAtUtc.AddMinutes(40),
                        InsuranceCoveredAmount = patient == patients[0] ? 48 : 0,
                        Lines =
                        [
                            new InvoiceLine { Description = "Consultation", Quantity = 1, UnitPrice = 60, ServiceItem = serviceItems[0] },
                            new InvoiceLine { Description = test.Name, Quantity = 1, UnitPrice = test.Price, ServiceItem = serviceItems[2] }
                        ]
                    });
                }
            }
        }

        var admissionPatient = patients[3];
        var admission = new Admission
        {
            Number = "ADM-000001",
            Patient = admissionPatient,
            AdmittingDoctor = doctors[2],
            Status = AdmissionStatus.Admitted,
            Reason = "Synthetic observation admission for dehydration",
            RequestedAtUtc = DateTime.UtcNow.AddDays(-1),
            AdmittedAtUtc = DateTime.UtcNow.AddDays(-1).AddHours(3)
        };
        admission.BedAssignments.Add(new BedAssignment { Bed = room1.Beds[0], AssignedAtUtc = DateTime.UtcNow.AddDays(-1).AddHours(3) });
        dbContext.Admissions.Add(admission);

        dbContext.Admissions.Add(new Admission
        {
            Number = "ADM-000002",
            Patient = patients[4],
            AdmittingDoctor = doctors[0],
            Status = AdmissionStatus.Requested,
            Reason = "Synthetic chest pain work-up",
            RequestedAtUtc = DateTime.UtcNow.AddHours(-3)
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        return $"Seeded synthetic dataset: {patients.Count} patients, {doctors.Count} clinicians, {encounterCount} encounters. Demonstration password for every demo account: {DemoPassword}";
    }
}
