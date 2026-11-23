# CareAxis — Commercial Readiness Assessment

**Purpose:** to separate, without hedging, what is *implemented*, what is *demonstration-only*, what
*requires backend work*, what *requires external integration*, and what *requires legal or clinical
review*. Every statement below is traceable to code in this repository or to a named gap.

**Verdict in one sentence:** CareAxis is now a coherent, compiling, tested, role-based hospital
platform suitable for **demonstration, evaluation, and pilot deployment with synthetic data**. It is
**not** ready for live patient data, and the remaining work is dominated by platform operations,
tenancy, and regulatory review rather than by application features.

---

## 1. Scope and scale of what was built

| Measure | Before | After |
|---|---|---|
| Controllers | 5 | 16 |
| Razor views | 44 | 73 |
| Action methods | 65 | 120 |
| Roles | 3 | 14 (11 operational + 3 legacy) |
| Permissions | 0 (role strings) | 43, enforced as policies |
| Domain entities | 9 | 36 entity classes |
| Reports | 0 | 18 |
| Automated tests | 5 | 70 |
| Design system | ad-hoc CSS | token-based system + RTL |
| Audit events | ad-hoc | 57 named event types across 16 domains |

---

## 2. Classification of every capability

### 2.1 Fully implemented — works end to end against a real database

| Capability | Evidence |
|---|---|
| Authentication, registration, reset, lockout, optional OAuth | `AccountController`, Identity configuration |
| Permission-claim authorisation with 43 policies | `Permission`, `RolePermissions`, `Program.cs` |
| Role-routed dashboards for eleven operational roles | `DashboardController` + three workspaces |
| Patient directory, search, paging, registration with duplicate guard | `PatientsController`, `PatientLookupService` |
| Consolidated patient chart (identity, allergies, encounters, labs, invoices) | `Patients/Details` |
| Encounter lifecycle with triage, vitals, notes, amendment, diagnoses, signing | `EncountersController`, `EncounterService` |
| Slot-based appointment booking, conflict prevention, check-in, cancel, no-show | `AppointmentsController`, `AppointmentSchedulingService` |
| Laboratory specimen-to-release workflow with separate verify and release | `LaboratoryController`, `LabService` |
| Radiology request-to-release with radiologist-authored reports | `RadiologyController`, `ImagingService` |
| Pharmacy review and dispensing with stock deduction | `PharmacyController`, `PharmacyService` |
| Admissions, bed board, bed assignment, discharge with database-enforced single occupancy | `AdmissionsController`, `AdmissionService` |
| Invoicing, payments, refunds with two-officer approval | `BillingController`, `BillingService` |
| 18 permission-gated reports with filters, paging, CSV, print | `ReportsController`, `ReportService` |
| Append-only audit trail with filtering | `AuditController` |
| Bilingual interface with Arabic RTL presentation | `UiText`, `CultureController`, design system |
| Responsive layout, WCAG-oriented markup, print stylesheet | `design-system.css`, shell |
| Schema generation, migrations artifact, health checks | CI pipeline |

### 2.2 Demonstration-only — real code, synthetic data, labelled as such

| Item | What makes it demo-only |
|---|---|
| Seeded users and clinical records | Synthetic; `IsSynthetic = true`; `example.invalid` addresses; password printed only to logs in non-production |
| All clinical values, results and reports in the seed | Fabricated for demonstration. The UI labels the environment as containing synthetic data. |
| Email delivery | Queued to Hangfire; without SMTP configuration nothing is sent. No delivery is claimed. |
| Google sign-in | Registered only when credentials are configured; untested against a live tenant |

**Nothing in the demo layer invents clinical results at runtime.** Seeded values are data; the
application never generates a finding, a result, a prescription or a payment on its own.

### 2.3 Implemented with a disclosed limitation

