# CareAxis — Patient Examination Report

**Requirement:** a patient examination report that distinguishes *draft*, *reviewed*, *signed* and
*finalized* states.

This document specifies how that requirement is met by the implemented encounter and diagnostic
model, where the four states live, and what each one permits.

---

## 1. The four states, and where they exist

The brief's four labels do not map onto a single column, because a record's authority is
established at two levels: the **note** (what the clinician wrote) and the **encounter** (the visit
as a whole). Both are modelled explicitly.

### Note level — `ClinicalNote.Status`

| State in the brief | Model value | Permitted actions |
|---|---|---|
| **Draft** | `NoteStatus.Draft` | Author may edit the text; author may sign |
| **Reviewed** | A draft that has been read in the encounter timeline by another clinician; audit records `encounter.view` | Read-only for everyone until signed; no "reviewed" flag is fabricated |
| **Signed** | `NoteStatus.Signed` | Immutable. Corrections create a new note with `AmendsNoteId` |
| **Finalized** | A *signed* note inside a *signed* encounter | Immutable and legally closed; visible as a finalized report |

> **Deliberate design decision.** There is no stored "reviewed" flag. A checkbox that says "reviewed"
> without recording *who* reviewed it and *when* would be theatre. Review is evidenced in the audit
> trail (`encounter.view` with actor and timestamp) and in the countersignature of the signing
> clinician. If a formal second-reader workflow is required for a specific document class, it must be
> added as an explicit signature with its own identity — not simulated by a status value.

### Encounter level — `Encounter.Status`

| State | Meaning for the report |
|---|---|
| `Registered`, `Triaged`, `InConsultation` | The report is in progress; content is provisional |
| `Completed` | Documentation is finished; the report is **awaiting signature** |
| `Signed` | The report is **finalized**. `SignedAtUtc` and the signing clinician are stamped |
| `Cancelled` | The visit was cancelled; any partial content is retained but not a report |

---

## 2. State machine

```
                     ┌──────────── signed note ────────────┐
                     │                                     ▼
Registered ──► Triaged ──► InConsultation ──► Completed ──► Signed (finalized)
     │            │               │               │
     └────────────┴───────────────┴──────────────► Cancelled
```

Transitions are the explicit graph in `EncounterRules.Transitions`; a state cannot be skipped.
`EncounterRules.CanSign` refuses to finalize unless **both** a diagnosis and a signed note exist, so
an empty report cannot be closed.

`EncounterRules.IsEditable` is false for `Completed`, `Signed` and `Cancelled`, which means every
write path — notes, vitals, diagnoses, orders, prescriptions — is closed the moment the clinician
signs. This is enforced in the service layer and asserted in `EncounterAndPermissionTests`.

---

## 3. What a finalized report contains

| Section | Source | Immutability |
|---|---|---|
| Patient identity and MRN | `Patient` | Snapshot at view time; demographics are the live record |
| Visit metadata | `Encounter.Number`, department, clinician, timestamps | Frozen on signing |
| Vital signs | `VitalSign` rows, newest first, with recorded-at and recorder | Frozen on signing |
| Clinical notes | `ClinicalNote` where `Status = Signed`, plus any amendments | Immutable per note |
| Diagnoses | `Diagnosis` with type (primary/secondary/differential) | Frozen on signing |
| Laboratory results | Only orders with `Status = Released` | Immutable per result |
| Imaging reports | Only orders with `Status = Released`, findings and impression | Immutable per report |
| Prescriptions | `Prescription` with review and dispense state | Immutable after review |
| Signature block | Signing clinician, `SignedAtUtc`, encounter number | Written once |

**Results that are not released are excluded from a finalized report.** A finalized report cannot
show an unverified laboratory value as a result, because `LabRules.IsClinicallyFinal` admits only
`Released`, and the report queries filter on that state.

---

## 4. Draft and final in the interface

| Signal | Draft | Finalized |
|---|---|---|
| Banner | Neutral notice: "This record is editable and not final" | Success notice: "Signed and closed on <date> by <clinician>" |
| Status badge | `Draft` (muted) | `Signed` (success) |
| Edit controls | Rendered | Absent, and the POST endpoints reject the write regardless |
| Laboratory block | Shows "provisional — not released" where an unreleased order exists | Shows only released results |
| Print | Watermark "DRAFT — NOT A FINAL REPORT" | Print header with signature line and timestamp |

---

## 5. Where this is implemented

| Concern | File |
|---|---|
| Encounter transitions and editability | `Hospital.BLL/Common/Rules/EncounterRules.cs` |
| Note signing and amendments | `Hospital.BLL/Services/Implementation/EncounterService.cs` |
| Laboratory finality | `Hospital.BLL/Common/Rules/LabRules.cs` |
| Imaging finality | `Hospital.BLL/Services/Implementation/ImagingService.cs` |
| Screening and actions | `HospitalManagement/Controllers/EncountersController.cs` |
| Report rendering | `HospitalManagement/Views/Encounters/Details.cshtml` |
| Tests | `Hospital.Tests/EncounterAndPermissionTests.cs`, `LabAndBillingRulesTests.cs` |

---

## 6. Limits of this implementation

- There is **no separate signed PDF artefact**. A finalized report is a database state that renders
  to a printable page. A verifiable, digitally signed document (e.g. a signed PDF with a certificate)
  is **not** implemented and would be a backend work item.
- **No countersignature hierarchy.** Any clinician with `encounter.sign` may sign; there is no
  attending-versus-resident rule, no attending co-sign requirement.
- **No addendum after finalization beyond a new note.** Amendments are modelled at note level
  (`AmendsNoteId`); a formal encounter addendum with its own publication event is not implemented.
- **No clinician attestation statement.** The signature records identity and time; it does not
  capture a legal attestation sentence, which requires legal review per jurisdiction.
