# CareAxis — Workflow Mapping

Every clinical and financial workflow the application implements, mapped from the real role that
performs it, through the states it can occupy, to the permission that gates each transition, the
persisted evidence, and the test that covers the rule.

**Legend for "Enforced by"**
`Policy` — an `[Authorize(Policy = "perm:…")]` on the action
`Rule` — a pure rule in `Hospital.BLL/Common/Rules` (covered by unit tests)
`DB` — a database constraint (unique index, filtered index, foreign key)
`Audit` — an `AuditLog` row written in the same request

---

## 1. Patient registration

| Step | Role | Permission | Enforced by | Evidence |
|---|---|---|---|---|
| Look up the patient | Receptionist, Records, Nurse, Doctor | `patient.view` | Policy | — |
| Run duplicate check | Receptionist, Records | `patient.create` | Rule (`PatientLookupService.HasSimilarAsync`) | similarity on name + DOB + phone |
| Register | Receptionist, Records | `patient.create` | Policy | `Patient` row, `MRN` unique index |
| Allocate MRN | system | — | DB (`NumberSequence`) | `PT-000001` format |
| Record allergy | Nurse, Doctor | `patient.edit` | Policy + Audit | `Allergy` row, `patient.allergy.add` audit event |

**Negative cases covered**
- Registering a near-duplicate requires the operator to re-submit with `ConfirmDuplicate` set; the
  form re-renders with the candidate matches listed. (UI guard, not a hard block — two real people
  can share a name and birthday, so the operator decides.)
- MRN collision is impossible: the sequence allocates inside a transaction and the column carries a
  unique index.

---

## 2. Appointment lifecycle

```
                   ┌──────────► Cancelled ◄──────────┐
                   │                                 │
Pending ──confirm──► Approved ──check-in──► CheckedIn ──complete──► Completed
   │                   │
   └──decline──► NotApproved        └──day passes without arrival──► NoShow
```

| Step | Role | Permission | Enforced by | Notes |
|---|---|---|---|---|
| Request | Receptionist, Doctor, Records | `appointment.create` | Policy | Slot taken from the clinician's weekly schedule |
| Confirm | Receptionist, Doctor | `appointment.manage` | Policy | — |
| Check in | Receptionist, Nurse | `appointment.checkin` | Policy + Rule | Opens an encounter in the same request |
| Cancel | Receptionist, Doctor | `appointment.manage` | Policy + Rule | Releases the slot |
| No-show | Receptionist, Doctor | `appointment.manage` | Rule | Only for a past confirmed day |
| Reschedule | Receptionist, Doctor | `appointment.manage` | Rule | Re-runs the conflict check |

**Conflict prevention is layered**
1. `SlotRules.AvailableSlots` removes taken offsets before the slot is offered.
2. `SlotRules.Overlaps` rejects an overlapping interval server-side.
3. A unique index on `(DoctorID, AppointmentDate, SlotMinutes)` rejects a duplicate at write time.

Only `Cancelled` and `NotApproved` release a slot. `NoShow` deliberately keeps it, because the
clinician's time was still consumed. Covered by `SlotRulesTests`.

**Check-in cannot be faked**: `SlotRules.CanCheckIn` requires `Approved` **and** the appointment day
to equal the clinic day. A pending request or a future appointment is rejected with a message.

---

## 3. Clinical encounter

```
Registered ──triage──► Triaged ──start──► InConsultation ──complete──► Completed ──sign──► Signed
     │                    │                     │                          │
     └────────────────────┴─────────────────────┴──────► Cancelled ◄────────┘
```

| Step | Role | Permission | Enforced by |
|---|---|---|---|
| Open encounter | Receptionist (via check-in), Doctor | `encounter.create` | Policy |
| Record vitals | Nurse, Doctor | `triage.record` | Policy |
| Add note (draft) | Doctor, Nurse | `encounter.document` | Policy |
| Sign note | Doctor | `encounter.sign` | Policy + Rule |
| Add diagnosis | Doctor | `encounter.document` | Policy |
| Complete | Doctor | `encounter.document` | Rule |
| Sign encounter | Doctor | `encounter.sign` | Policy + Rule |

**Immutability rules**
- `EncounterRules.IsEditable` is false for `Completed`, `Signed` and `Cancelled`. Signed encounters
  reject every write, including new orders.
- `EncounterRules.CanSign` requires a diagnosis **and** a signed note; an empty encounter cannot be
  closed.
- A signed note is immutable; a correction creates a new note carrying `AmendsNoteId` and a visible
  amendment badge rather than overwriting the original.

---

## 4. Laboratory

```
Requested → AwaitingCollection → Collected → Received → Processing
          → AwaitingVerification → Released        (any non-terminal state → Cancelled)
```