| Capability | Limitation |
|---|---|
| Audit trail | Metadata only; no IP address, user agent, hash chain or SIEM forwarding |
| Refund approval | Rule enforced in the service layer; there is no separate "approvals inbox" role |
| Optimistic concurrency | Last-write-wins on concurrent note edits (no `RowVersion`) |
| Patient portal authorisation | Ownership checked in controller code, not by policy |
| Client-side validation | Progressive enhancement only; every rule is also server-side |
| Radiology | Report text and metadata; **no images**, no DICOM, no PACS |
| Billing | Ledger records; **no payment gateway**, no clearinghouse, no claim submission |
| CSV export | Capped at 200 rows; no XLSX, no PDF |
| Schema initialisation | Falls back to model-created schema only where no migrations exist and never in production |

### 2.4 Requires backend work — specified, not built

| # | Item | Why it blocks production |
|---|---|---|
| 1 | **Multi-factor authentication** | Password-only authentication for clinical and financial roles |
| 2 | **Branch / tenant scoping** | No claim, no query filter; two hospitals cannot share an instance safely |
| 3 | **`BranchAdmin` and `InsuranceOfficer` roles** | Named in the brief; absent from the role enum |
| 4 | **Break-glass emergency access** | No emergency override with reason capture and review |
| 5 | **Retention, erasure and subject-access workflows** | No retention policy, no export, no rectification/erasure path |
| 6 | **Optimistic concurrency on clinical records** | Concurrent edits silently overwrite |
| 7 | **Sensitive-field redaction** (`patient.view.sensitive`) | Defined in the catalogue, unenforced |
| 8 | **Security regression matrix test** | No automated authz test that hits every action as every role |
| 9 | **PDF and XLSX generation** | Print-to-PDF only |
| 10 | **Scheduled report delivery** | Hangfire infrastructure present, no schedules |
| 11 | **Session management UI** | No device list or sign-out-everywhere |
| 12 | **Audit enhancements** | IP, user agent, tamper-evidence |
| 13 | **Playwright E2E and visual regression** | Scenarios specified in `test-plan.md` §8; runner unavailable here |

### 2.5 Requires external integration

| Integration | Status | Notes |
|---|---|---|
| SMTP / transactional email | Configured via options, **not verified** | No credentials in this environment |
| Payment gateway | **Absent** | Would require PCI-DSS scoping |
| Insurance clearinghouse | **Absent** | Would require payer contracts and message formats |
| PACS / DICOM | **Absent** | Would require a DICOM store and viewer |
| FHIR exchange / HIE | **Absent** | See `healthcare-standards-mapping.md` §4 |
| Terminology services (SNOMED, LOINC, ICD) | **Absent** | Free-text codes only |
| SMS / notification provider | **Absent** | — |
| Identity provider (SAML / OIDC beyond Google) | **Absent** | Enterprise SSO not supported |
| SIEM | **Absent** | Audit trail is queryable, not forwarded |

### 2.6 Requires legal, clinical or regulatory review

| Item | Why it cannot be settled by engineering |
|---|---|
| HIPAA / GDPR position | Needs BAAs, DPIAs, lawful-basis analysis and jurisdictional advice |
| Medical-device classification | A system influencing clinical decisions may be a regulated device |
| Clinical safety case | Requires a qualified clinician's risk assessment of the workflows |
| Retention schedule | Statutory periods differ by jurisdiction and record class |
| Consent and information-governance model | Policy decision, not a schema decision |
| Arabic clinical terminology | Needs a native-speaking clinical reviewer; the interface catalogue is complete but nuance is unverified |
| Penetration test | Requires an independent assessor |

---

## 3. Deployment decision: multi-tenant or per-hospital

**Recommendation: deploy one instance per hospital. Do not attempt multi-tenancy in this schema.**

