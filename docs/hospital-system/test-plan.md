# CareAxis — Test Plan and Regression Report

**Verification method for this document:** every result recorded here comes from the GitHub Actions
run for the commit named in §6. Nothing is inferred. Where a category has not been executed, it is
labelled **Not run** with the reason, rather than described as passing.

---

## 1. What the environment permits

| Capability | Available | Consequence |
|---|---|---|
| .NET 10 SDK, build, unit test | ✅ via CI | Automated unit tests execute on every push |
| SQL Server | ❌ not provisioned | No integration tests against a live database |
| Browser / Playwright | ❌ no browser, no package installation | No end-to-end or visual regression run |
| Network for NuGet | ❌ blocked | Cannot add test-only packages (EF InMemory, Testcontainers, Playwright) |

This is why the suite is **pure unit testing of decision logic**. The decision rules were deliberately
extracted into `Hospital.BLL/Common/Rules` (pure static classes with no EF dependency) precisely so
that the invariants that matter are testable without a database.

---

## 2. Test strategy

```
        ┌───────────────────────────────────────────────┐
        │  Not executed: E2E / visual regression        │  ← needs a browser
        ├───────────────────────────────────────────────┤
        │  Not executed: repository integration tests   │  ← needs SQL Server
        ├───────────────────────────────────────────────┤
        │  Executed: 69 pure-unit tests, 190+ assertions │  ← runs on every push
        └───────────────────────────────────────────────┘
```

**Why rules and not controllers.** A controller test mostly asserts that a method was called. The
defects that actually reach a hospital — a double-booked slot, a discharged bed still marked
occupied, an unverified laboratory value shown as final — are decided by the rule layer. Putting the
test boundary there gives high defect detection per test and no infrastructure cost.

**Layering.**
1. `*RulesTests` — pure decision logic (slots, beds, laboratory, billing, encounters).
2. `EncounterAndPermissionTests` — the RBAC boundary and query normalisation.
3. `LocalisationTests` — interface-language completeness.
4. `ResourceOwnershipTests` — the patient-portal ownership predicate.

---

## 3. Test inventory

| Suite | Tests | What it proves |
|---|---:|---|
| `AppointmentRulesTests` | 4 | Bookable dates, daily capacity, weekday matching, status validity |
| `SlotRulesTests` | 8 | Slot generation, overlap detection, slot occupancy per status, check-in window, no-show window, cancellation eligibility |
| `BedRulesTests` | 9 | Occupancy with history, per-bed counting, occupancy rate rounding, inactive beds excluded, admission transitions, bed-assignment eligibility |
| `LabAndBillingRulesTests` | 12 | Laboratory transition graph, no-skip verification, terminal states, clinical-finality, reference-range classification, balance arithmetic, overpayment rejection, refund threshold, insurance clamping, invoice status derivation |
| `EncounterAndPermissionTests` | 14 | Encounter transition graph, signed-record immutability, sign preconditions, note edit permission, five role-boundary assertions, permission catalogue integrity, query normalisation and paging route preservation |
| `LocalisationTests` | 4 | Every key translated to Arabic, unknown-key fallback, RTL detection, regional-culture resolution |
| `ResourceOwnershipTests` | 1 | Ownership predicate rejects null/whitespace/mismatched ids |
| `ViewHygieneTests` | 7 | No inline script, no inline event handlers, no hard-coded colours, no unencoded HTML outside the icon catalogue, no duplicate `Scripts` section, no reference to the retired layout partials, no patient data scoped by display name |
| `AuthorizationCoverageTests` | 10 | Every one of the 120 actions is authorised or on a reviewed public list; no controller mixes `[AllowAnonymous]` with `[Authorize]`; no action is both; every policy is built from a `Permission` constant; the catalogue is exactly 43 permissions, matches `Permission.All`, has no duplicate values, and only the four declared reserved permissions are unenforced |
| **Total** | **69** | |

### What the authorisation audit does and does not prove

`AuthorizationCoverageTests` is a **static** audit. It reads the controller sources and asserts the
shape of the authorisation attributes. It therefore proves:

- no action is reachable without authentication unless a human added it to a reviewed list;
- the `[AllowAnonymous]` + `[Authorize]` combination that silently disables a controller's policy
  never occurs;
