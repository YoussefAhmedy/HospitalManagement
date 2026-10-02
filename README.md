# CareAxis — healthcare workflow demonstration

CareAxis is a role-based ASP.NET Core MVC demonstration for patient appointment requests, clinician review, and scoped medical-record workflows. It preserves the existing domain model while modernizing account handling, persistence, security controls, and the shared UI.

> **Use synthetic data only.** This repository is not a clinical service, emergency channel, medical-advice system, or a claim of HIPAA, GDPR, or other legal compliance. Do not deploy it with real patient data until the organization has completed the required privacy, legal, security, clinical, and operational review.

## Stack

- .NET 10 / ASP.NET Core 10 / C# 14 (`global.json`, `Directory.Build.props`)
- EF Core 10 and SQL Server
- ASP.NET Core Identity for accounts and role checks
- Hangfire SQL Server storage for background transactional-email jobs
- MailKit SMTP delivery (disabled unless configured)
- MSTest domain-rule tests in `Hospital.Tests`

Microsoft package versions are centrally pinned in `Directory.Packages.props`. The EF design-time tool is pinned in `.config/dotnet-tools.json`.

## Included workflows

- Patient self-registration, email confirmation, sign-in/lockout, password recovery, profile updates, and verified email changes.
- Optional Google sign-in when OAuth client credentials are provided.
- A one-time, out-of-band administrator bootstrap command; there is no default admin account or checked-in password.
- Clinician account creation by an administrator, with email verification required before sign-in.
- Patient appointment requests against an assigned clinician schedule, a per-clinician daily capacity, patient cancellation, and clinician approval/decline.
- Patient and clinician record queries scoped to the authenticated account; clinician record creation requires an approved appointment relationship.
- Minimal audit metadata for selected account, appointment, and medical-record operations. Clinical note text and credentials are not written to the audit table.
- Local profile-image uploads validated by extension, size, and signature, stored outside `wwwroot`, and served through an authenticated endpoint.

Dates in the legacy appointment domain are clinic-local calendar dates. Configure and document the clinic time zone before using the workflow in any real deployment; this application does not currently provide a time-zone management feature.

## Requirements

- .NET SDK 10.0.100 or a compatible later .NET 10 feature band (as selected by `global.json`)
- A reachable SQL Server database
- NuGet access for restore

No database, patient, clinician, or administrator seed account is included. Startup creates the required Identity roles after the database schema exists.

## Local development

1. Set a development connection string outside source control. For example, use .NET user-secrets or an environment variable:

   ```sh
   export ConnectionStrings__DefaultConnection='Server=localhost;Database=CareAxisDev;User Id=...;Password=...;TrustServerCertificate=True'
   ```

   Replace the placeholder with a **local synthetic-data database** credential. Do not commit it.

2. Restore dependencies, restore the local EF tool, apply the migrations to that disposable development database, then run the checks:

   ```sh
   dotnet tool restore
   dotnet restore HospitalManagement.sln
   dotnet ef database update --project DAL/Hospital.DAL.csproj --startup-project HospitalManagement/HospitalManagement.csproj --context HospitalDbContext
   dotnet build HospitalManagement.sln --configuration Release --no-restore
   dotnet test HospitalManagement.sln --configuration Release --no-build
   ```

3. Start the app locally:

   ```sh
   ASPNETCORE_ENVIRONMENT=Development dotnet run --project HospitalManagement/HospitalManagement.csproj --urls http://localhost:8080
   ```

   The application requires `ConnectionStrings__DefaultConnection` at startup. Do not enable SMTP or Google sign-in unless their provider settings are configured in a secret manager or environment.

### One-time administrator bootstrap

After applying migrations, provision the first administrator using a dedicated command. Supply the email, display name, and a strong unique password through your secret manager or process environment, then remove those temporary credentials immediately:

```sh
export BootstrapAdmin__Email='admin@example.invalid'
export BootstrapAdmin__FirstName='CareAxis'
export BootstrapAdmin__LastName='Administrator'
export BootstrapAdmin__Password='use-a-unique-secret-from-your-secret-manager'
dotnet run --project HospitalManagement/HospitalManagement.csproj -- --bootstrap-admin
unset BootstrapAdmin__Email BootstrapAdmin__FirstName BootstrapAdmin__LastName BootstrapAdmin__Password
```

The bootstrap command creates an email-confirmed Identity account in the `Admin` role, refuses to alter an existing account, and exits without starting the web server. It never accepts a password as a command-line argument. Use a real, controlled email address for your organization, not the documentation placeholder.

## Configuration

