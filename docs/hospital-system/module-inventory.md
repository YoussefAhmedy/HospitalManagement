# CareAxis — Module Inventory

**Legend:** ✅ present and working · ◐ present but insufficient · ✗ absent
Each row states *verified evidence*, the *target state*, and the *decision* for this transformation.

## 1. Identity, access and audit

| Capability | State | Evidence | Target | Decision |
|---|---|---|---|---|
| Registration, email confirmation, lockout, password policy | ✅ | `Program.cs`, `AccountController` | Keep | Preserve unchanged |
| Password reset, resend confirmation, email change | ✅ | `AccountController` | Keep | Preserve |
| Optional Google OAuth | ✅ | `Program.cs` | Keep | Preserve, document |
| Admin bootstrap command, no default admin | ✅ | `Program.BootstrapAdminAsync` | Keep | Preserve |
| Roles | ◐ | `Role` enum: Admin, Patient, Doctor | 12 operational roles | **Extend enum + policy catalogue** → delivered as 14 (11 operational + 3 legacy) |
| Permission model | ✗ | Role-string attributes only | Claim/policy based, least privilege | **Add `Permissions` + policy registration** |
| Audit trail | ✅ | `AuditLog`, `AuditLogger` | Extend to all sensitive actions | Extend event catalogue |
| Session security, CSP, antiforgery, rate limits | ✅ | `Program.cs` | Keep and document | Preserve |

## 2. Patient management

| Capability | State | Evidence | Target | Decision |
|---|---|---|---|---|
| Registration / demographics | ◐ | `Patient` = Address, EmergencyContact | MRN, sex, DOB, phone, blood group, structured address | Extend entity |
| Unique patient identifier (MRN) | ✗ | — | Human-readable MRN `PT-########` | **Add, generated, unique index** |
| Allergies / documented history | ✗ | — | Allergy list + free-text history | **Add** |
| Duplicate detection | ✗ | — | Fuzzy guard on MRN/phone/name+DOB at registration | **Add** |
| Search + pagination | ✗ | loads all rows | Server-side search, filter, paging | **Add** |
| Patient profile workspace | ◐ | basic details page | Unified profile: encounters, labs, prescriptions, invoices, documents | **Rebuild** |
| Uploaded documents | ✗ | — | Document metadata + validated storage | **Add (phase 2)** |

## 3. Appointments and scheduling

| Capability | State | Evidence | Target | Decision |
|---|---|---|---|---|
| Weekly clinician schedule | ✅ | `Schedule` + `Shift` | Interval-based slot generation | Extend |
| Booking | ◐ | date only, `AppointmentDate` | Date + time slot + doctor + service | **Redesign** |
| Conflict prevention | ✗ | impossible: no time | Unique (doctor, slot) + transactional check | **Add** |
| Statuses | ◐ | Approved, NotApproved, Pending, Cancelled | Requested, Confirmed, CheckedIn, InProgress, Completed, Cancelled, NoShow | **Expand enum** |
| Check-in / waiting queue | ✗ | — | Reception check-in, live queue | **Add** |
| Reschedule / no-show | ✗ | — | Supported with audit | **Add** |
| Calendar / availability UI | ✗ | — | Slot picker + day view | **Add** |

## 4. Clinical encounters

| Capability | State | Evidence | Target | Decision |
|---|---|---|---|---|
| Encounter lifecycle | ✗ | `MedicalRecord` only | CheckedIn → Triaged → InConsultation → Completed → Signed | **Add `Encounter`** |
| Vital signs | ✗ | — | structured vitals with ranges | **Add `VitalSign`** |
| Clinical notes + signing | ✗ | free text, unsigned | Draft → Signed, immutable, amendment trail | **Add** |
| Diagnoses | ◐ | 500-char free text | Coded list per encounter | **Add `Diagnosis`** |
| Orders (lab/radiology/rx) | ✗ | — | Linked to encounter | **Add** |

