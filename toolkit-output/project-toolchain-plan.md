# Project Toolchain Plan — CareAxis Hospital Information System

**Date:** 2026-11-22 · **Branch:** `arena/66f04dee-hospitalmanagement`

## 1. Project capability map (derived from the brief + audit)

| Capability | Needed | Candidate source (from toolkit catalog) | Decision |
|---|---|---|---|
| Server-side application framework | Yes | Existing stack: ASP.NET Core 10 MVC + EF Core + Identity | **Use existing** — brief mandates preserving the stack |
| Compile + test verification | Yes | Existing `.github/workflows/ci.yml` (dotnet restore/build/test on .NET 10) + authenticated `gh` CLI | **Use existing** — the only executable verification available (no local SDK/NuGet) |
| Design direction / UI quality | Yes | Taste Skill, Impeccable, UI UX Pro Max, React Bits, GSAP Skills, transitions.dev | **Reference only** — the app is server-rendered Razor; animated-React libraries and skill installers require hosts (Claude Code plugins / Node 22 skill CLIs) that do not exist in this harness, and installing them cannot be verified here. Their *principles* are applied in `design-system.md`. |
| Browser inspection / visual regression | Yes | REA (`morluto/rea`), Playwright MCP / Playwright CLI | **Blocked** — REA requires Node 22 + an MCP host; Playwright needs a browser runtime with no network egress to download one. Recorded as deployment-phase configuration. |
| Library documentation retrieval | Useful | Context7 | **Skip** — targets a different agent host; the .NET 10 APIs in use are known and pinned |
| Repository semantic analysis | Optional | Serena | **Skip** — Python/uv-based MCP for a host that is not present |
| Security audit | Yes | Cloudflare security-audit-skill (guidance) + OWASP ASVS 5.0 | **Guidance only** — applied as the manual checklist in `security-privacy-review.md` |
| Reporting/PDF | Yes | — | **CSV + print-to-PDF** implemented with BCL only; server-side PDF engine requires a NuGet dependency unavailable offline |
| 3D / images / video | No | TRELLIS.2 and others | **Not applicable** |

## 2. Dependency closure

- No new NuGet packages may be added: **NuGet is unreachable from the sandbox**, so any added package would be unverifiable and would risk breaking the build. All new code uses `Microsoft.AspNetCore.App` framework types and the already-pinned package set (EF Core, Identity, Hangfire, AutoMapper, MailKit, MSTest).
- No new system services. No credentials requested. No paid services. No remote server changes.

## 3. Verification method (per selected tool)

| Tool | Verification | Recorded in |
|---|---|---|
| .NET 10 build | `gh run watch` on the CI workflow; conclusion + job log | `project-toolchain-verification.md` |
| MSTest suite | CI `dotnet test` summary | same |
| EF model integrity | New test building the model + migration snapshot consistency check in CI | same |
| Design system | Static rendering of real markup + real stylesheet in a live preview; screenshots | `02-verification-report.md` |

## 4. Rollback

Every change is additive to a feature branch; the base commit `bf4c082` is the rollback point (`git revert` / branch reset). No production configuration, no secrets, no remote infrastructure touched.
