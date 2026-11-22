# CareAxis — Technical Architecture (as-is, and the target architecture)

## 1. As-is architecture

```
HospitalManagement (web, net10.0)
  Controllers/  Account · Admin · Doctor · Home · Patient
  Views/        Shared layouts + per-controller Razor pages
  Infrastructure/ DashboardAuthorizationFilter · DatabaseHealthCheck · EmailDeliveryJob
                  HangfireEmailQueue · LocalProfileImageStorage · IProfileImageStorage
  Services/     AuditLogger · IAuditLogger
  Program.cs    composition root, middleware pipeline, role seeding, admin bootstrap
        │
        ▼
Hospital.BLL (services + view models + mapping, net10.0)
  Services/Implementation  Appointment · Doctor · Patient · MedicalRecord · Schedule · Shift
                           Sepcialization · EmailSender
  Services/Abstraction     one interface per service
  ModelVM/                 26 flat view models
  Mappers/DomainProfile    AutoMapper profile
  Helpers/                 Role · ResourceOwnership · EmailOptions · GoogleOptions
        │
        ▼
DAL (net10.0)
  DataBase/     HospitalDbContext (IdentityDbContext<ApplicationUser>), design-time factory
  Entities/     ApplicationUser ← Patient | Doctor | Admin
                Appointment · MedicalRecord · Schedule · Shift · Specialization · AuditLog
  Entities/config  IEntityTypeConfiguration per aggregate
  Repository/   generic-ish repositories per aggregate (Abstraction + Implementation)
  Migrations/   4 generated migrations + seeddRoles + 1 hand-authored hardening migration
```

**Request pipeline (verified order in `Program.cs`):** forwarded headers → roles seeded → HTTPS redirection → static files → routing → rate limiter → authentication → authorization → Hangfire dashboard → health endpoints → MVC default route.

**Data:** single SQL Server database holding Identity tables, clinical tables and Hangfire tables together.

### Structural observations

1. **Repository layer adds little.** Repositories wrap `DbSet` with near pass-through methods while services also hold a `DbContext`. Entity Framework's `DbContext` is already a unit-of-work/repository; the wrapper is duplicated surface area with no abstraction benefit (nothing is ever faked or swapped).
2. **Services return eager lists.** `GetPatients()`-style methods materialise full tables; no `IQueryable` composition, no paging contract, no projection to a DTO.
3. **View models are flat and controller-shaped**, not workspace-shaped; each new page currently implies a new bespoke view model with no shared primitives (no `PagedResult<T>`, `DateRange`, `FilterState`).
4. **One `DbContext` scope per request**, correct, but no transaction boundaries around multi-row operations (bed assignment, dispensing, invoice payment) because those aggregates do not yet exist.
5. **Migrations are inconsistent**: generated migrations plus a hand-written one plus a seed migration whose `Down` deletes all roles, plus runtime seeding.
6. **No caching, no job beyond the single email job**, no outbox for domain events, no optimistic concurrency except `Appointment.RowVersion`.

### Preserve / replace decisions (target architecture)

| Element | Decision | Reason |
|---|---|---|
| ASP.NET Core MVC + Razor | **Preserve** | Server-rendered clinical UI is appropriate; no SPA rewrite risk |
| ASP.NET Core Identity + EF stores | **Preserve** | Correct, already hardened |
| Security middleware (CSP, HSTS, antiforgery, rate limit) | **Preserve & extend** | Strongest asset in the codebase |
| EF Core + SQL Server | **Preserve** | Provider is deployment reality; no reason to churn |
| Hangfire | **Preserve** | Useful for reminders/jobs |
| Repository wrappers | **Retain but stop expanding**; new modules query through purpose-built read services | Avoid gratuitous duplication without a risky refactor |
| Audit logger | **Preserve & extend** | Extend event catalogue, add correlation id |
| AutoMapper | **Retain** for existing flows; new code uses explicit projections | Explicit projections are cheaper and clearer in reports |
| `_Layout.cshtml` scaffold layout | **Remove** | Dead code, confusing |
| Marketing CSS | **Replace** with tokenised clinical design system | Brief requires enterprise healthcare language |
| 3-role model | **Extend to 12 roles + permission claims** | Least-privilege requirement |