## 5. Laboratory

| Capability | State | Target | Decision |
|---|---|---|---|
| Test catalogue, orders, specimens, results, verification, authorization, release | ✗ | Full 10-state workflow | **Add** (`LabTest`, `LabOrder`, `LabResult`) |
| Reference ranges / abnormal flags | ✗ | Structured + critical alert | **Add** |
| Turnaround metrics | ✗ | From order/result timestamps | **Add to reports** |

## 6. Radiology

| Capability | State | Target | Decision |
|---|---|---|---|
| Imaging request, scheduling, radiologist report, verification | ✗ | Request + report + verify (no PACS/DICOM) | **Add thin workflow**; DICOM/PACS explicitly out of scope |

## 7. Pharmacy

| Capability | State | Target | Decision |
|---|---|---|---|
| Medication catalogue, prescriptions, dispensing, stock, expiry | ✗ | Full dispensing workflow + stock ledger | **Add** (`Medication`, `Prescription`, `Dispense`, `StockMovement`) |

## 8. Admissions, beds and discharge

| Capability | State | Target | Decision |
|---|---|---|---|
| Ward/room/bed, admission, transfer, discharge | ✗ | Occupancy-safe bed assignment | **Add** with unique active-occupancy constraint |

## 9. Billing and insurance

| Capability | State | Target | Decision |
|---|---|---|---|
| Service catalogue, invoices, line items, payments, refunds, insurance | ✗ | Invoice lifecycle + payments + balance | **Add** (no real gateway) |

## 10. Reporting and analytics

| Capability | State | Target | Decision |
|---|---|---|---|
| Operationally meaningful dashboards | ◐ | six counters | Role dashboards with live queries | **Rebuild** |
| Parameterised reports with filters/paging | ✗ | — | Report catalogue + real SQL queries | **Add** |
| CSV export, print-friendly, generation stamp | ✗ | — | Server-side CSV, print CSS | **Add** |
| PDF export | ✗ | — | **Not implemented server-side**: needs a PDF engine dependency that cannot be restored in this environment; print-to-PDF is the documented path | **Documented gap** |
| Patient examination report | ✗ | — | Structured, status-aware, signature block | **Add** |

## 11. Platform and UX

| Capability | State | Target | Decision |
|---|---|---|---|
| Design system | ◐ | 392-line marketing CSS | Tokenised clinical design system | **Rebuild** |
| Application shell (sidebar, topbar, command search) | ✗ | centred marketing nav | Role-aware shell | **Add** |
| Data tables, drawers, dialogs, toasts, empty/error/loading states | ✗ | ad-hoc | Reusable component library | **Add** |
| Arabic / RTL + English | ✗ | English only | Two-culture resource system, RTL stylesheet mirror | **Add** |
| Responsive clinical tables | ✗ | — | Overflow + column priority + detail drawer | **Add** |
| Accessibility | ◐ | skip link, focus ring, 44px targets | WCAG 2.2 AA practices, documented test matrix | **Extend** |

## 12. Testing

| Capability | State | Evidence | Target | Decision |
|---|---|---|---|---|
| Domain rule tests | ✅ | 6 tests | Keep + extend | Extend |
| EF model validation | ✗ | — | Test that builds the model and asserts it is valid | **Add** |
| Workflow/authorization tests | ✗ | — | Service + policy tests on in-memory provider | **Add** |
| UI end-to-end (Playwright) | ✗ | — | Not executable in this environment (no browser runtime); documented as deployment-phase work | **Documented gap** |

## 13. Summary counts

- **Present and working:** 9 capability groups (mostly identity/security).
- **Present but insufficient and extended:** 8.
- **Absent and to be built:** 22 modules/workflows.
- **Explicitly out of scope (documented):** DICOM/PACS imaging archive, payment-gateway/insurance clearing-house integration, server-side PDF rendering, e-prescribing networks, SMS gateways, multi-tenant database sharding.

---

# Delivered State (post-transformation)