| Step | Role | Permission | Enforced by |
|---|---|---|---|
| Order | Doctor | `lab.order` | Policy |
| Collect specimen | Nurse, Lab | `lab.collect` | Policy |
| Receive | Lab | `lab.collect` | Policy |
| Enter result | Lab | `lab.result.enter` | Policy |
| Submit for verification | Lab | `lab.result.verify` | Rule |
| Verify and release | Lab | `lab.result.release` | Policy + Rule |

**The release gate** is the safety-critical property: `LabRules.IsClinicallyFinal` is true only for
`Released`. `LabRules.CanTransition` has no path from `Processing` straight to `Released`, so an
entered value cannot skip verification. Result flags (`Low`/`High`/`Critical`) are computed from
the configured reference range only — the system never writes an interpretation.

---

## 5. Imaging

```
Requested → Scheduled → Performed → Reported → Verified → Released
```

| Step | Role | Permission |
|---|---|---|
| Request | Doctor | `imaging.order` |
| Schedule / mark performed | Radiology, Admin | `imaging.manage` |
| Write findings and impression | Radiology | `imaging.report` |
| Verify | Radiology | `imaging.verify` |
| Release | Radiology | `imaging.release` |

A report cannot be written before the study is performed, and a released report is immutable. The
verifier is stored separately from the author (`RadiologistUserId` vs `VerifiedByUserId`) so a
self-verified single-radiologist report is visibly self-verified rather than appearing
independently reviewed.

---

## 6. Pharmacy

```
Pending → Reviewed ──► PartiallyDispensed ──► Dispensed
   └────► Rejected
```

| Step | Role | Permission |
|---|---|---|
| Prescribe | Doctor | `prescription.create` |
| Review (approve / reject) | Pharmacist | `prescription.review` |
| Dispense | Pharmacist | `prescription.dispense` |

Dispensing is blocked until a pharmacist has approved. Stock is deducted per line with the batch
recorded, and the quantity cannot exceed the outstanding amount. A prescription is only ever
created from a clinician's order on a live encounter — there is no path in the application that
manufactures a prescription.

---

## 7. Admission and bed management

```
Requested ──assign bed──► Admitted ──discharge──► Discharged
     └──────────────────► Cancelled
```

| Step | Role | Permission | Enforced by |
|---|---|---|---|
| Request admission | Admin, Doctor | `admission.manage` | Policy |
| Assign / transfer bed | Nurse, Admin | `bed.manage` | Policy + Rule + **DB** |
| Discharge | Admin | `discharge.approve` | Policy + Rule |

**Double-bed occupancy is prevented in the database**, not only in code: `BedAssignments` carries a
filtered unique index on `BedId` where `ReleasedAtUtc IS NULL`. Two concurrent requests that both
read "free" cannot both commit; the service maps the constraint violation to a business error.
`BedRulesTests` covers the in-process guard.

---

## 8. Billing and refunds

| Step | Role | Permission | Enforced by |
|---|---|---|---|
| View invoice | Billing, Management | `billing.view` | Policy |
| Issue invoice | Billing | `billing.manage` | Policy |
| Record payment | Billing | `billing.manage` | Rule (`IsPaymentValid`) |
| Record refund | Billing | `billing.manage` | Rule + `billing.refund` for approval |
| Approve refund ≥ threshold | Billing (second officer) | `billing.refund` | Rule + Audit |

`BillingRules.IsPaymentValid` rejects overpayment and non-positive amounts, so the ledger cannot go
negative. Refunds at or above the threshold are recorded with `RequiresApproval = true` and stay
pending until a **different** officer approves them.

---

## 9. Reporting and export

| Step | Role | Permission |
|---|---|---|
| Browse the catalogue | any role with `report.view` | filtered per report by `ReportDefinition.Permission` |
| Run a report | as above | live query, filter + paging |
| Export CSV | Records, Billing, Management, Admin | `report.export` |
| Print | as above | print stylesheet, no data egress |

Every export writes a `report.export` audit row. Exports are capped at 200 rows per request to
prevent a single call from extracting the whole dataset.

---

## 10. Cross-cutting controls

| Control | Implementation |
|---|---|
| Session | Identity cookie, sliding expiry, `HttpOnly`, `Secure` when HTTPS |
| CSRF | `[ValidateAntiForgeryToken]` on every state-changing action; token also exposed as a meta tag for fetch calls |
| Uploads | Signature-checked, stored outside the web root, extension and size limited |
| Rate limiting | Fixed-window limiter on authentication endpoints, `429` with a ProblemDetails body |
| Audit | Only metadata: event type, actor, subject, resource type and id, timestamp. No clinical text. |
| Transport | HTTPS redirection plus HSTS in non-development environments |
| Error handling | No stack traces or SQL text in responses; the error page exposes only a trace identifier |
