# CareAxis — Security & Privacy Review

**Reference frameworks:** OWASP ASVS 5.0, OWASP Top Ten, HL7 FHIR R5 (security and privacy
considerations). **This is a self-assessment of the implementation in this repository. It is not an
audit, not a penetration test, and not a compliance certification.**

> **No claim of HIPAA, GDPR, or any other regulatory compliance is made here or anywhere in this
> project.** Compliance depends on the operator's legal agreements, hosting jurisdiction, workforce
> training, business-associate contracts, breach procedures and physical controls — none of which a
> codebase can supply. What follows is a technical control inventory with an honest gap list.

---

## 1. What is implemented

### 1.1 Authentication

| Control | Implementation | ASVS ref |
|---|---|---|
| Password policy | Minimum 12 characters, upper + lower + digit + symbol | V2.1 |
| Lockout | 5 failed attempts, 15-minute lockout for new users | V2.2 |
| Email confirmation required before sign-in | `RequireConfirmedEmail = true` | V2.1 |
| Unique email enforced | `RequireUniqueEmail = true` | V2.1 |
| Password reset | Token-based, single-use, expiring | V2.5 |
| Optional Google OAuth | Only registered when `GoogleOptions` are configured; `SaveTokens = false` | V2.8 |
| Admin bootstrap | Explicit command, no default administrator seeded | V2.9 |
| Login rate limiting | Fixed window, **10 attempts per 5 minutes per IP** on all authentication actions | V2.2 |

**Gaps:** no multi-factor authentication; no breached-password check; no account-enumeration
protection on the registration path beyond ASP.NET Core defaults; no session listing or remote
revocation.

### 1.2 Session management

| Control | Implementation |
|---|---|
| Cookie name/type | `CareAxis.Session`, `HttpOnly` |
| Transport | `SecurePolicy.Always` — the cookie is never sent over cleartext |
| `SameSite` | `Lax` (portal navigation works; cross-site POST is not sent) |
| Lifetime | 8 hours absolute, sliding |
| Anti-forgery cookie | `CareAxis.AntiForgery`, `HttpOnly`, `Secure`, `SameSite=Strict` |
| Scoped anti-forgery | Global `AutoValidateAntiforgeryTokenAttribute` — every unsafe verb is validated unless explicitly opted out |

**Gap:** no re-authentication challenge before sensitive operations such as refund approval or
record release. ASVS V3.3 recommends step-up for high-value actions.

### 1.3 Authorisation

| Control | Implementation |
|---|---|
| Model | Permission claims → policies, enforced per action |
| Least privilege | `RolePermissions` grant map; verified cell-by-cell by script (602 cells, 0 mismatches) |
| Default deny | Every clinical and administrative controller carries a controller-level `[Authorize(Policy = …)]`; the only actions without a policy are the 8 public pages of `Home` and the 13 anonymous `Account` authentication/error endpoints, all listed in one reviewed constant in `AuthorizationCoverageTests`. A new action on a `Home`-style controller with no attribute fails a test rather than going live. |
| No accidental full-controller bypass | A test asserts no controller carries both `[AllowAnonymous]` and `[Authorize]`, a combination in which ASP.NET Core silently ignores the policy |
| Policy names are not strings | Every attribute references a `Permission` constant, so a rename cannot silently disable a gate |
| Segregation of duties | Receipt vs approval, prescribing vs dispensing, authoring vs auditing |
| Portal ownership | `ResourceOwnership.IsOwner`, unit-tested against null/whitespace |
| Hidden UI is not access control | Every gated POST is also gated server-side |

**Gaps:** no branch/tenant claim, so tenants cannot be isolated; no break-glass emergency access; no
re-authorisation after a role change during an active session (claims are re-materialised per
request from the role store, but a role *removal* is only effective on the next request that
refreshes claims).

### 1.4 Input handling and output encoding

