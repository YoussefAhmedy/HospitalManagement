# CareAxis — Improvement Roadmap

Sequenced, dependency-aware, with explicit verification gates. Phases follow the brief's mandated order.

## Phase 1 — Current system audit ✅ complete
- `current-system-audit.md`, `module-inventory.md`, `technical-architecture.md`, this roadmap.
- **Gate:** every finding cites a file/line-level evidence area.

## Phase 2 — Product and workflow mapping ✅ complete
- `user-roles-permissions.md` (12 roles, permission matrix, least privilege).
- `workflow-mapping.md` (appointment, encounter, lab, pharmacy, admission, billing journeys with state machines).

## Phase 3 — UX architecture ✅ complete
- Application shell (fixed sidebar + topbar + command search + branch context), role-aware navigation, workspace hierarchy, breadcrumbs, queue-first home pages.
- **Gate:** every navigation item resolves to an implemented route; no dead links.

## Phase 4 — Design system ✅ complete
- `design-system.md` + `design-system.css`: colour tokens (navy/teal clinical), type scale, spacing, radius, elevation, controls, tables, badges, drawers, dialogs, toasts, charts, empty/error states, responsive breakpoints, print styles.
- **Gate:** all application pages consume the same tokens; no page-local hex colours.

## Phase 5 — Core functional improvements
1. Domain extension: encounters, vitals, notes/diagnoses, lab, radiology, pharmacy, admissions/beds, billing, branches/departments, service catalogue, numbering sequences.
2. Scheduling redesign: date + slot, conflict prevention, check-in queue, no-show.
3. Patient master data: MRN, allergies, search/paging, duplicate guard.
4. Role workspaces for doctor, nurse/triage, reception, laboratory, radiology, pharmacy, billing, records, management, auditor.
- **Gate:** compilation + EF model validation + domain tests in CI.

## Phase 6 — Reporting system
- Report catalogue (patients, operations, laboratory, financial, inventory) with date/branch/department/doctor/status filters, search, pagination, summary rows, CSV export, print layout, generation timestamp, permission gating.
- Patient examination report with draft/signed/finalized states.
- **Gate:** every report is backed by a real query; zero hard-coded chart values.

## Phase 7 — Security and permissions
- Permission catalogue + policy registration; `[Authorize(Policy=...)]` on every controller action; ownership handlers for patient data; audit coverage for reads of restricted records, exports, and finalizations; correlation ids.
- **Gate:** negative-test matrix (unauthorized access, cross-patient access, finalized-record mutation) executed in CI.

## Phase 8 — Responsive, accessible, bilingual refinement
- RTL stylesheet mirror, culture switching, Arabic resource files, locale dates/numbers, table overflow + detail drawers at mobile breakpoints, focus management in drawers/dialogs, reduced-motion, contrast audit.
- **Gate:** documented WCAG 2.2 AA checklist with per-item evidence or explicit gap.

## Phase 9 — Quality assurance
- **Delivered:** 69 unit tests across nine suites (slot conflicts, bed occupancy, laboratory release gate, billing ledger, encounter lifecycle, RBAC boundaries, localisation completeness, static view hygiene, authorisation coverage). Recorded in `test-plan.md` with the run of record.
- **Regression-hunting tooling:** CI now converts failed tests into annotated build errors, so a red build is diagnosable from the API without downloading logs. Sixteen defects were found this way and fixed, plus two more while reconciling documentation against code.
- **Grant-matrix verification:** the documented permission matrix is machine-compared against the seeded role map (384 cells, 0 mismatches), closing the gap where documentation silently drifts from code.
- **Not executed:** Playwright/E2E, visual regression, axe accessibility automation, database integration, load tests, penetration test. Each is listed with its blocking prerequisite in `test-plan.md` §7, and the E2E scenarios are specified in §8 so the suite can be built without further design work.
- **Gate:** CI test summary recorded verbatim. 

## Phase 10 — Commercial readiness review ✅

- `commercial-readiness-assessment.md`: implemented vs simulated vs requiring certification, with deployment, privacy, interoperability and legal gaps enumerated.
- **Delivered:** the five-way classification (fully implemented / demonstration-only / backend-required /
  external integration / legal review), the per-hospital versus multi-tenant decision with its reason
  and the cost of being wrong, readiness scoring per dimension, and a sequenced path to production.
- **Supporting deliverables also produced:**
  - `patient-examination-report.md` — draft/reviewed/signed/finalized, with the note-level and
    encounter-level state machines and an explicit statement that no signed PDF artefact exists.
  - `project-examination-report.md` — a reviewer's assessment including the weaknesses, not only the
    achievements.
  - `final-report.md` — the closing report, the verification record and the verdict.
  - `reporting-specification.md` — the 18-report catalogue and shared contract.
  - `healthcare-standards-mapping.md` — FHIR R5 alignment and an explicit non-conformance position.
- **Work surfaced by this phase and fixed rather than documented around:** the patient chart selected a
  patient's invoices by surname substring (a cross-patient disclosure); the demonstration password was
  a hard-coded constant presented as if it were a secret; four permissions were documented as controls
  while gating nothing.
- **Gate:** every claim in the assessment is traceable to a file, a test or an explicit gap statement.

## Delivery constraints of this execution environment (disclosed up front)
| Constraint | Consequence |
|---|---|
| No .NET SDK / NuGet / SQL Server / Docker in sandbox | Code cannot be compiled or run locally; GitHub Actions is the compile/test authority |
| No browser automation runtime | E2E and visual regression cannot be executed; provided as deployment-phase configuration |
| No PDF engine | Server-side PDF out of scope; print-to-PDF path delivered |
| Only GitHub/npm/PyPI egress | No NuGet package additions possible — implementation is constrained to **already-pinned dependencies** (deliberate, keeps CI green and avoids unverifiable packages) |