| Factor | Assessment |
|---|---|
| Current isolation | **None.** `BranchId` exists on organizations, but no branch claim is issued and no query filter is applied, so any authenticated user reaching a query can reach another branch's rows |
| What multi-tenancy would require | A tenant claim on the principal, a global query filter on every clinical and financial `DbSet`, tenant-aware unique indexes (MRN uniqueness per tenant, not globally), tenant-scoped sequences, per-tenant file storage, and a test suite that proves non-leakage on every entity |
| Cost of getting it wrong | A cross-tenant leak of patient data is the most serious failure mode a hospital system can have |
| Per-hospital cost | A separate database and app instance; straightforward with the existing container and configuration model |
| Scale assumption | A single hospital's data volume is well within one SQL Server; there is no scale-driven reason to share a tenant |

**Decision record:** per-hospital deployment is the supported model for the current schema.
Multi-tenancy is recorded as a **backend work item**, not a configuration toggle, and must not be
advertised until the isolation tests exist.

---

## 4. Readiness scoring

Scored 1–5 where 3 means "fit for a supervised pilot with synthetic or consented data".

| Dimension | Score | Rationale |
|---|---:|---|
| Functional coverage of the brief | 4 | All named modules exist with real workflows; branch-admin and insurance roles missing |
| Clinical workflow safety | 4 | Strong invariants (release gate, signed-record immutability, single-bed occupancy) verified in code and tests |
| Authorisation and least privilege | 4 | Claim-based, matrix-verified, segregation of duties; no tenancy, no MFA |
| Data integrity | 4 | Database-enforced uniqueness for the two dangerous invariants; no concurrency tokens |
| Audit and traceability | 3 | Good coverage and append-only; no tamper-evidence, no forwarding |
| Security posture | 3 | Strong headers and CSP, strict anti-forgery, signature-checked uploads; missing MFA, at-rest encryption and backup |
| Accessibility | 3 | Design intent and markup discipline are in place; no automated or assistive-technology verification |
| Bilingual support | 4 | Complete catalogue, real RTL layout, logical properties; no native-speaker review |
| Reporting | 4 | 18 live reports, exports, print; no XLSX/PDF/scheduling |
| Test coverage | 2 | Strong on decision logic, absent on integration, E2E and visual |
| Operational readiness | 2 | No backup, retention, monitoring, or tenancy; schema flow is pipeline-generated |
| Documentation | 5 | Twelve documents, each citing code |

---

## 5. What a buyer would actually get

**Can be demonstrated today, convincingly, with synthetic data:**
a role-based hospital platform where a receptionist books a slot that a clinician cannot double-book,
a nurse triages, a doctor documents and signs a record that then refuses edits, a laboratory collects
and releases a result that is explicitly not final until verified, a radiologist authors and releases
a report, a pharmacist dispenses against stock, a bed cannot be given to two patients, a refund above
threshold needs a second officer, and an auditor can read every one of those actions — in English or
Arabic, on a laptop or a phone.

**Cannot be delivered today:**
a production deployment holding real patient data, a multi-hospital tenancy, insurance claim
submission, medical imaging, or any statement of regulatory compliance.

---

## 6. Recommended path to production

| Stage | Work | Exit criterion |
|---|---|---|
| 1. Hardening | MFA, concurrency tokens, retention/erasure, session management, audit enhancements | Security review signs off |
| 2. Verification | Playwright E2E + visual regression, axe, database integration tests, load test | Suite green in CI with a browser runner |
| 3. Operations | Backup and tested restore, at-rest encryption, monitoring/alerting, SIEM ingestion | Runbook exercised in a staging environment |
| 4. Clinical | Clinical safety review, terminology integration, pilot with synthetic data in a real setting | Clinician sign-off |
| 5. Compliance | DPIA, legal review, penetration test, device classification | Counsel and assessor sign-off |
| 6. Pilot | One hospital, real data, restricted scope, supported by the vendor | Pilot exit criteria met |
| 7. Multi-tenancy (optional) | Tenant claims, filters, isolation tests | Non-leakage proven per entity |

Stages 1–3 are engineering. Stages 4–5 are not, and no amount of code closes them.
