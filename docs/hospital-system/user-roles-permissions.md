# CareAxis — User Roles & Permissions

**Status of this document:** describes the *implemented* role model. Every permission name below
exists as a constant in `Hospital.BLL/Permissions/Permission.cs` and is granted by
`Hospital.BLL/Permissions/RolePermissions.cs`, which is the single source of truth seeded into
Identity role claims at start-up.

---

## 1. Model

| Concept | Implementation |
|---|---|
| Role | `Hospital.BLL.Helpers.Role` enum, seeded as an Identity role |
| Permission | String constant, e.g. `patient.view`, `lab.result.release` |
| Grant | Identity **role claim** of type `careaxis:perm` |
| Principal | User → roles → permission claims, materialised into the ticket by `PermissionClaimsTransformation` |
| Enforcement | ASP.NET Core authorisation policy `perm:<permission>` declared on every controller or action |
| Clinical marker | `careaxis:clinical` claim set for roles that may write to the clinical record |

Authorisation is evaluated **server-side on every request**. Hiding a control in a view is a
usability measure only; it is never the access control. Every new action in this codebase carries
an explicit `[Authorize(Policy = "perm:…")]`.

### Why claims rather than role strings

Role-string checks (`[Authorize(Roles = "Doctor")]`) couple authorisation to a job title. A
hospital that wants a senior nurse to sign notes, or a billing officer to read the laboratory
worklist, requires a code change. Claims let the grant set evolve without touching controllers, and
they make the effective privilege of a role auditable in one file.

---

## 2. Roles

| Role | Scope | Primary workspace | Notes |
|---|---|---|---|
| **Super Admin** | Platform | Administration | Full catalogue. Reserved for deployment operators, not hospital staff. |
| **Hospital Admin** | One hospital | Administration | Full catalogue within the hospital tenant. |
| **Admin** (legacy) | Legacy | Legacy `Admin` screens | Retained for existing data; narrower than `HospitalAdmin`. To be retired. |
| **Branch Admin** | One branch | Administration | **Not yet implemented** — see §6. |
| **Doctor** | Own patients / department | Clinician dashboard | Documents encounters, orders, prescribes, signs. |
| **Nurse** | Ward / clinic | Reception + triage queue | Triage, vitals, specimen collection, bed management. Does **not** sign encounters. |
| **Receptionist** | Front desk | Reception dashboard | Registration, booking, check-in. No clinical write access. |
| **Lab Technician** | Laboratory | Laboratory worklist | Collects, enters, verifies, releases. Cannot prescribe. |
| **Radiology Specialist** | Imaging | Radiology worklist | Reports, verifies, releases imaging. |
| **Pharmacist** | Pharmacy | Pharmacy queue | Reviews and dispenses. Cannot author prescriptions. |
| **Billing Officer** | Finance | Billing | Invoices, payments, refunds, insurance. No clinical read beyond demographics. |
| **Insurance Officer** | Finance | Billing (insurance subset) | **Not yet implemented** as a distinct role — see §6. |
| **Medical Records Officer** | Health information | Patient directory | Registration, correction, release of records. |
| **Management** | Hospital-wide | Reports | Read-only operational and financial reporting. |
| **Auditor** | Hospital-wide read | Audit trail | Read-only; can read the audit trail and clinical records, cannot write. |
| **Patient** | Self | Patient portal | Own appointments and own records only. |

---

## 3. Permission catalogue

### Patient master data
`patient.view` · `patient.create` · `patient.edit` · `patient.delete` · `patient.view.sensitive`

### Scheduling
`appointment.view` · `appointment.create` · `appointment.manage` · `appointment.checkin`

### Clinical
`encounter.view` · `encounter.create` · `encounter.document` · `encounter.sign` · `triage.record`

### Diagnostics
`lab.order` · `lab.collect` · `lab.result.enter` · `lab.result.verify` · `lab.result.release`
`imaging.view` · `imaging.order` · `imaging.manage` · `imaging.report` · `imaging.verify` · `imaging.release`

### Pharmacy
`prescription.create` · `prescription.review` · `prescription.dispense` · `inventory.manage`

### Operations
`admission.view` · `admission.manage` · `bed.manage` · `discharge.approve`

### Billing
`billing.view` · `billing.manage` · `billing.refund` · `insurance.manage`

### Governance
`report.view` · `report.financial` · `report.export` · `audit.view` · `user.manage` · `settings.manage`

---

## 4. Grant matrix

`●` granted · `–` not granted

