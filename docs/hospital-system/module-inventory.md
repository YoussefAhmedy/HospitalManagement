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
| Roles | ◐ | `Role` enum: Admin, Patient, Doctor | 12 operational roles | **Extend enum + policy catalogue** |
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
