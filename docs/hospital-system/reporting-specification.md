# CareAxis — Reporting Specification

**Basis:** the catalogue implemented in `Hospital.BLL/Services/Implementation/ReportService.cs`.
Every report listed here exists, is reachable from `/Reports`, and is produced by a live query over
the operational tables. No report contains fabricated figures, placeholder rows or sample values.

---

## 1. Contract shared by every report

| Property | Behaviour |
|---|---|
| Definition | `ReportDescriptor(Key, Title, Description, Permission)` |
| Authorisation | The catalogue is filtered per principal by `report.view` / `report.financial`; running a report re-checks the descriptor's permission server-side and rejects a forged key |
| Filters | `ReportFilter { From, To, Status, Term, Page, PageSize }` — a date range defaulting to the last 30 days, an optional status, an optional free-text term |
| Paging | Server-side, default 50 rows, maximum 200 |
| Outputs | On-screen table, print layout, CSV export — all rendered from the same `ReportResult` |
| Navigation | Pager preserves every active filter through `ToRouteValues()` (unit-tested) |
| Timestamps | Rendered in UTC with an explicit `UTC` marker on the report header |
| Empty result | An explicit empty state, never a blank table with a broken footer |
| Export cap | 200 rows per export request, regardless of page size, to bound extraction |
| Export audit | Every CSV export writes a `report.export` event with the actor and report key |
| Money | Formatted from a single `Money.Format` helper (en-US, USD) so reports, invoices and receipts agree |
| Numbers | Tabular figures, right-aligned; identifiers in mono |

### CSV shape

```
Report,<title>
Generated,<ISO-8601 UTC>
Filters,<human-readable filter summary>

<column headers…>
<rows…>

<summary key>,<summary value>
```

Fields are quoted and escaped; a value containing a comma, quote or newline cannot break the record
boundary.

---

## 2. Catalogue

### 2.1 Patient and access

| Key | Title | Question it answers | Permission |
|---|---|---|---|
| `patient-visits` | Patient visit history | Who came, when, to which clinician, and what happened? | `report.view` |
| `appointments-status` | Appointment outcomes | How many were booked, attended, cancelled or missed? | `report.view` |
| `no-shows` | No-show register | Which confirmed appointments were not attended? | `report.view` |

### 2.2 Clinical activity

| Key | Title | Question it answers | Permission |
|---|---|---|---|
| `encounters` | Clinical encounters | Volume and duration of encounters in the period | `report.view` |
| `department-workload` | Department workload | Which departments are carrying the load? | `report.view` |
| `prescriptions` | Prescription register | Prescribing volume by medication | `report.view` |

### 2.3 Diagnostics

| Key | Title | Question it answers | Permission |
|---|---|---|---|
| `lab-throughput` | Laboratory throughput | Orders by state with turnaround | `report.view` |
| `lab-pending` | Pending laboratory results | What is stuck before release? | `report.view` |
| `lab-tests` | Tests requested | Volume by test and abnormal rate | `report.view` |
| `lab-critical` | Critical results | Which values are outside critical limits and need action? | `report.view` |

### 2.4 Inpatient operations

| Key | Title | Question it answers | Permission |
|---|---|---|---|
| `admissions` | Admissions and discharges | Inpatient flow for the period | `report.view` |
| `bed-occupancy` | Bed occupancy | Live occupancy by ward and room | `report.view` |

### 2.5 Financial

| Key | Title | Question it answers | Permission |
|---|---|---|---|
| `revenue` | Revenue by period | Approved payments less refunds, by day | `report.financial` |
| `revenue-department` | Revenue by department | Where income is attributed | `report.financial` |
| `outstanding` | Outstanding invoices | Who owes what | `report.financial` |
| `payments` | Payments ledger | Every payment and refund recorded | `report.financial` |

### 2.6 Inventory

| Key | Title | Question it answers | Permission |
|---|---|---|---|
| `inventory` | Medication inventory | Stock on hand, value and reorder state | `report.view` |
| `expiry` | Expiring stock | Which batches expire within the horizon? | `report.view` |

---

## 3. Interpretation rules

These are deliberate and matter clinically:

| Rule | Reason |
|---|---|
| `lab-critical` selects on the stored `ResultFlag`, which is derived from the configured reference range | The system never invents an interpretation; the flag is a calculation over the range the laboratory configured |
| Unreleased laboratory orders never count as results | A result that has not passed verification is not a result (see `LabRules.IsClinicallyFinal`) |
| Revenue counts **approved** payments, net of refunds | A pending refund is not money returned until it is approved |
| Bed occupancy counts assignments with no release timestamp | Matches the database invariant that prevents double occupancy |
| No-show requires a *past, confirmed* appointment | An unresolved same-day booking is not a miss |
| Encounter "signed" is reported separately from "completed" | A completed but unsigned note is an outstanding clinical obligation |

---

## 4. Charts

Chart data is computed from the same queries as the tables; there is no separate chart data source
that could drift. Where a chart and a table answer the same question they must agree, and they do
because one function backs both.

Chart rules:
- No decorative 3D, no gradients in data marks, no animated re-draw.
- Series colour comes from the token palette and is stable across reports (a status keeps its colour
  everywhere in the product).
- Every chart has an accessible table equivalent; the chart is never the only way to read a value.
- Axes start at zero for counts and money.

---

## 5. Print

The print stylesheet:

- hides navigation, filters, pager, toasts and the export controls;
- shows the `.print-header` with report title, filter summary and generation timestamp;
- forces table headers to repeat and avoids breaking a card across pages;
- renders badges as outlined text rather than coloured pills, so a monochrome printer preserves the
  distinction;
- prints the record identifier on every page so a printed page cannot be separated from its patient.

---

## 6. Not implemented

Stated plainly, because a reporting specification that overstates itself is worse than none:

| Capability | Status | Note |
|---|---|---|
| XLSX export | **Not implemented** | Only CSV and print. XLSX requires a package not currently available in the offline build environment. |
| PDF export | **Not implemented** | Print-to-PDF works through the browser; there is no server-side PDF generator. |
| Scheduled / emailed reports | **Not implemented** | The Hangfire infrastructure exists but no report schedule is configured. |
| Saved filter presets | **Not implemented** | Filters are URL parameters and can be bookmarked, but are not persisted per user. |
| Cohort builder / ad-hoc query | **Not implemented** | The catalogue is fixed. |
| Financial reconciliation to a payment gateway | **Not applicable** | No gateway is integrated; payments are recorded by an officer. |
| Report-level row security (e.g. department-scoped analyst) | **Not implemented** | Depends on the branch/department claim that is not yet built. |