| Permission | SuperAdmin | HospitalAdmin | Doctor | Nurse | Reception | Lab | Radiology | Pharmacy | Billing | Records | Mgmt | Auditor |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `patient.view` | ● | ● | ● | ● | ● | ● | ● | ● | ● | ● | ● | – |
| `patient.create` | ● | ● | – | – | ● | – | – | – | – | ● | – | – |
| `patient.edit` | ● | ● | – | – | ● | – | – | – | – | ● | – | – |
| `appointment.view` | ● | ● | ● | ● | ● | – | – | – | – | – | – | – |
| `appointment.manage` | ● | ● | ● | – | ● | – | – | – | – | – | – | – |
| `appointment.checkin` | ● | ● | – | – | ● | – | – | – | – | – | – | – |
| `encounter.view` | ● | ● | ● | ● | – | – | – | – | – | ● | ● | ● |
| `encounter.document` | ● | ● | ● | ● | – | – | – | – | – | – | – | – |
| `encounter.sign` | ● | ● | ● | – | – | – | – | – | – | – | – | – |
| `triage.record` | ● | ● | ● | ● | – | – | – | – | – | – | – | – |
| `lab.order` | ● | ● | ● | – | – | – | – | – | – | – | – | – |
| `lab.collect` | ● | ● | – | ● | – | ● | – | – | – | – | – | – |
| `lab.result.enter` | ● | ● | – | – | – | ● | – | – | – | – | – | – |
| `lab.result.release` | ● | ● | – | – | – | ● | – | – | – | – | – | – |
| `imaging.order` | ● | ● | ● | – | – | – | – | – | – | – | – | – |
| `imaging.report` | ● | ● | – | – | – | – | ● | – | – | – | – | – |
| `imaging.release` | ● | ● | – | – | – | – | ● | – | – | – | – | – |
| `prescription.create` | ● | ● | ● | – | – | – | – | – | – | – | – | – |
| `prescription.review` | ● | ● | – | – | – | – | – | ● | – | – | – | – |
| `prescription.dispense` | ● | ● | – | – | – | – | – | ● | – | – | – | – |
| `admission.view` | ● | ● | ● | ● | ● | – | – | – | – | – | ● | – |
| `admission.manage` | ● | ● | – | – | – | – | – | – | – | – | – | – |
| `bed.manage` | ● | ● | – | ● | – | – | – | – | – | – | – | – |
| `discharge.approve` | ● | ● | – | – | – | – | – | – | – | – | – | – |
| `billing.view` | ● | ● | – | – | – | – | – | – | ● | – | ● | – |
| `billing.manage` | ● | ● | – | – | – | – | – | – | ● | – | – | – |
| `billing.refund` | ● | ● | – | – | – | – | – | – | – | – | – | – |
| `report.view` | ● | ● | ● | ● | ● | ● | ● | ● | ● | ● | ● | ● |
| `report.financial` | ● | ● | – | – | – | – | – | – | ● | – | ● | – |
| `report.export` | ● | ● | – | – | – | – | – | – | ● | ● | ● | – |
| `audit.view` | ● | ● | – | – | – | – | – | – | – | – | – | ● |
| `user.manage` | ● | ● | – | – | – | – | – | – | – | – | – | – |

`SuperAdmin` receives `Permission.All` programmatically rather than by an enumerated list, so a new
permission cannot be forgotten during seeding.

---

## 5. Segregation of duties

These separations are deliberate and are asserted in `Hospital.Tests/EncounterAndPermissionTests.cs`:

| Separation | Why |
|---|---|
| Reception **cannot** enter results or sign encounters | Front-desk staff must not author clinical content. |
| Pharmacist **cannot** create prescriptions | A dispenser must not also be the prescriber. |
| Laboratory staff **cannot** prescribe or bill | Diagnostic staff must not control revenue. |
| Billing **cannot** read clinical notes | Financial staff see demographics and charges only. |
| Auditor **cannot** write anything | An auditor who can modify records cannot audit them. |
| Refund approval is separate from refund entry | `billing.manage` records, `billing.refund` approves; a large refund cannot be approved by the officer who entered it (`ApprovedByUserId` ≠ `RecordedByUserId`). |

### Break-glass (not implemented)

Emergency access to a patient outside a clinician's normal relationship is **not** implemented.
Production deployment requires a break-glass flow with mandatory reason capture and retrospective
review. Recorded as a gap in `commercial-readiness-assessment.md`.

---

## 6. Known gaps in the role model

| Gap | Consequence | Disposition |
|---|---|---|
| `BranchAdmin` and `InsuranceOfficer` roles absent from the enum | Branch-level and insurance-specific scoping is unavailable | **Backend work required.** Both are named in the brief; the enum must be extended and `RolePermissions` updated. |
| No branch/tenant claim is issued at sign-in | A user with a branch scope could not be filtered to it | **Backend work required.** Requires a `BranchId` on the user and a query filter. |
| Patient portal authorisation is by ownership check in controller code | Works, but is not policy-based | Acceptable for now; flagged for consolidation. |
| No MFA | Password-only authentication | **Backend work required** for production. |
| `patient.view.sensitive` is defined but unenforced | Sensitive-field redaction is not active | **Backend work required.** |
| `Role.Admin` retained | Two overlapping administrator roles exist | Deprecate after data migration. |

The gap list is the honest position: the *enforcement mechanism* is complete and tested; the *role
catalogue* is complete for the hospital roles exercised by the application; branch scoping,
insurance scoping and MFA are not built.
