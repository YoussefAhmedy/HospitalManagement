# CareAxis — User Roles & Permissions

**Status of this document:** describes the *implemented* role model. Every permission name below
exists as a constant in `Hospital.BLL/Permissions/Permission.cs` and is granted by
`Hospital.BLL/Permissions/RolePermissions.cs`, which is the single source of truth seeded into
Identity role claims at start-up.

---

## The catalogue at a glance

**43 permissions**, of which **39 are enforced** by an authorisation attribute and **4 are
reserved**:

| Reserved permission | Why it exists | What it gates today |
|---|---|---|
| `patient.delete` | Records whether any role may destroy a patient record | Nothing. No delete path exists; retention is a legal decision. |
| `patient.view.sensitive` | Names the redaction capability (HIV status, psychiatric history, protected identifiers) | Nothing. Redaction is backend work. |
| `insurance.manage` | Names insurer, policy and pre-authorisation administration | Nothing. Only coverage values on an invoice are recorded. |
| `settings.manage` | Names organisation-wide configuration | Nothing. Configuration is deployment-time. |

Holding a reserved permission grants no access, and none of them is presented anywhere as a live
control. `AuthorizationCoverageTests` fails if a fifth permission appears unenforced, and also fails
if one of these four silently becomes enforced without this table being updated.

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

`●` granted · `–` not granted. **Generated from `RolePermissions`** — all 43 permissions × all 14
roles, **602 cells**. The previous version of this table listed 32 permissions and omitted `Admin`
and `Patient`; it was corrected by deriving the table from the code rather than by hand.

| Permission | SuperAdmin | HospitalAdmin | Admin | Doctor | Nurse | Reception | Lab | Radiology | Pharmacy | Billing | Records | Mgmt | Auditor | Patient |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `patient.view` | ● | ● | ● | ● | ● | ● | ● | ● | ● | ● | ● | ● | – | – |
| `patient.create` | ● | ● | ● | – | – | ● | – | – | – | – | ● | – | – | – |
| `patient.edit` | ● | ● | ● | – | – | ● | – | – | – | – | ● | – | – | – |
| `patient.delete` | ● | ● | – | – | – | – | – | – | – | – | – | – | – | – |
| `patient.view.sensitive` | ● | ● | – | – | – | – | – | – | – | – | – | – | – | – |
| `appointment.view` | ● | ● | ● | ● | ● | ● | – | – | – | – | – | – | – | – |
| `appointment.create` | ● | ● | ● | – | – | ● | – | – | – | – | – | – | – | – |
| `appointment.manage` | ● | ● | ● | ● | – | ● | – | – | – | – | – | – | – | – |
| `appointment.checkin` | ● | ● | ● | – | – | ● | – | – | – | – | – | – | – | – |
| `encounter.view` | ● | ● | ● | ● | ● | – | – | – | – | – | ● | ● | ● | – |
| `encounter.create` | ● | ● | – | ● | – | – | – | – | – | – | – | – | – | – |
| `encounter.document` | ● | ● | – | ● | ● | – | – | – | – | – | – | – | – | – |
| `encounter.sign` | ● | ● | – | ● | – | – | – | – | – | – | – | – | – | – |
| `triage.record` | ● | ● | – | ● | ● | – | – | – | – | – | – | – | – | – |
| `lab.order` | ● | ● | ● | ● | – | – | – | – | – | – | – | – | – | – |
| `lab.collect` | ● | ● | – | – | ● | – | ● | – | – | – | – | – | – | – |
| `lab.result.enter` | ● | ● | – | – | – | – | ● | – | – | – | – | – | – | – |
| `lab.result.verify` | ● | ● | – | – | – | – | ● | – | – | – | – | – | – | – |
| `lab.result.release` | ● | ● | – | – | – | – | ● | – | – | – | – | – | – | – |
| `imaging.view` | ● | ● | ● | ● | – | – | ● | ● | – | – | – | – | – | – |
| `imaging.order` | ● | ● | ● | ● | – | – | – | – | – | – | – | – | – | – |
| `imaging.manage` | ● | ● | ● | – | – | – | – | ● | – | – | – | – | – | – |
| `imaging.report` | ● | ● | – | – | – | – | – | ● | – | – | – | – | – | – |
| `imaging.verify` | ● | ● | – | – | – | – | – | ● | – | – | – | – | – | – |
| `imaging.release` | ● | ● | – | – | – | – | – | ● | – | – | – | – | – | – |
| `prescription.create` | ● | ● | ● | ● | – | – | – | – | – | – | – | – | – | – |
| `prescription.review` | ● | ● | – | – | – | – | – | – | ● | – | – | – | – | – |
| `prescription.dispense` | ● | ● | – | – | – | – | – | – | ● | – | – | – | – | – |
| `inventory.manage` | ● | ● | – | – | – | – | – | – | ● | – | – | – | – | – |
| `admission.view` | ● | ● | ● | ● | ● | ● | – | – | – | – | – | ● | – | – |
| `admission.manage` | ● | ● | ● | – | – | – | – | – | – | – | – | – | – | – |
| `bed.manage` | ● | ● | ● | – | ● | – | – | – | – | – | – | – | – | – |
| `discharge.approve` | ● | ● | – | – | – | – | – | – | – | – | – | – | – | – |
| `billing.view` | ● | ● | ● | – | – | – | – | – | – | ● | – | ● | – | – |
| `billing.manage` | ● | ● | – | – | – | – | – | – | – | ● | – | – | – | – |
| `billing.refund` | ● | ● | – | – | – | – | – | – | – | – | – | – | – | – |
| `insurance.manage` | ● | ● | – | – | – | – | – | – | – | ● | – | – | – | – |
| `report.view` | ● | ● | ● | ● | ● | ● | ● | ● | ● | ● | ● | ● | ● | – |
| `report.financial` | ● | ● | – | – | – | – | – | – | – | ● | – | ● | – | – |
| `report.export` | ● | ● | ● | – | – | – | – | – | – | ● | ● | ● | – | – |
| `audit.view` | ● | ● | – | – | – | – | – | – | – | – | – | – | ● | – |
| `user.manage` | ● | ● | ● | – | – | – | – | – | – | – | – | – | – | – |
| `settings.manage` | ● | ● | – | – | – | – | – | – | – | – | – | – | – | – |