The application reads standard ASP.NET Core configuration. Environment-variable names use `__` for nested keys.

| Setting | Required | Purpose |
| --- | --- | --- |
| `ConnectionStrings__DefaultConnection` | Yes | SQL Server used by Identity, application data, and Hangfire storage. |
| `AllowedHosts` | Production | Explicit host allowlist. Production startup rejects an empty or wildcard value. |
| `DataProtection__KeysPath` | Production | Persistent, access-controlled location for ASP.NET Core Data Protection keys. |
| `ForwardedHeaders__KnownProxies__0` (and subsequent indices) | If behind a proxy | IP addresses of trusted reverse proxies. Forwarded headers are limited to one hop. |
| `EmailOptions__Enabled` | No; defaults to `false` | Enable actual SMTP delivery. When enabled, sender, SMTP host, port, and provider credential are required. |
| `EmailOptions__From` | When email enabled | SMTP sender/account. |
| `EmailOptions__SmtpServer` | When email enabled | SMTP server hostname. |
| `EmailOptions__Port` | When email enabled | SMTP port; the implementation requires STARTTLS. |
| `EmailOptions__Password` | When email enabled | SMTP provider credential; supply through a secret manager. |
| `Auth__Google__ClientID`, `Auth__Google__ClientSecret` | No | Both are required to register the optional Google sign-in provider. |

`EmailOptions__Enabled=false` means messages are **not delivered**. The UI reports that notification delivery is unavailable rather than claiming that an email was sent. Queued email payloads are protected with ASP.NET Core Data Protection before they are stored by Hangfire.

## Database and migration notes

Migrations are located in `DAL/Migrations`. The latest migration adds appointment row-version concurrency, audit metadata, and composite query indexes. It was manually authored in this environment and has **not yet been validated by EF tooling or applied to a database**. Before a production release:

1. Run the build and tests with the pinned SDK and package feeds available.
2. Inspect EF's pending model changes and generate/compare the migration from the current model snapshot.
3. Apply the migration to a disposable SQL Server database and test upgrade, rollback, and backup/restore procedures.
4. Review data-retention, audit access, SQL permissions, concurrency behavior, and the legacy date/time assumptions.

`HospitalDbContextFactory` lets EF tooling create the context without starting role seeding or the background-job server. It requires `ConnectionStrings__DefaultConnection` in the tool process environment.

## Container deployment

A multi-stage `Dockerfile` builds the web project on .NET 10 and runs it as a non-root user on port 8080. `compose.yaml` expects an **existing SQL Server** and uses persistent Docker volumes for the Data Protection key ring and profile images; it does not provision a database or embed credentials.

```sh
cp .env.example .env
# Edit .env locally; provide the real database connection and exact public host allowlist.
docker compose up --build
```

The `.env` file is ignored by Git. Do not use the example values as production settings. For a one-time container bootstrap after applying migrations, export the four `BootstrapAdmin__...` variables as described above and run `docker compose run --rm -e BootstrapAdmin__Email -e BootstrapAdmin__FirstName -e BootstrapAdmin__LastName -e BootstrapAdmin__Password web --bootstrap-admin`, then unset them. This reuses the configured database and persistent key volume without starting the web server.

Terminate TLS at a trusted reverse proxy, configure its exact IP in `ForwardedHeaders:KnownProxies`, protect the key and upload volumes, and restrict network/database access. The sample port binding is loopback-only; if you change it for a different proxy topology, keep the web port private behind TLS. Never expose the app directly to the public internet over plain HTTP.

## Security and operations boundary

The code includes Identity password/lockout settings, secure session and antiforgery cookies, automatic antiforgery validation, role checks, owner-scoped patient/clinician queries, selected rate limits, response security headers, `no-store` headers on authenticated routes, parameterized EF queries, profile-image validation, and minimal audit events. These are implementation controls, not proof of a complete threat model or a security certification.

The repository does **not** provide production backups, tested restores, an alerting/monitoring service, managed secret storage, cloud profile-image storage, payment processing, incident response, a staffed support desk, a completed privacy notice, or jurisdiction-specific legal terms. Those remain the deployment operator's responsibility. Do not claim HIPAA, GDPR, or other regulatory compliance based only on these technical controls.

Public pages use generic CSS illustrations and synthetic UI examples. No real patient records are bundled or seeded. Profile-image storage is local to the app container unless the deployment supplies persistent storage; a managed object-storage integration is not implemented.

## CI

The GitHub Actions workflow in `.github/workflows/ci.yml` restores, builds, and runs the unit tests on .NET 10. It does not connect to a database, apply migrations, or certify deployment security.