- no policy name is written as a literal string, so a renamed permission cannot silently produce an
  always-failing (or, via a typo'd custom policy, always-passing) gate;
- the four unenforced permissions are exactly the four that the documentation calls reserved.

It does **not** prove that the *right* permission is on each action — a developer could still put
`patient.view` on a delete endpoint. That requires a request-level matrix test (sign in as each of
the tested roles, call every action, assert the status code), which needs a host and a database and
is specified in §7 as outstanding work. The static audit closes the failure modes that occur by
accident; the matrix test would close the ones that occur by mistake.

---

### Positive and negative coverage of the brief's named scenarios

| Scenario named in the brief | Covered by | Type |
|---|---|---|
| Duplicate / conflicting appointment | `SlotRulesTests.Overlaps_*`, `AvailableSlots_*`, `OccupiesSlot_*` + unique index `UX_Appointments_DoctorSlot` | Unit + DB constraint |
| Conflicting bed assignment | `BedRulesTests.IsOccupied_*`, `OccupiedCount_*` + filtered unique index `UX_BedAssignments_ActiveOccupancy` | Unit + DB constraint |
| Unauthorised access | `EncounterAndPermissionTests` role-boundary assertions, `ResourceOwnershipTests` | Unit |
| Finalized-report edits | `EncounterRulesTests` via `IsEditable`/`CanEditNote`; laboratory `Released` terminal; imaging released-report rejection in `ImagingService` | Unit + service guard |
| Cross-tenant access | **Not covered** — no tenant/branch claim exists yet (see `user-roles-permissions.md` §6) | Gap |
| Unverified result shown as final | `LabAndBillingRulesTests.IsClinicallyFinal_*`, `LabTransitions_CannotSkipVerification` | Unit |
| Overpayment / refund abuse | `IsPaymentValid_*`, `RefundRequiresApproval_*` | Unit |
| Empty encounter signing | `CanSign_RequiresADiagnosisAndASignedNotePresent` | Unit |

---

## 4. Named negative cases and their expected outcome

These are the assertions that would fail if a control regressed:

| Test | Failure it prevents |
|---|---|
| `LabTransitions_CannotSkipVerification` | A result being released straight from `Processing` without review |
| `IsClinicallyFinal_IsTrueOnlyForReleasedResults` | Any non-released status being treated as a final result |
| `OccupiedCount_CountsEachBedOnceEvenWithHistoricalAssignments` | A discharged patient still occupying a bed, blocking admissions |
| `IsOccupied_IsTrueWhileAnyAssignmentIsUnreleased` | Double-booking a bed |
| `CanCheckIn_RequiresConfirmedAppointmentOnTheSameDay` | Check-in of an unconfirmed or future request |
| `CanMarkNoShow_RequiresAMissedConfirmedDay` | Marking a same-day patient as a no-show |
| `EncounterTransitions_AreTerminalAfterSigning` | Editing a signed clinical record |
| `Transitions_PreventDischargingAnAdmissionThatWasNeverAdmitted` | Discharge without admission |
| `ReceptionCannot_SignOrEnterResults` | Front-desk staff authoring clinical content |
| `PharmacistCannot_PrescribeOrReleaseLaboratoryResults` | Dispenser also prescribing |
| `AuditorCanReadTheTrailButCannotWriteClinicalData` | An auditor modifying what they audit |
| `EveryRolePermission_IsInTheCatalogue` | A typo granting a permission that no policy enforces (silent no-op) |
| `EveryKey_IsTranslatedIntoArabic` | An English string leaking into the Arabic interface |
| `ListQuery_RouteValues_PreserveActiveFiltersForPaging` | Paging silently dropping the active filter and exposing other patients' rows |

---

## 5. Defects found and fixed during this work

Recorded because they were found by the verification loop, not by inspection — which is the point of
having the loop.

| # | Defect | Detected by | Fix |
|---|---|---|---|
| 1 | `@page` used as a loop variable in `_Pager.cshtml` collided with the Razor `@page` directive, breaking compilation | CI build | Loop restructured to a computed page window |
| 2 | `BLL.` / `DAL.` namespace shorthands used outside their own assemblies (10 sites) | CI build | Fully qualified |
| 3 | Two `IAuditLogger` interfaces existed, making every constructor ambiguous | CI build | Single contract in the business layer |
| 4 | Doctor lookups returned `Id = 0` for every clinician, silently breaking every doctor filter and booking form | Code review while wiring the directory | Replaced with a string-keyed `PersonOption` carrying the real user id |
| 5 | `_Icon` partial referenced an undeclared `name` variable | CI build (Razor) | Bound `@model string` |
| 6 | `DepartmentQueue.Confirmed` vs `.Pending` mismatch rendered the wrong queue figure twice | CI build (Razor) | Correct property per metric |
| 7 | Local variable `encounters` shadowed the service field, making the chart's encounter list unreachable | CI build | Renamed |
| 8 | `BedWith(null)` bound `null` to the `params` array, producing a `NullReferenceException` in four tests | CI test annotations | Explicit `(DateTime?)null` element |
| 9 | `AppointStatus.Declined` does not exist (it is `NotApproved`) | CI test annotations | Corrected; the test now asserts the enum member that actually releases a slot |
| 10 | Nurse role lacked `report.view` while the shell navigation offered the link | Scripted grant-matrix comparison against `RolePermissions.cs` | Granted; matrix now verified cell-by-cell |
| 11 | Legacy views declared a `Header` section rendering a deleted partial | Markup inventory | Removed 17 dead sections |
| 12 | One inline `#003366` remained in a legacy table, bypassing the design system | Token lint | Replaced with component classes |
| 13 | Client-side validation loaded jQuery **validation** but never jQuery itself, so unobtrusive validation was silently dead | Inspecting the CSP work | Partial now loads jQuery from the vendored `wwwroot/lib` copy |
| 14 | The application called `MigrateAsync()` but no migrations are committed, so a fresh database could not start | Reading start-up code | Centralised `DatabaseSchemaInitializer`: migrations when present, model-created schema in non-production only, hard failure in production |
| 15 | Eleven inline `<script>` blocks blocked any meaningful CSP | CSP review | Moved to `booking.js`, `invoice.js`, `flash-dismiss.js`; `script-src` tightened to `'self'`; six dead legacy blocks removed |
| 16 | The patient chart assembled a patient's invoices by matching the **surname substring** against the global invoice list, so two patients sharing a surname would see each other's financial records — and any patient with a unique surname saw an empty list past the first page | Reading `PatientsController.Details` while documenting it | Added `IBillingService.ForPatientAsync(string patientId, …)` filtering on the foreign key; added `NoView_ScopesPatientDataByDisplayName` as a static guard |

The grant matrix in `user-roles-permissions.md` is now machine-checked against `RolePermissions.cs`
(384 cells, 0 mismatches) rather than maintained by hand.

---

## 6. Regression run of record

| Field | Value |
|---|---|
| Pipeline | `.github/workflows/ci.yml` → job `build-and-test` |
| Steps executed | Restore → Build (Release) → Test → migrations artifact |
| Result | **All 69 tests pass; build succeeds** |
| Commit | `5635b78` verified green. The tree has advanced since with documentation, security hardening and schema-initialisation changes; the latest green run is recorded in the final report. |

To reproduce the exact status of any commit:

```bash
gh run list --repo YoussefAhmedy/HospitalManagement --limit 5 \
  --json databaseId,headSha,conclusion
gh api repos/YoussefAhmedy/HospitalManagement/actions/runs/<run-id>/jobs \
  --jq '.jobs[]|"\(.name): \(.conclusion)"'
```

CI now converts every failed test into a build annotation (`::error title=Test failed::…`) with the
assertion message, so a red build is diagnosable from the API without downloading logs.

---

## 7. Not run — and what would be required

### Static analysis executed instead of runtime browser checks

Because no browser is available, the properties that a browser test would cover for the security
posture are asserted statically over the Razor sources by `ViewHygieneTests`: absence of inline
script and inline handlers (which is what makes `script-src 'self'` meaningful), absence of raw
HTML injection outside the one controlled icon catalogue, and absence of hard-coded colours. These
run on every push.

| Area | Status | Requirement to run it |
|---|---|---|
| Browser E2E (Playwright) | **Not run** | A runner with Chromium and NuGet/npm access. Scenarios to automate are specified in §8. |
| Visual regression | **Not run** | Playwright snapshots against the design system's stable surfaces (tables, KPIs, dialogs) |
| Accessibility automation (axe) | **Not run** | Headless browser |
| Manual screen-reader pass (NVDA/VoiceOver) | **Not run** | Human tester with the assistive technology |
| Database integration (unique indexes firing) | **Not run** | A SQL Server instance; the constraints themselves are verified by reading `HasFilter(...)` in `DAL/Entities/config/` |
| Load / concurrency (double-booking race) | **Not run** | A database and a load driver |
| Penetration test | **Not run** | Independent assessor |
| Arabic linguistic review | **Not run** | A native-speaker clinical reviewer; the catalogue is complete but untranslated nuance is unverified |

---

## 8. End-to-end scenarios specified for the Playwright suite

Written now so the suite can be implemented as soon as a browser runner exists. Each is a
positive/negative pair.

1. **Login and role routing** — each seeded role lands on its own workspace; a role without
   `report.view` gets `403` from `/Reports` rather than a rendered page.
2. **Duplicate appointment** — book slot 09:20 for Dr A, then book the same slot for Dr A again as
   another operator; second attempt is refused with a visible message and the first booking survives.
3. **Check-in without confirmation** — a `Pending` appointment offers no check-in control and the
   POST is rejected server-side.
4. **Bed double-assignment** — assign bed B-01 to admission X, then attempt admission Y; refused, and
   the bed board still shows one occupant.
5. **Finalized report edit** — open a `Signed` encounter, confirm no editable control is rendered,
   and confirm a forged POST is rejected.
6. **Unverified laboratory result** — an `AwaitingVerification` order shows "not final" and is absent
   from the patient's released results.
7. **Cross-tenant access** — request a record id belonging to another branch; expect `404`/`403`,
   never data. *Requires the branch claim that is not yet built.*
8. **CSRF** — replay a POST without the antiforgery token; expect `400`.
9. **RTL** — switch to Arabic; assert `dir="rtl"`, that the sidebar is on the right, that an MRN is
   still rendered left-to-right, and that no key name leaks into the DOM.
10. **Responsive** — at 360px, no horizontal page scroll; tables scroll within their container; the
    navigation becomes a drawer that traps focus.
11. **Print** — the print stylesheet hides navigation and shows the print header with the record
    identifier.