## 2. Target architecture

```
HospitalManagement (web)
  Controllers/        Account · Home · Admin/* · Doctor/* · Reception/* · Nurse/* ·
                      Laboratory/* · Radiology/* · Pharmacy/* · Billing/* · Reports/* ·
                      Patient/* · Audit/*
  Views/              Shared shell (_AppShell, _Topbar, _Sidebar) + Workspaces/* + partials
                      Partial components: DataTable, StatusBadge, KpiCard, Drawer, Dialog, Toast,
                                          EmptyState, ErrorState, FilterBar, Pagination
  ViewComponents/     Sidebar, Breadcrumbs, KpiStrip, Queue
  Infrastructure/     Permission policies, authorization handlers, CSV writer, correlation middleware
  wwwroot/css/        design-system.css (tokens + components), app.css (shell), rtl.css, print.css
  wwwroot/js/         app.js (shell, drawers, toasts, fetch helper with CSRF), tables.js (client sort/filter)
        │
        ▼
Hospital.BLL
  Services/           Existing 7 services (preserved) + new services per module:
                      EncounterService · TriageService · LabService · RadiologyService ·
                      PharmacyService · AdmissionService · BillingService · ReportService ·
                      SchedulingService · PatientDirectoryService · NumberingService
  Reports/            Typed report queries returning PagedResult<T> + summary rows
  ModelVM/            Workspace view models composed from shared primitives
  Common/             PagedResult<T>, DateRange, Result<T>, FilterOptions, Money
        │
        ▼
DAL
  Entities/           Existing + Encounter, VitalSign, Diagnosis, ClinicalNote,
                      LabTest, LabOrder, LabOrderItem, LabResult,
                      ImagingOrder, ImagingReport,
                      Medication, Prescription, PrescriptionItem, Dispense, StockBatch, StockMovement,
                      Ward, Room, Bed, Admission, BedAssignment,
                      ServiceCatalogueItem, Invoice, InvoiceLine, Payment,
                      InsurancePolicy, Branch, Department, NumberSequence
  DataBase/           HospitalDbContext extended; one migration per logical change set
  Seed/               DemoDataSeeder (synthetic, idempotent, guarded by environment flag)
```

### Key invariants to enforce (server-side, not UI-side)

1. A patient may not hold two **active** inpatient bed assignments (`filtered unique index` on active occupancy).
2. A doctor may not hold two appointments at the same date+slot (`unique index`).
3. A lab result can only be **released** by a verifier/authorizer role, and only after it is `Verified`.
4. A signed clinical note or finalized report cannot be updated; amendments create a new version row referencing the original.
5. An invoice balance = Σ lines − Σ payments; payments cannot exceed balance; refunds require an explicit permission.
6. Every write that touches clinical or financial data is audited with actor, resource, and correlation id.
7. Patient-role users can only ever read their own records (fail-closed ownership), never enumerate others.

### Non-goals (explicit)

DICOM/PACS, real payment/insurance gateways, e-prescribing networks, SMS, server-side PDF generation, multi-tenant database isolation. Each is documented in `commercial-readiness-assessment.md` with the exact integration seam.

## 3. Deployment model decision

The existing deployment is **single-hospital, self-hosted, one SQL Server instance**, configured via environment variables and a reverse proxy. That is retained. Multi-branch is supported by a `Branch` entity and branch-scoped queries, **not** by shared multi-tenant databases. Rationale: healthcare customers routinely require data residency and isolated backups; a shared-schema SaaS is a commercial decision that must not be made implicitly by code. Documented in `commercial-readiness-assessment.md`.
