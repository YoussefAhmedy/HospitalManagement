# CareAxis — Current System Audit

**Audit date:** 2026-11-22
**Audited commit:** `bf4c082` (`master`), working branch `arena/66f04dee-hospitalmanagement`
**Auditor role:** architecture / security / UX review of the existing repository
**Method:** full read-only inspection of every project, controller, entity, EF configuration, migration, Razor view, stylesheet, test, CI workflow and deployment file. No application code was modified before this document was written.

---

## 1. Executive summary

CareAxis is a **role-based ASP.NET Core MVC appointment-and-records demonstration**. It is materially better engineered than a typical student hospital project: it uses ASP.NET Core Identity with email confirmation, lockout and 12-character password policy; antiforgery on all unsafe verbs; CSP, HSTS, `no-store` on authenticated routes; rate limiting; owner-scoped queries; a minimal audit log that deliberately excludes clinical text; a non-root container; health endpoints; and a Hangfire background email queue with Data-Protection-encrypted payloads.

It is **not** a hospital management system. The clinical and administrative surface required by the brief — encounters, triage, vital signs, laboratory, radiology, pharmacy, admissions/beds, billing, insurance, reporting, multi-branch configuration, Arabic/RTL, and granular RBAC beyond three roles — **does not exist**. There is one appointment object with a date (no time slot), one free-text `MedicalRecord` row per visit, and no reporting engine of any kind.

The correct strategy is therefore **preserve and extend**, not rebuild: keep Identity, EF Core, MVC, the security middleware pipeline, audit logger, health checks and CI, and grow the domain and UI around them.

---

## 2. Verified technology stack

| Layer | Technology | Evidence |
|---|---|---|
| Runtime | .NET 10 / ASP.NET Core 10 / C# 14 | `global.json`, `Directory.Build.props` |
| Web | ASP.NET Core MVC (controllers + Razor views) | `HospitalManagement/Controllers`, `Views` |
| Data | EF Core 10 + **SQL Server only** | `Program.cs` `UseSqlServer`, `Hospital.DAL.csproj` |
| Identity | ASP.NET Core Identity, EF stores, default token providers | `Program.cs` |
| Background | Hangfire + SQL Server storage | `Program.cs`, `HangfireEmailQueue.cs` |
| Email | MailKit SMTP, queue-first, disabled by default | `EmailSender.cs`, `EmailOptions.cs` |
| Mapping | AutoMapper | `DomainProfile.cs` |
| Tests | MSTest (2 classes, 6 pure-logic tests) | `Hospital.Tests` |
| Packaging | Central package management + pinned EF tool | `Directory.Packages.props`, `.config/dotnet-tools.json` |
| Deploy | Multi-stage Dockerfile, compose, external SQL Server | `Dockerfile`, `compose.yaml` |
| CI | GitHub Actions: restore → build → test on .NET 10 | `.github/workflows/ci.yml` |

Layering is a conventional 3-project split: `HospitalManagement` (web) → `Hospital.BLL` (services, view models, mappers, helpers) → `DAL` (entities, EF config, repositories, migrations).

---

## 3. Verified feature inventory (what actually works)

### 3.1 Accounts and identity — strong
- Self-registration with email confirmation required before sign-in; resend confirmation.
- Sign-in with lockout (5 attempts / 15 min), 12-char complexity policy, unique email.
- Password recovery and reset with token flow; anti-enumeration responses.
- Optional Google OAuth, registered only when both client ID and secret exist.
- Profile view/edit with signature-validated image upload stored **outside** `wwwroot` and served through an authenticated endpoint.
- Verified email change with confirmation link.
- Role-seeded start-up; admin account only via an explicit `--bootstrap-admin` command with credentials from environment — no default admin, no checked-in password. This is genuinely good practice.
- `SafeLocalRedirect` used for return URLs (open-redirect protection).

### 3.2 Appointments — thin
- Patient requests an appointment for a **date only** (no time slot), against a doctor's weekly `Schedule` (`DayOfWeek` + `Shift`).
- Business rules in `AppointmentRules`: date must be after today; per-clinician **global** daily capacity of 50; requested weekday must match an active schedule row; status enum validated.
- Clinician approves / declines; patient cancels or edits a pending request.
- `RowVersion` concurrency token already added to `Appointment`.
- **No** availability computation, **no** time-of-day column, **no** double-booking protection, **no** check-in, **no** waiting queue, **no** no-show tracking.

### 3.3 Medical records — minimal
- A `MedicalRecord` = `Diagnosis` (500 chars), `Treatment` (500 chars), `RecordDate`, patient, doctor. That is the entire clinical record model.
- Clinician may add a record only where an approved appointment relationship exists (`ResourceOwnership` + repository checks).
- **No** encounter entity, vitals, notes lifecycle, signing, amendment, allergies, history, documents, or provenance.

### 3.4 Administration
- Dashboard counters (doctors, patients, appointment requests, upcoming, "approved today", medical records).
- Clinician account creation (emails credentials/confirmation), list, details, edit, delete (AJAX).
- Patient list, edit, details, delete by email.
- Medical-record inventory and record creation; schedule and shift creation.
- Lists are **unpaginated, unsearched, unsorted server-side**; actions are scattered across ad-hoc pages rather than a coherent workspace.

