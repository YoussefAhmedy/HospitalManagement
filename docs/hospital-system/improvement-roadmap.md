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
- Domain tests, EF model test, authorization tests, workflow state-machine tests, report query tests, negative scenarios.
- Playwright/E2E and visual regression: **cannot execute in this environment**; supplied as configured work for the deployment phase, explicitly marked not-run.
- **Gate:** CI test summary recorded verbatim.

## Phase 10 — Commercial readiness review
- `commercial-readiness-assessment.md`: implemented vs simulated vs requiring certification, with deployment, privacy, interoperability and legal gaps enumerated.

## Delivery constraints of this execution environment (disclosed up front)
| Constraint | Consequence |
|---|---|
| No .NET SDK / NuGet / SQL Server / Docker in sandbox | Code cannot be compiled or run locally; GitHub Actions is the compile/test authority |
| No browser automation runtime | E2E and visual regression cannot be executed; provided as deployment-phase configuration |
| No PDF engine | Server-side PDF out of scope; print-to-PDF path delivered |
| Only GitHub/npm/PyPI egress | No NuGet package additions possible — implementation is constrained to **already-pinned dependencies** (deliberate, keeps CI green and avoids unverifiable packages) |
