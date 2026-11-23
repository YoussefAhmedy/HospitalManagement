# CareAxis — Data Architecture

**Basis:** generated from the entity model and `IEntityTypeConfiguration` classes in
`DAL/Entities`. Everything below is present in the model; the physical schema is produced by
`dotnet ef migrations` in CI, not hand-authored (see `Migrations & deployment` at the end).

---

## 1. Shape of the database

One SQL Server database, five schema areas plus Identity and Hangfire:

| Area | Namespace | Purpose |
|---|---|---|
| Identity & people | `DAL.Entities` | `ApplicationUser` (TPH base) → `Patient`, `Doctor`, `Admin` |
| Organization | `DAL.Entities.Organization` | `Branch`, `Department` |
| Clinical | `DAL.Entities.Clinical` | `Encounter`, `VitalSign`, `ClinicalNote`, `Diagnosis` |
| Diagnostics | `DAL.Entities.Diagnostics` | `LabTest`, `LabOrder`, `LabOrderItem`, `ImagingOrder` |
| Pharmacy | `DAL.Entities.Pharmacy` | `Medication`, `StockBatch`, `Prescription`, `PrescriptionItem`, `StockMovement` |
| Operations | `DAL.Entities.Operations` | `Ward`, `Room`, `Bed`, `Admission`, `BedAssignment`, `NumberSequence` |
| Billing | `DAL.Entities.Billing` | `ServiceItem`, `Invoice`, `InvoiceLine`, `Payment`, `InsurancePolicy` |
| Governance | `DAL.Entities` | `AuditLog` |
| Legacy | `DAL.Entities` | `Appointment`, `MedicalRecord`, `Schedule`, `Shift`, `Specialization` |

**Inheritance.** `Patient`, `Doctor` and `Admin` all derive from `ApplicationUser`, stored in a single
table (table-per-hierarchy). This preserves the existing Identity design and means one sign-in path
for staff and patients.

> **Known constraint.** Because `Doctor` and `Patient` share a table, a doctor cannot also be a
> patient record in the same row. Provisioning a clinician as a patient requires a separate patient
> row with its own login. This is a real limitation of the inherited model; it is recorded in
> `commercial-readiness-assessment.md` rather than papered over.

---

## 2. Identifiers

Human-facing identifiers are allocated by `NumberSequence`, never by `MAX(id)+1`:

| Document | Format | Owner |
|---|---|---|
| Patient MRN | `PT-000001` | `PatientLookupService` / registration |
| Encounter | `ENC-000001` | `EncounterService` |
| Appointment | `APT-000001` | `AppointmentSchedulingService` |
| Laboratory order | `LAB-000001` | `LabService` |
| Imaging order | `IMG-000001` | `ImagingService` |
| Prescription | `RX-000001` | `PharmacyService` |
| Admission | `ADM-000001` | `AdmissionService` |
| Invoice / receipt | `INV-000001` / `RCP-000001` | `BillingService` |

Each has a unique index on the formatted column, so a duplicate is a database error rather than a
silent overwrite.

---

## 3. Integrity that the database enforces

These are the invariants that would be dangerous to hold in code alone, because two concurrent
requests can each read a "free" state before either writes.

| Invariant | Mechanism | Where |
|---|---|---|
| A clinician cannot hold two appointments in one slot | **Filtered unique index** on `(DoctorID, AppointmentDate, SlotMinutes)` where `DoctorID IS NOT NULL AND Status NOT IN (2, 4)` (2 = NotApproved, 4 = Cancelled) | `AppointmentConfig` |
| A bed cannot hold two active patients | **Filtered unique index** on `BedId` where `ReleasedAtUtc IS NULL` (`UX_BedAssignments_ActiveOccupancy`) | `OperationsConfig` |
| Document numbers are unique | Unique index per formatted number column | All config classes |
| A room number is unique within its ward | Unique `(WardId, Number)` | `OperationsConfig` |
| A bed label is unique within its room | Unique `(RoomId, Label)` | `OperationsConfig` |
| A department code is unique within its branch | Unique `(BranchId, Code)` | `OrganizationConfig` |
| Branch, department, lab-test and service codes are unique | Unique index | `OrganizationConfig`, `DiagnosticsConfig`, `BillingConfig` |
| A test cannot appear twice on one laboratory order | Unique `(LabOrderId, LabTestId)` | `DiagnosticsConfig` |
| Deleting a hospital that has clinical rows is impossible | `DeleteBehavior.Restrict` on encounter → lab/imaging, order → test | `DiagnosticsConfig`, `ClinicalConfig` |
| Deleting an admission removes only its own bed assignments | `DeleteBehavior.Cascade` | `OperationsConfig` |

Service-layer rules duplicate the two filtered indexes so the user receives a readable message
instead of a raw constraint violation. The index remains the authority.

---

## 4. Soft lifecycle, not hard delete

Clinical and financial rows are **never deleted** by the application:

| Aggregate | Lifecycle |
|---|---|
| Encounter | `Registered → Triaged → InConsultation → Completed → Signed`; `Cancelled` from any open state |
| Clinical note | `Draft → Signed`; a correction is a **new** note carrying `AmendsNoteId` |
| Laboratory order | `Requested → AwaitingCollection → Collected → Received → Processing → AwaitingVerification → Released`; `Cancelled` before release |
| Imaging order | `Requested → Scheduled → Performed → Reported → Verified → Released` |
| Prescription | `Pending → Reviewed → PartiallyDispensed → Dispensed`; `Rejected` |
| Admission | `Requested → Admitted → Discharged`; `Cancelled` |
| Invoice | `Issued → PartiallyPaid → Paid`; `Void` (never deleted) |
| Bed assignment | Released by timestamp (`ReleasedAtUtc`), never deleted |

Because every one of these is a state or a timestamp rather than a row deletion, the historical
record survives and the audit story is consistent.

---

## 5. Audit data

`AuditLog` is append-only at the application level (no controller exposes create, update or delete)
and stores **metadata only**:

| Column | Content | Deliberately excluded |
|---|---|---|
| `EventType` | e.g. `patient.chart.view`, `lab.result.enter` | — |
| `ActorUserId` | Identity id of the person acting | Name, email |
| `SubjectUserId` | Identity id of the patient concerned, when relevant | Clinical content |
| `ResourceType` / `ResourceId` | Entity name and primary key | Record contents |
| `OccurredAtUtc` | UTC timestamp (server clock) | Client-supplied time |
| — | — | IP address and user agent are **not** stored; adding them requires a schema change |

No clinical text, no credentials, no request bodies. This keeps the trail safe to retain for longer
than the clinical data and safe to export to a SIEM.

---

## 6. Query patterns

| Pattern | Implementation | Why |
|---|---|---|
| List screens | `PagedResult<T>` with `Skip/Take`, server-side filter and search | Never materialise a table |
| Projections | Anonymous-type projections or record DTOs | Avoids tracking whole graphs for a 5-column table |
| Detail screens | `AsNoTracking()` + `Include` of exactly the needed graph | One round trip, no change tracking |
| Dashboards | One aggregated query per figure in `WorkspaceService` | Headline numbers are live, never cached stale |
| Reports | Filter objects → `IQueryable` → count + page | The printed page, the CSV and the screen share one query |
| Patient lookup | Anonymous projection before `ToLower`/`Contains` | Keeps the predicate translatable to SQL |
| Ownership filter | `ResourceOwnership.IsOwner` on the authenticated id | Portal reads are scoped in code |

**No `SELECT *`.** No screen loads a full table. Every list has a page size and a maximum page size.

---

## 7. Concurrency

| Concern | Approach |
|---|---|
| Document numbers | Sequence row updated inside a transaction |
| Slot booking | Filtered unique index; violation mapped to a business error |
| Bed assignment | Filtered unique index; violation mapped to a business error |
| Refund approval | `ApproveRefundAsync` rejects the recording officer by comparing `ReceivedByUserId` with the approver, then stamps `ApprovedByUserId`/`ApprovedAtUtc` |
| Clinical signing | State transition checked and committed in one `SaveChanges` |
| Optimistic concurrency | **Not configured** (no `RowVersion`). Concurrent edits to the same note are last-write-wins. Recorded as a gap. |

---

## 8. Migrations & deployment

Migrations are **generated by the pipeline, not committed by hand**:

1. `build-and-test` must pass.
2. `migrations` job runs `dotnet ef migrations add InitialSchema` against the model.
3. `dotnet ef migrations script --idempotent` produces `careaxis-schema.sql`.
4. Both are uploaded as the `ef-migrations` artifact (14-day retention).

The idempotent script is the deployment artefact: it can be applied repeatedly and safely to an
existing database. Because the model changed substantially, the historical migration set was replaced
by a single initial migration rather than a chain — appropriate for a pre-production schema, and
explicitly **not** the approach to take once a database holds real patient data, at which point
migrations must be additive and reversible.

### Row-level security and multi-tenancy

There is **no row-level security** today. `BranchId` exists on organizations and on the directory
lookups, but it is not stamped onto clinical rows and there is no query filter or session context
that would enforce tenant isolation. Multi-tenant deployment is therefore **not possible in the
current schema**; see `commercial-readiness-assessment.md` for the decision and the work required.

---

## 9. Data classification

| Class | Examples | Handling |
|---|---|---|
| Direct identifier | Name, MRN, phone, address, DOB | Visible to clinical roles; redaction for `patient.view.sensitive` is defined but not enforced |
| Clinical | Vitals, notes, diagnoses, results | Role-gated; immutable once signed/released |
| Financial | Invoices, payments, insurance | Billing/management only; never joined to clinical text |
| Audit | Actor, action, resource, time | Append-only, metadata only |
| Credential | Password hash, security stamp | Identity-managed; never logged |
| Synthetic | Seeded demo records (`IsSynthetic = true`) | Safe to display in demonstrations; labelled in the UI |

**There is no real patient data in this repository.** The seeder creates synthetic records only, all
using `example.invalid` addresses.
