# CareAxis — Healthcare Standards Mapping

**Reference:** HL7 FHIR R5 (`https://hl7.org/fhir/`). **This is a design-alignment assessment, not a
conformance statement.** CareAxis is not a FHIR server and does not claim FHIR conformance.

---

## 1. Position

The platform models the same clinical concepts FHIR models, but with a bespoke relational schema
optimised for this application's workflows. That is a deliberate, defensible choice for an internal
hospital system. It becomes a liability the moment the hospital needs to exchange data with another
system, a national health information exchange, or a payer.

| Question | Answer |
|---|---|
| Is CareAxis a FHIR server? | **No.** There is no `/fhir` endpoint and no capability statement. |
| Does the data model align with FHIR resources? | **Partially** — see the mapping table. |
| Can data be exported in FHIR format? | **Not currently.** The mapping below is the design work required, not a shipped feature. |
| Does the UI use FHIR terminology? | No — it uses clinical language appropriate to each role. |
| Are codes standardised (SNOMED / LOINC / ICD)? | **No.** Diagnosis has an optional free-text `Code` field; laboratory tests have a local `Code`. No terminology server is integrated. |

---

## 2. Resource mapping

| CareAxis entity | Closest FHIR R5 resource | Alignment | Gap |
|---|---|---|---|
| `Patient` (derives `ApplicationUser`) | `Patient` | Identity, name, DOB, gender, contact, MRN as identifier | Address is a single string, not `Address`; no `telecom` array; no `generalPractitioner` |
| `Encounter` | `Encounter` | Class, status, period, subject, participant, department as location | No `serviceType`/`priority` coding; no episode-of-care link |
| `VitalSign` | `Observation` (vital-signs profile) | Value, unit, effective time, subject | Each vital is a column group rather than one `Observation` per code; no UCUM unit coding |
| `ClinicalNote` | `DocumentReference` or `Composition` | Author, status (draft/signed), timestamp, amendment link | Not a structured `Composition`; no attachment/`Binary` |
| `Diagnosis` | `Condition` | Code, description, category | No coding system; no clinical/verification status ladder beyond type |
| `LabOrder` + `LabOrderItem` | `ServiceRequest` + `Observation` + `Specimen` | Status workflow maps closely; specimen identifier present | One order carries multiple items; no `Specimen` resource, no `DiagnosticReport` grouping |
| `LabTest` | `ObservationDefinition` / `ActivityDefinition` | Reference ranges, unit, category | No LOINC coding |
| `ImagingOrder` | `ServiceRequest` (imaging) + `DiagnosticReport` | Status ladder, findings/impression, performer, verifier | No `ImagingStudy`/DICOM linkage; images are not stored |
| `Prescription` + `PrescriptionItem` | `MedicationRequest` | Medication, dose, frequency, duration, route, status | No `Medication` coding (RxNorm/ATC); no `dosageInstruction` structure |
| `Medication`, `StockBatch` | `Medication` + `InventoryItem` | Name, strength, form, batch, expiry | No coding; inventory is not a FHIR priority |
| `Admission`, `BedAssignment`, `Ward`, `Room`, `Bed` | `Encounter` (inpatient) + `Location` | Occupancy invariant, ward/room/bed hierarchy | `Location` is implicit in the ward tree, not a resource |
| `Invoice`, `InvoiceLine`, `Payment` | `Claim` / `Account` / `ChargeItem` | Ledger arithmetic, insurance coverage amount | No `Coverage`/`ClaimResponse` exchange; no adjudication |
| `InsurancePolicy` | `Coverage` | Payer, member, coverage percent | Not exchanged with a payer |
| `AuditLog` | `AuditEvent` | Actor, action, entity, recorded time | No `AuditEvent` coding; no `agent`/`source` structure |
| `Organization` (`Branch`, `Department`) | `Organization` / `Location` | Hierarchy present | Not coded |
| `Practitioner` (`Doctor`) | `Practitioner` + `PractitionerRole` | Identity, specialisation | No role coding; no `qualification` |

---

## 3. Semantics that already match FHIR's safety intent

These are the points where the implementation independently arrived at the same conclusions FHIR
encodes, which is a good sign for future interoperability work:

| FHIR principle | CareAxis implementation |
|---|---|
| `Observation.status` distinguishes `preliminary` from `final` | `LabRules.IsClinicallyFinal` — only `Released` is final |
| `Observation.status = amended` with a new version | Signed notes are immutable; corrections create a new note with `AmendsNoteId` |
| `DiagnosticReport.status` ladder | Imaging: reported → verified → released, with author and verifier recorded separately |
| `ServiceRequest.status` lifecycle | Laboratory transitions are an explicit graph with no skipping |
| `Provenance` / `AuditEvent` for every change | `AuditLog` metadata on every sensitive action, append-only |
| `Encounter` must be closed before amendment | Signed encounter rejects edits |
| Subject-of-care scoping | Portal reads limited to the owner; staff reads gated by permission |

---

## 4. What full FHIR alignment would require

Scoped honestly, because this is a large programme and should not be started by accident:

1. **Terminology.** LOINC for laboratory tests, SNOMED CT for diagnoses and findings, UCUM for units,
   RxNorm or ATC for medications, and a terminology cache with version pinning.
2. **Resource restructuring.** Split the grouped vitals into one `Observation` per measurement;
   introduce `Specimen`, `DiagnosticReport` and `ImagingStudy`.
3. **Identity.** `Patient.identifier` with a system URI derived from the MRN; `Practitioner`
   registration numbers.
4. **API surface.** A FHIR facade over the existing services, read-only first, with SMART-on-FHIR
   scopes mapping to the existing permission catalogue — the permission model already maps cleanly
   onto scopes.
5. **Exchange.** `Bundle` assembly, `CapabilityStatement`, and either a RESTful server or an
   asynchronous exchange channel.

**Not in scope for this transformation:** any of the above. Building a half-correct FHIR facade is
worse than having none, because downstream systems would trust it.

---

## 5. Accreditations and certifications

**None held and none claimed.** Specifically:

| Framework | Position |
|---|---|
| FHIR R5 conformance | **Not conformant.** Design alignment only, as above. |
| HL7 v2 messaging | Not implemented. |
| DICOM / PACS | Not implemented. Radiology stores report text and study metadata only. |
| ICD-10 / ICD-11 coding | Not implemented. Free-text codes only. |
| HIPAA | **No compliance claim.** Requires BAAs, risk analysis, workforce training and audit procedures outside this repository. |
| GDPR | **No compliance claim.** Requires a lawful basis, records of processing, DPIA and subject-rights procedures. |
| ISO 27001 / ISO 13485 | Not certified. |
| MDR / FDA device classification | **Not assessed.** A system that influences clinical decisions may be a regulated medical device in some jurisdictions. Legal and regulatory review is required before commercial sale. |
| Clinical safety standard (e.g. DCB0129 / DCB0160 in England) | Not performed. A clinical risk assessment by a qualified clinician is a prerequisite for live use. |

The distinction the brief asked for is preserved throughout: this is **implemented software with a
documented safety model**, not certified software.