### 3.5 Infrastructure and security controls present
- Global `AutoValidateAntiforgeryToken`.
- CSP (`default-src 'self'`), `X-Frame-Options: DENY`, `nosniff`, `Referrer-Policy: no-referrer`, `Permissions-Policy`, HSTS in non-development.
- Session cookie: HttpOnly, `Secure`, `SameSite=Lax`, 8 h sliding; antiforgery cookie `SameSite=Strict`.
- Fixed-window rate limiters: authentication 10/5 min/IP, public search 60/min/IP.
- Forwarded headers restricted to configured known proxies, one hop.
- `/health/live`, `/health/ready` (DB check) with JSON writer; Hangfire dashboard read-only and admin-only at `/ops/jobs`.
- Audit logger records event type, actor, subject, resource type/id, UTC timestamp — **no clinical content** (deliberate and correct).

### 3.6 UI/UX
- One shared layout, one stylesheet (`careaxis.css`, 392 lines) with CSS custom properties, skip link, visible focus ring, 44 px minimum control height, responsive grid.
- Bootstrap 5 + jQuery + unobtrusive validation vendored locally.
- Public marketing pages (home, privacy, terms, security, support) and error page.
- **Design is "marketing site" rather than "clinical workstation"**: large hero sections, generous cards, centred marketing nav. There is no application shell — no persistent sidebar, no global search, no breadcrumbs, no data-table system, no drawer/dialog system, no toast system, no empty/loading/error state conventions beyond one or two ad-hoc strings.
- **English only.** No localization resources, no RTL, no Arabic typography, no locale-aware dates/numbers.

---

## 4. Defects and risks found

| # | Severity | Finding | Location |
|---|---|---|---|
| 1 | High | `Doctor.Salary` is hard-coded `[Range(15000,20000)]` and `Admin.Salary` `[Range(20000,25000)]`; clinician creation is impossible outside those values and salary has no business meaning in this domain. | `DAL/Entities/Doctor.cs`, `Admin.cs` |
| 2 | High | Appointment model has **no time-of-day**; capacity is a global constant of 50 for every clinician regardless of shift length. Two patients cannot conflict in the model because time does not exist — scheduling is not clinically usable. | `Appointment.cs`, `AppointmentRules.cs` |
| 3 | High | No encounter, therefore no lifecycle, no responsible clinician per visit, no signed/finalized state, no amendment trail. `MedicalRecord` is freely editable in principle and carries no author signature/time. | `MedicalRecord.cs` |
| 4 | High | Only three roles (`Admin`, `Patient`, `Doctor`). No nurse, receptionist, laboratory technician, radiologist, pharmacist, billing officer, records officer, management, auditor. No permission granularity — role strings only. | `Hospital.BLL/Helpers/Role.cs` |
| 5 | High | No reporting system. Admin dashboard exposes six raw counters with no filters, no date ranges, no exports, no queries beyond counts. | `AdminController.Index` |
| 6 | Medium | Patient identity is only an `ApplicationUser` row: no medical record number (MRN), no duplicate detection, no allergies, no structured demographics beyond address/emergency contact. | `Patient.cs` |
| 7 | Medium | Hand-authored migration `20261002000000_AddAuditAndAppointmentHardening` was, per the README, **never validated by EF tooling nor applied to a database**. The model snapshot must be proven consistent. | `DAL/Migrations` |
| 8 | Medium | Roles are seeded twice (migration `seeddRoles` + runtime `SeedRolesAsync`); the migration's `Down` deletes **all** roles. Divergent seed paths are a maintenance hazard. | `Migrations/seeddRoles.cs`, `Program.cs` |
| 9 | Medium | Admin/doctor/patient list endpoints have no pagination or server-side filtering; `GetPatients` loads all patients. This will not survive production data volume. | `AdminController` |
| 10 | Medium | No CSRF-token-aware fetch helper, no client-side error convention; AJAX deletes (`[HttpDelete]`) return ad-hoc shapes, so the UI cannot show consistent failures. | `wwwroot/js/site.js`, `AdminController` |
| 11 | Medium | `AdminController.EditPatient` / `DeletePatient(email)` — patient selection by email is fragile and leaks an identifier correlation; should be by opaque patient key. | `AdminController` |
| 12 | Medium | No localization infrastructure at all; the brief requires Arabic + English with RTL. | repository-wide |
| 13 | Low | Two layouts exist (`_Layout.cshtml` with the default scaffold header and `_MainLayout.cshtml` the real one); `_Layout` is dead weight and confusing. | `Views/Shared` |
| 14 | Low | `Home/Error` view and `UseExceptionHandler` differ in development vs production; no correlation id surfaced to the user for support. | `Program.cs`, `Views/Shared/Error.cshtml` |
| 15 | Low | Email delivery is silently "unavailable"; there is no operator-visible queue/job health indicator beyond the Hangfire dashboard. | `EmailDeliveryJob.cs` |

---

## 5. Verification limitations of this environment (disclosed)

- This sandbox has **no .NET SDK, no NuGet feed, no SQL Server and no Docker**. The solution **cannot be compiled or executed here**.
- Authoritative compile/test verification is therefore performed by the repository's own **GitHub Actions** pipeline on .NET 10, whose output is recorded verbatim in `toolkit-output/project-toolchain-verification.md`.
- Because the app cannot run in the sandbox, runtime and visual verification are performed on a **static rendering of the actual Razor markup and the actual design-system stylesheet**, served as a live preview, and every limitation is stated in the verification report. No test is ever reported as passed unless the CI run or a real execution proves it.

---

## 6. Conclusion

The existing system is a **well-secured appointment demo with a marketing-site UI**. The brief demands a hospital operating platform. The gap is overwhelmingly in *domain* (encounters, diagnostics, pharmacy, beds, billing, reporting) and *workspace UX* (application shell, data tables, role workspaces, i18n), not in the security foundation — which is the expensive part and is already sound.

Recommended direction: **in-place extension** of the existing solution, retaining Identity, EF Core, MVC, security middleware, audit logging and CI, with a documented, reversible migration path.