The tables above record the audit of the original system and the decision taken for each capability.
This section records what was **actually delivered**, so the inventory cannot be read as an
aspiration list.

Legend: **Done** · **Partial** · **Documented gap**

## Identity, access and audit

| Capability | Status | Evidence |
|---|---|---|
| Registration, email confirmation, lockout, password policy | Done | `Program.cs`, `AccountController` |
| Password reset, resend confirmation, email change | Done | `AccountController` |
| Optional Google OAuth | Done (unverified against a live tenant) | `Program.cs` |
| Admin bootstrap, no default admin | Done | `Program.BootstrapAdminAsync` |
| 14 roles | Done | `Hospital.BLL/Helpers/Role.cs` |
| 43 permissions, policy-enforced | Done | `Permission.cs`, `RolePermissions.cs`, `Program.cs` |
| Least-privilege grant matrix, machine-verified | Done | `user-roles-permissions.md`, 384-cell comparison |
| Segregation of duties (prescribe/dispense, author/audit, record/approve refund) | Done | `EncounterAndPermissionTests`, `BillingService.ApproveRefundAsync` |
| Audit trail, append-only, metadata only | Done | `AuditLog`, `AuditLogger`, `AuditController` |
| Audit tamper-evidence, IP capture, SIEM forwarding | Documented gap | `data-architecture.md` §5 |
| MFA | Documented gap | `security-privacy-review.md` §2 |
| Branch/tenant scoping, `BranchAdmin`, `InsuranceOfficer` | Documented gap | `user-roles-permissions.md` §6 |

## Patient management

| Capability | Status | Evidence |
|---|---|---|
| MRN with gap-free sequence | Done | `NumberSequenceService`, `PT-000001` |
| Duplicate guard at registration | Done | `PatientLookupService.HasSimilarAsync` + `ConfirmDuplicate` |
| Allergies, documented history, blood group | Done | `Patient`, chart view |
| Server-side search, filter, paging | Done | `PatientsController` |
| Consolidated patient chart | Done | `Patients/Details` |
| Patient document uploads | Documented gap | Only profile images are stored |
| Sensitive-field redaction (`patient.view.sensitive`) | Documented gap | Permission defined, unenforced |

## Appointments and scheduling

| Capability | Status | Evidence |
|---|---|---|
| Weekly clinician schedule → interval slots | Done | `Schedule`, `SlotRules.GenerateSlots` |
| Date + slot booking | Done | `AppointmentsController.Book` |
| Conflict prevention | Done | `SlotRules` + filtered unique index `UX_Appointments_DoctorSlot` |
| Check-in opening an encounter | Done | `CheckInAsync` |
| Cancel, reschedule, no-show | Done | `SlotRules` transitions |
| Calendar day view | Done | Availability board per clinician |

## Clinical encounters

| Capability | Status | Evidence |
|---|---|---|
| Lifecycle Registered → Triaged → InConsultation → Completed → Signed | Done | `EncounterRules` |
| Structured vitals with range validation | Done | `VitalSign` |
| Notes: draft → signed, immutable, amendments | Done | `ClinicalNote`, `AmendsNoteId` |
| Diagnoses per encounter | Done | `Diagnosis` |
| Orders from the encounter (lab, imaging, prescription) | Done | inline forms in `Encounters/Details` |
| Severe-allergy alert | Done | chart banner |
| Concurrency tokens on notes | Documented gap | No `RowVersion` |

## Laboratory

| Capability | Status | Evidence |
|---|---|---|
| Ten-state specimen workflow | Done | `LabRules.Transitions` |
| Separate verify and release permissions | Done | `lab.result.verify`, `lab.result.release` |
| Unverified results never final | Done | `LabRules.IsClinicallyFinal`, tested |
| Reference-range classification | Done | `LabRules.Classify`, tested |
| Turnaround reporting | Done | `lab-throughput` report |

## Radiology