| Control | Implementation |
|---|---|
| Output encoding | Razor auto-encoding everywhere; `Html.Raw` is used in exactly one place (`_Icon`, a closed SVG catalogue keyed by a compile-time string) |
| Model validation | Data-annotation attributes on every command model, validated server-side |
| Over-posting | Explicit parameter binding / view models, not entity binding, on new controllers |
| SQL injection | EF Core parameterised queries throughout; no string-concatenated SQL |
| Path traversal | Upload filenames discarded and replaced by a GUID; extension allow-list |
| Upload validation | Signature (magic-byte) check in addition to extension, size ceiling, stored outside the web root |
| Open redirect | Culture switch only accepts same-site relative paths (`/`-prefixed, rejecting `//` and `/\`) |
| Binding size | `MaxLength` on every user-supplied string column |

**Gaps:** no centralised request-size limit beyond framework defaults; no HTML sanitisation library
because no user-supplied HTML is rendered as markup.

### 1.5 Transport and response hardening

| Header | Value |
|---|---|
| `Content-Security-Policy` | `default-src 'self'; base-uri 'self'; object-src 'none'; frame-ancestors 'none'; form-action 'self'; img-src 'self' data: blob:; font-src 'self' data:; style-src 'self' 'unsafe-inline'; script-src 'self'; connect-src 'self'` |
| `X-Content-Type-Options` | `nosniff` |
| `X-Frame-Options` | `DENY` |
| `Referrer-Policy` | `no-referrer` |
| `Permissions-Policy` | `camera=(), microphone=(), geolocation=()` |
| `Strict-Transport-Security` | Enabled outside Development |
| HTTPS | `UseHttpsRedirection`, cookie `Secure` policy |
| Cache | `no-store, private` on every clinical, financial and audit surface |
| Error handling | Exception handler page in non-Development; only a trace identifier is exposed |

**Note on `script-src`.** The application emits **no inline script**. Every handler lives in
`wwwroot/js` (`app.js`, `booking.js`, `invoice.js`, `flash-dismiss.js`), and client validation is
served from the vendored jQuery libraries. `script-src 'self'` is therefore a real restriction, not a
decorative one. `style-src 'unsafe-inline'` remains because Razor emits a small number of inline
layout offsets; that is the next item to remove.

### 1.6 Audit and monitoring

| Control | Implementation |
|---|---|
| What is logged | Event type, actor, subject, resource type and id, UTC timestamp — **metadata only** |
| Coverage | Chart views, registration, order creation, result entry, verification, release, dispensing, refunds, approvals, report exports, audit views |
| Immutability | No create/update/delete route for the audit trail |
| Reading the trail is itself audited | `audit.view` |
| Application logging | Structured `ILogger`; clinical content is never written to logs |
| Health endpoints | `/health/live`, `/health/ready` with a database check; no version or configuration disclosure |
| Background jobs | Hangfire dashboard at `/ops/jobs`, admin-only, **read-only** |

**Gaps:** no SIEM forwarding; no alerting on anomalous access; no log retention policy in code; no
IP address or user-agent on audit rows; no tamper-evidence (hash chain) on the trail.

### 1.7 Data protection

| Control | Implementation |
|---|---|
| Encryption in transit | TLS terminated at the host with HTTPS redirection and HSTS |
| Encryption at rest | **Not implemented in application code.** Depends entirely on the operator's SQL Server TDE / volume encryption configuration. Must be a deployment prerequisite. |
| Secrets | Read from configuration / environment; connection string is required at start-up and the app refuses to boot without it; no secret is committed. Data Protection keys are persisted outside the repository. |
| Synthetic data | All seeded records are synthetic (`IsSynthetic`), addresses use `example.invalid` |
| Backups | **Not implemented.** Operator responsibility; must be configured before any real data is loaded. |
| Retention / erasure | **Not implemented.** No retention schedule, no subject-access export, no erasure workflow. Legal review required. |

---

## 2. Risk register

Severity uses a simple high/medium/low judgement, not a formal CVSS calculation.

| # | Risk | Severity | Status | Mitigation required |
|---|---|---|---|---|
| 1 | **No multi-factor authentication** | High | Open | TOTP or WebAuthn for all staff roles, mandatory for administrators and prescribing roles |
| 2 | **No tenant/branch isolation** | High | Open | Branch claim on the principal plus a global query filter; prerequisite for multi-tenant deployment |
| 3 | **No encryption at rest in code** | High | Operator-dependent | TDE or encrypted volumes; document as a hard deployment prerequisite |
| 4 | **No backup / restore tested** | High | Open | Automated backups, tested restore, documented RPO/RTO |
| 5 | **No retention or erasure workflow** | High | Open | Retention schedule + subject-access export + legal review |
| 6 | No step-up auth for refunds, releases, exports | Medium | Open | Re-authenticate or require a second officer |
| 7 | No tamper-evidence on the audit trail | Medium | Open | Hash chain or append-only store with WORM retention |
| 8 | No session revocation / device list | Medium | Open | Sign-out-everywhere, admin session termination |
| 9 | `style-src 'unsafe-inline'` remains | Low | Accepted, tracked | Move remaining inline offsets to classes |
| 10 | No optimistic concurrency on notes | Medium | Open | `RowVersion` so concurrent edits surface a conflict rather than last-write-wins |
| 11 | Patient-portal authorisation is controller-level | Medium | Open | Consolidate into an ownership policy |
| 12 | No alerting on anomalous access patterns | Medium | Open | Rules for bulk record access, off-hours access, repeated 403s |
| 13 | Doctor/patient table-per-hierarchy overlap | Low | Documented limitation | Separate patient identity if a clinician must also be a patient |
| 14 | No security regression tests in CI | Medium | Open | Add an authz matrix test that hits every action as every role |

---

## 3. Privacy considerations

| Principle | Position |
|---|---|
| **Lawful basis** | Not determinable in code. Requires the operator's legal analysis under the applicable regime. |
| **Data minimisation** | The model stores what the workflows need and no more; audit rows deliberately exclude clinical content. |
| **Purpose limitation** | Clinical, financial and audit data are separated by permission so a role cannot reach data outside its purpose. |
| **Storage limitation** | **Not implemented** — no retention policy. |
| **Integrity and confidentiality** | Confidentiality is enforced by RBAC; integrity by immutable signed records; availability depends on the operator's infrastructure. |
| **Accountability** | The audit trail records who did what; the trail itself is access-controlled and its access is logged. |
| **Subject rights** | **Not implemented** — no export, no rectification workflow beyond manual editing, no erasure. |
| **Special-category data** | Health data is special category under GDPR; processing requires an appropriate Article 9 condition. Legal review required. |

### Explicitly out of scope in this build

- Telemetry or analytics of any kind. The application sends **no** data to external services.
- No AI or third-party inference is given access to patient data.
- No DICOM image storage (the radiology module stores the radiologist's report text and the study
  metadata, not the imaging study itself).
- No payment gateway integration — payments are recorded, not processed.

---

## 4. Mapping to ASVS 5.0 chapters

| Chapter | Coverage | Notes |
|---|---|---|
| V1 Encoding and sanitisation | Good | Auto-encoding, one controlled `Html.Raw`, no concatenated SQL |
| V2 Validation and business logic | Good | Server-side validation; business rules extracted and unit-tested |
| V3 Web frontend security | Good | Strict CSP for scripts, `nosniff`, frame denial, no inline handlers |
| V4 API and web service | N/A | No public API surface beyond the culture switch |
| V5 File handling | Good | Signature check, size limit, GUID names, outside web root |
| V6 Authentication | **Partial** | Strong password policy and lockout; **no MFA** |
| V7 Session management | Good | `HttpOnly`, `Secure`, `SameSite`, 8-hour sliding, scoped anti-forgery |
| V8 Authorisation | Good | Claim/policy, least privilege, segregation of duties; **no tenant scoping** |
| V9 Self-contained tokens | N/A | Cookie-based session, no bearer tokens |
| V10 OAuth/OIDC | Partial | Google login supported when configured; no token storage |
| V11 Cryptography | Operator-dependent | TLS in code; at-rest is infrastructure |
| V12 Secure communication | Good | HTTPS redirect + HSTS |
| V13 Configuration | Good | Secrets from environment, no committed credentials, health endpoints minimal |
| V14 Data protection | **Partial** | Auditing good; retention, erasure, backup, at-rest encryption outstanding |
| V15 Secure coding | Good | Explicit results instead of boolean flags, immutable clinical records |
| V16 Logging and monitoring | Partial | Structured logs and an audit trail; no SIEM, no alerting |

**Overall ASVS position:** the *application-level* controls are in reasonable shape for a
demonstration and pre-production platform. The platform-level controls (MFA, at-rest encryption,
backup, retention, monitoring) are what stand between this and a production hospital deployment, and
they are not code problems alone.

---

## 5. Pre-production checklist

Blocking items before any real patient data is loaded:

1. MFA enforced for every staff role.
2. Encryption at rest enabled and verified.
3. Backup configured **and a restore actually performed**.
4. Retention schedule and subject-rights procedures agreed with counsel.
5. Tenant/branch isolation implemented if more than one hospital will use the instance.
6. External penetration test completed and findings remediated.
7. SIEM ingestion of the audit trail with alert rules.
8. Break-glass emergency access with retrospective review.
9. Incident response plan and breach-notification procedure documented.
10. Clinical safety review of the workflows by a qualified clinician.