`SuperAdmin` and `HospitalAdmin` receive `Permission.All` programmatically rather than by an
enumerated list, so a newly added permission cannot be forgotten during seeding.
`Patient` deliberately holds **no permissions**: the patient portal is gated on the `Patient` *role*
and on record ownership (`ResourceOwnership.IsOwner`), because a patient's authority derives from
being that patient — not from a capability grant that an administrator could widen.

### Findings from deriving this table

Deriving the matrix exposed four least-privilege questions that a hand-maintained table had hidden.
They are recorded here rather than quietly fixed, because each is a policy decision:

| Finding | Consequence | Position |
|---|---|---|
| `Admin` holds `user.manage` but **not** `encounter.view` | The legacy administrator account can create staff but cannot open a clinical encounter | Accepted: `Admin` is the legacy role, retained for compatibility only. Use `HospitalAdmin`. |
| `BillingOfficer` holds both `billing.manage` and `billing.refund` | A billing officer could in principle enter and approve the same refund | Mitigated in code: `ApproveRefundAsync` rejects an approver who is the recording officer. The permission pair does not enforce it — the service does. |
| `MedicalRecordsOfficer` holds `report.export` but not `report.financial` | Records staff can export clinical reports but not revenue reports | Intended. |
| `Admin` holds `imaging.manage` and `lab.order` | The legacy administrator can act on diagnostics | Accepted for the legacy role; `HospitalAdmin` is the supported administrative role. |

The refund mitigation is the important one: **segregation of duties is enforced by the service, not
by the permission set.** A future reader should not assume that splitting the permission would be
sufficient, or that holding both permissions is harmless.

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