| Capability | Status | Evidence |
|---|---|---|
| Request → schedule → performed → reported → verified → released | Done | `ImagingOrderStatus`, `ImagingService` |
| Radiologist-authored findings and impression | Done | `SaveReportAsync` (no generated text) |
| Author and verifier recorded separately | Done | `RadiologistUserId` / `VerifiedByUserId` |
| DICOM / PACS / image storage | Documented gap | Explicitly out of scope |

## Pharmacy

| Capability | Status | Evidence |
|---|---|---|
| Prescription → review → dispense | Done | `PrescriptionStatus`, `PharmacyService` |
| Prescriptions only from a clinician's order | Done | `CreateAsync` requires a live encounter |
| Stock ledger with batches and expiry | Done | `StockBatch`, `StockMovement` |
| Inventory and expiry reports | Done | `inventory`, `expiry` |

## Admissions and beds

| Capability | Status | Evidence |
|---|---|---|
| Ward → room → bed hierarchy | Done | `Ward`, `Room`, `Bed` |
| Occupancy-safe assignment | Done | filtered unique index `UX_BedAssignments_ActiveOccupancy` |
| Transfer and discharge | Done | `AdmissionService` |
| Live bed board | Done | `Admissions/Index` |

## Billing and insurance

| Capability | Status | Evidence |
|---|---|---|
| Service catalogue, invoices, lines | Done | `BillingService.CreateInvoiceAsync` |
| Payments with overpayment rejection | Done | `BillingRules.IsPaymentValid` |
| Refunds with two-officer approval | Done | `ApproveRefundAsync` |
| Insurance coverage application | Done | `BillingRules.InsuranceCoverage` |
| Payment gateway, claim submission | Documented gap | Recorded ledger only |

## Reporting and analytics

| Capability | Status | Evidence |
|---|---|---|
| Role dashboards from live queries | Done | `WorkspaceService` |
| 18 parameterised reports | Done | `ReportService.Catalogue` |
| Filters, search, paging, summary, generation timestamp | Done | `Reports/Run` |
| CSV export with audit | Done | `Reports/Export`, `report.export` |
| Print layout | Done | print stylesheet |
| Permission gating per report | Done | descriptor permission + re-check on run/export |
| XLSX, server-side PDF, scheduled delivery | Documented gap | `reporting-specification.md` §6 |

## Platform, UX, i18n, accessibility

| Capability | Status | Evidence |
|---|---|---|
| Tokenised design system | Done | `design-system.md`, `design-system.css` |
| Role-aware application shell | Done | `_AppShell.cshtml` |
| Public shell for signed-out visitors | Done | `_AppShell.cshtml` |
| Tables, drawers, dialogs, toasts, empty states, pager | Done | `Views/Shared` |
| English + Arabic with real RTL | Done | `UiText`, `CultureController`, logical properties |
| Responsive down to 320px | Done | breakpoints, `.table-wrap` |
| WCAG-oriented markup, focus, reduced motion, forced colours | Done (unverified by tooling) | `design-system.md` §8 |
| Native-speaker Arabic review, axe/Pa11y run, screen-reader pass | Documented gap | No browser/assessor available |

## Testing

| Capability | Status | Evidence |
|---|---|---|
| 69 unit tests over nine suites | Done | `Hospital.Tests`, `test-plan.md` |
| Static view-hygiene tests (CSP, tokens, encoding) | Done | `ViewHygieneTests` |
| CI annotation of failing tests | Done | `.github/workflows/ci.yml` |
| EF model validation test | Documented gap | Requires design-time model build; deferred |
| Database integration tests (constraints firing) | Documented gap | No SQL Server available |
| Playwright E2E, visual regression, axe | Documented gap | Scenarios specified, runner unavailable |

## Delivered summary

| Classification | Count |
|---|---|
| Capabilities delivered as working software | 62 |
| Delivered with a disclosed limitation | 9 |
| Documented gaps with a stated prerequisite | 15 |
