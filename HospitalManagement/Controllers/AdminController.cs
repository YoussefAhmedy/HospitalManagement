using System.Security.Claims;
using AutoMapper;
using Hospital.BLL.Helpers;
using Hospital.BLL.ModelVM;
using Hospital.BLL.Notifications;
using Hospital.DAL.Entities;
using HospitalManagement.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using System.Text;
using Hospital.BLL.Services.Abstraction;
using Microsoft.AspNetCore.Authorization;

namespace HospitalManagement.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly IMapper mapper;
        private readonly IEmailQueue emailQueue;
        private readonly IAuditLogger auditLogger;
        private readonly UserManager<ApplicationUser> userManager;
        private readonly ILogger<AdminController> logger;
        private readonly ISepcializationService sepcializationService;
        private readonly IDoctorService doctorService;
        private readonly IPatientService patientService;
        private readonly ImedicalRecordService medicalRecordService;
        private readonly IShiftService shiftService;
        private readonly IScheduleService scheduleService;
        private readonly IAppointmentService appointmentService;

        public AdminController(IMapper mapper, IEmailQueue emailQueue, IAuditLogger auditLogger,
            UserManager<ApplicationUser> userManager,
            ILogger<AdminController> logger, ISepcializationService sepcializationService,
            IDoctorService doctorService, IPatientService patientService,
            ImedicalRecordService medicalRecordService, IShiftService shiftService,
            IScheduleService scheduleService, IAppointmentService appointmentService)
        {
            this.mapper = mapper;
            this.emailQueue = emailQueue;
            this.auditLogger = auditLogger;
            this.userManager = userManager;
            this.logger = logger;
            this.sepcializationService = sepcializationService;
            this.doctorService = doctorService;
            this.patientService = patientService;
            this.medicalRecordService = medicalRecordService;
            this.shiftService = shiftService;
            this.scheduleService = scheduleService;
            this.appointmentService = appointmentService;
        }
        public IActionResult Index()
        {
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);
            var model = new AdminIndexVm
            {
                NoOfPatients = patientService.GetAllPatients().Count(),
                NoOfAppoint = appointmentService.GetAppointments(_ => true).Count(),
                NoOfNewAppoint = appointmentService.GetAppointments(appointment =>
                    appointment.AppointmentDate >= tomorrow &&
                    appointment.Status != Hospital.DAL.Entities.OwnedTypes.AppointStatus.Cancelled &&
                    appointment.Status != Hospital.DAL.Entities.OwnedTypes.AppointStatus.NotApproved).Count(),
                NoOfTodayPatient = appointmentService.GetAppointments(appointment =>
                    appointment.AppointmentDate >= today && appointment.AppointmentDate < tomorrow &&
                    appointment.Status == Hospital.DAL.Entities.OwnedTypes.AppointStatus.Approved).Count(),
                NoOfDoctors = doctorService.GetAllDoctors().Count(),
                NoOfMedical = medicalRecordService.GetMedicalRecordsWithPatientAndDoctor().Count()
            };
            return View(model);
        }

        public IActionResult CreateDoctor()
        {
            CreateDoctorViewModel createDoctorViewModel = new CreateDoctorViewModel()
            {
                Specializations = sepcializationService.GetSpecializations()
            };

            return View(createDoctorViewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateDoctor(CreateDoctorViewModel model)
        {
            model.Specializations = sepcializationService.GetSpecializations();
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var doctor = mapper.Map<Doctor>(model);
            doctor.Email = model.Email.Trim();
            doctor.UserName = doctor.Email;
            doctor.Specialization = await sepcializationService.GetSpecialization(model.SpecializationId);
            doctor.CreatedAt = DateTime.UtcNow;

            var createResult = await userManager.CreateAsync(doctor, model.Password);
            if (!createResult.Succeeded)
            {
                foreach (var error in createResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
                return View(model);
            }

            var roleResult = await userManager.AddToRoleAsync(doctor, Role.Doctor.ToString());
            if (!roleResult.Succeeded)
            {
                await userManager.DeleteAsync(doctor);
                logger.LogError("Doctor role assignment failed after account creation.");
                ModelState.AddModelError(string.Empty, "We could not complete the account setup. Please try again.");
                return View(model);
            }

            var token = await userManager.GenerateEmailConfirmationTokenAsync(doctor);
            var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
            var confirmationUrl = Url.Action(
                nameof(AccountController.ConfirmEmail), "Account",
                new { id = doctor.Id, code = encodedToken }, Request.Scheme);
            var emailQueued = confirmationUrl is not null && emailQueue.TryEnqueue(
                doctor.Email!, "Confirm your CareAxis clinician account",
                EmailTemplates.AccountConfirmation(doctor.FirstName, confirmationUrl));

            await auditLogger.RecordAsync("admin.doctor-created", User.FindFirstValue(ClaimTypes.NameIdentifier),
                doctor.Id, "user", doctor.Id);
            logger.LogInformation("A clinician account was created by an administrator.");
            ViewBag.Success = emailQueued
                ? "Clinician account created. The clinician must verify their email before signing in."
                : "Clinician account created, but verification email delivery is not configured. No password was emailed.";

            return View(new CreateDoctorViewModel
            {
                Specializations = sepcializationService.GetSpecializations()
            });
        }

        public IActionResult ListDoctors()
        {
            var result = doctorService.GetDoctorVms();

            return View(result);
        }

        public async Task<IActionResult> getDoctorDetails(string id)
        {
            
            var doctor = await doctorService.DoctorByIdAsync(id);
            if (doctor == null)
                return RedirectToAction("ListDoctors");
            var result = mapper.Map<DoctorVm>(doctor);
            result.specializations = sepcializationService.GetSpecializations();
            return View(result);
        }
        [HttpDelete]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteDoctorAjax(string id)
        {
            var user = await userManager.FindByIdAsync(id);
            if (user is not Doctor doctor || !await userManager.IsInRoleAsync(doctor, Role.Doctor.ToString()))
            {
                return NotFound();
            }

            var result = await userManager.DeleteAsync(doctor);
            if (!result.Succeeded)
            {
                return BadRequest("The clinician account could not be deleted.");
            }

            await auditLogger.RecordAsync("admin.doctor-deleted",
                User.FindFirstValue(ClaimTypes.NameIdentifier), doctor.Id, "user", doctor.Id);
            logger.LogInformation("An administrator deleted a clinician account.");
            return Ok(new { redirect = "/Admin/ListDoctors" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditDoctor(string id, DoctorVm doctorVm)
        {
            doctorVm.specializations = sepcializationService.GetSpecializations();
            if (string.IsNullOrWhiteSpace(id))
            {
                return BadRequest();
            }

            if (ModelState.IsValid)
            {
                var doctor = await doctorService.DoctorByIdAsync(id);
                if (doctor is null)
                {
                    return NotFound();
                }

                doctor.FirstName = doctorVm.FirstName.Trim();
                doctor.LastName = doctorVm.LastName.Trim();
                doctor.Salary = doctorVm.Salary;
                doctor.Specialization = await sepcializationService.GetSpecialization(doctorVm.SpecializationId);

                var result = await userManager.UpdateAsync(doctor);
                if (result.Succeeded)
                {
                    await auditLogger.RecordAsync("admin.doctor-updated",
                        User.FindFirstValue(ClaimTypes.NameIdentifier), doctor.Id, "user", doctor.Id);
                    return RedirectToAction(nameof(getDoctorDetails), new { id });
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            doctorVm.Id = id;
            return View("getDoctorDetails", doctorVm);
        }

        public IActionResult GetPatients()
        {
            var patients = patientService.GetAllPatients().Select( p=> new PatientVm()
            {
                FirstName = p.FirstName,
                LastName = p.LastName,
                Email = p.Email,
                Image = p.Image,
                Phone = p.PhoneNumber,
                Id= p.Id
            }
            ).ToList();
            
            return View(patients);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditPatient(EditPatientVm vm)
        {
            if (ModelState.IsValid)
            {
                var patient = await patientService.GetPatientById(vm.Id);
                if (patient != null)
                {
                    patient.FirstName = vm.FirstName;
                    patient.LastName = vm.LastName;
                    patient.Email = vm.Email;
                    patient.PhoneNumber = vm.PhoneNumber;
                    patient.Gender = vm.Gender;
                    patient.DateOfBirth = vm.DateOfBirth;
                    var result = await userManager.UpdateAsync(patient);
                    if (result.Succeeded)
                    {
                        await auditLogger.RecordAsync("admin.patient-updated",
                            User.FindFirstValue(ClaimTypes.NameIdentifier), patient.Id, "user", patient.Id);
                        return RedirectToAction(nameof(GetPatients));
                    }
                    foreach (var error in result.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }
                }
                else
                    ModelState.AddModelError(string.Empty, "this patient does not exist");

            }
            return View(vm);

        }

       
        public async Task<IActionResult> GetPatientDetails(string id)
        {
            var patient = await patientService.GetPatientById(id);
            if (patient == null)
                return RedirectToAction("GetPatients");
            var patientVm = mapper.Map<EditPatientVm>(patient); 
            return View(patientVm);
        }

        [HttpDelete]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeletePatient(string email)
        {
            var patient = await patientService.GetPatient(patient => patient.Email == email);
            if (patient is null)
            {
                return NotFound();
            }

            var result = await patientService.Delete(patient);
            if (!result)
            {
                return BadRequest("The patient account could not be deleted.");
            }

            await auditLogger.RecordAsync("admin.patient-deleted",
                User.FindFirstValue(ClaimTypes.NameIdentifier), patient.Id, "user", patient.Id);
            logger.LogInformation("An administrator deleted a patient account.");
            return Ok(new { redirect = "/Admin/GetPatients" });
        }

        public IActionResult GetMedicalRecords()
        {
            var model = medicalRecordService.GetMedicalRecordsWithPatientAndDoctor().Select(med => new MedicalRecordVm
            {
                MedicalRecordID = med.MedicalRecordID,
                Diagnosis = med.Diagnosis,
                Treatment = med.Treatment,
                RecordDate = med.RecordDate,
                PatientName = $"{med.Patient?.FirstName} {med.Patient?.LastName}".Trim(),
                DoctorName = $"{med.Doctor?.FirstName} {med.Doctor?.LastName}".Trim(),
            }).ToList();
            return View(model);
        }

        public  IActionResult AddMedicalRecord()
        {
            var CreateMedicalRecord = new AddmedicalRecordVM()
            {
                patients = patientService.GetAllPatients(),
                Doctors = doctorService.GetAllDoctors()

            };

            return View(CreateMedicalRecord);
        }

        [HttpPost]
        public async Task<IActionResult> AddMedicalRecord(AddmedicalRecordVM addmedicalRecordVM)
        {
            addmedicalRecordVM.patients = patientService.GetAllPatients();
            addmedicalRecordVM.Doctors = doctorService.GetAllDoctors();
            if (ModelState.IsValid)
            {
                var medicalRecord = mapper.Map<MedicalRecord>(addmedicalRecordVM);
                if (await medicalRecordService.AddMedicalRecord(medicalRecord))
                {
                    await auditLogger.RecordAsync("admin.medical-record-created",
                        User.FindFirstValue(ClaimTypes.NameIdentifier), medicalRecord.PatientID,
                        "medical-record", medicalRecord.MedicalRecordID.ToString());
                    return RedirectToAction(nameof(GetMedicalRecords));
                }
                ModelState.AddModelError(string.Empty, "The record could not be saved. Please try again.");
            }

            addmedicalRecordVM.patients = patientService.GetAllPatients();
            addmedicalRecordVM.Doctors = doctorService.GetAllDoctors();
            return View("AddMedicalRecord",addmedicalRecordVM);
        }


        public IActionResult CreateSchedule()
        {
            var CreateSchedule = new CreateScheduleVM()
            {
                Doctors = doctorService.GetAllDoctors(),
                Shifts = shiftService.GetShifts().ToList(),
            };
            return View(CreateSchedule);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateSchedule(CreateScheduleVM createScheduleVM)
        {
            createScheduleVM.Doctors = doctorService.GetAllDoctors();
            createScheduleVM.Shifts = shiftService.GetShifts().ToList();
            if (ModelState.IsValid)
            {
                var docSchedules = await doctorService.GetDoctorAndSchedulesById(createScheduleVM.DoctorId);
                if(docSchedules != null)
                {
                    if (docSchedules.Schedules != null && docSchedules.Schedules.Any(sec =>
                    sec.DoctorId == createScheduleVM.DoctorId
                    && sec.Day == createScheduleVM.Day
                    && sec.ShiftId == createScheduleVM.ShiftId
                    && sec.Status == createScheduleVM.Status))
                    {
                        ModelState.AddModelError(string.Empty, "This schedule added to this doc already");
                        return View(createScheduleVM);
                    }
                        
                    var schedule = mapper.Map<Schedule>(createScheduleVM);
                    if(await scheduleService.AddSchedule(schedule))
                    {
                        ViewBag.Success = "Added!!!";
                        var CreateSchedule = new CreateScheduleVM()
                        {
                            Doctors = doctorService.GetAllDoctors(),
                            Shifts = shiftService.GetShifts().ToList(),
                        };
                        return View(CreateSchedule);
                    }
                    ModelState.AddModelError(string.Empty, "can't add this schedule");
                }
                ModelState.AddModelError(string.Empty, "doctor not found");

            }
            return View(createScheduleVM);
        }
        [HttpGet]
        public IActionResult CreateShift() => View(new CreateShiftVm());
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateShift(CreateShiftVm model)
        {
            if (ModelState.IsValid)
            {
                if(shiftService.GetShifts().Any(s => s.ShiftType == model.ShiftType && s.EndTIme > model.StartTime))
                {
                    ModelState.AddModelError(string.Empty, "there is a conflict shift");
                    return View(model);
                }
                var shift = mapper.Map<Shift>(model);
                if(await shiftService.AddShift(shift))
                {
                    return RedirectToAction("Index");
                }
                ModelState.AddModelError(string.Empty, "can' add this Shift");
            }
            return View(model);
        }
    }
}
