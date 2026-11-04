# House Broker — Technical Overview

> High-level technical view. Detailed decisions, rules and standards live in the [plan](../plan.md); this page links to them instead of repeating them.

## System at a glance

A .NET 10 backend split into two independently deployable services behind one origin, plus an Angular single-page app:

```
Browser ──► gateway (nginx, single origin)
              ├─ /      → Angular SPA
              ├─ /api/  → HouseBroker.Api  ×N ──► SQL Server (HouseBroker: listings + image blobs), Redis
              └─ /sso/  → HouseBroker.Auth ×N ──► SQL Server (HouseBroker)
```

- **`HouseBroker.Api`** — the business API: listings, search, images, commission, admin.
- **`HouseBroker.Auth`** — the OpenID Connect / OAuth 2.0 identity provider (ASP.NET Core Identity + OpenIddict), issuing signed JWTs.
- **`HouseBroker` web app** — Angular SPA using authorization code + PKCE.
- **Gateway** — terminates TLS, preserves `/api` and `/sso`, serves the SPA.

The API validates tokens locally against the issuer's cached discovery document and JWKS, so the two services share no secret and the API stays stateless; only the SSO service persists session state (shared Data Protection keys in its database).

Full diagram set (context, containers, layering, ERD, auth, commission, lifecycle, caching, idempotency): [diagrams.md](diagrams.md).

## Architecture principles

- **Clean Architecture** with an explicit project dependency table: `Domain.Shared` is referenced by all; `Domain` and `Application.Contracts` depend only on it; `Application` depends on `Domain` + `Application.Contracts`; `Infrastructure` depends on `Application`; `Api`, `Auth` and `ServiceDefaults` are hosts/support. Dependencies are enforced by architecture tests.
- **DDD-lite**: `Listing` is the aggregate root with invariant-protecting behavior; commission calculation is a pure, framework-free function.
- **Pragmatic patterns**: repository + unit of work, decorator-based caching, adapter-based storage; no MediatR, generic repositories or specification pattern.
- **Result over exceptions** for expected outcomes; `ProblemDetails` at the HTTP boundary.

Full layout, dependency rules and modeling choices are in [architecture](plan/03-architecture.md).

## Key technical decisions (summary)

| Concern | Approach |
|---------|----------|
| API style | Controllers, URL versioning, OpenAPI documented; exposed for third parties |
| Auth | OpenIddict + Identity; authorization code + PKCE, `client_credentials` for partners; permission ∩ scope, resource ownership |
| Commission | Flat rate by price tier, snapshotted on the listing; tiers in DB; owner-only DTO |
| Persistence | EF Core code-first, one SQL Server database (`HouseBroker`: business tables in `dbo`, Identity/OpenIddict in `identity`), GUID v7 primary keys, UTC `datetime2(3)`, audit columns, `rowversion` concurrency, guided by indexes |
| Caching | `ICacheService` with Memory (dev) and Redis (multi-instance) providers; decorators; never fails a request |
| Reliability | Correlation ids, idempotent creates, optimistic concurrency, retried transient faults |
| Localization & time | English/Nepali via resources and `Accept-Language`/`ui_locales`; NPR default currency; UTC storage, injectable clock, zone-aware display |

Rationale and exact values are in [decisions](plan/02-decisions.md).

## Data and contracts

- One database on SQL Server: `HouseBroker` (business tables in `dbo`, identity/OpenIddict tables in `identity`).
- Listing data is structured (location, type, price, features, images); lookups and commission tiers are seeded idempotently in every environment.
- Mutable entities carry audit columns; listing status and price changes are append-only history, and business/security-critical changes also write `AuditEvent`. There is no generic application logs table; Serilog handles application logs.
- Contracts are explicit request/response DTOs in `Application.Contracts`; public and owner DTOs are separate so commission can never leak.
- API is versioned and documented; the web client's API layer is generated from the exported OpenAPI document.

Schema, API surface and permission/scope matrix: [architecture](plan/03-architecture.md) (3.2, 3.4, 3.6).

## Quality attributes

- **Security** — controls mapped to OWASP Top 10:2025 and OWASP API Security Top 10:2023; default-deny authorization; strict headers and CSP; rate limiting.
- **Testability** — selective strict TDD on commission, domain, services, idempotency and Angular stores/pipes; controller and architecture tests; provider contract tests.
- **Operability** — health probes, structured JSON logs, optional OpenTelemetry, `migrate` jobs, least-privilege database logins.
- **Build quality** — warnings as errors, analyzers, central package management, vulnerability gates, CI for both .NET and web.

Standards and the security checklist: [standards](plan/04-standards.md). Test coverage map: [testing](plan/06-testing.md).

## Delivery and operations

Packaging is container-first: multi-stage images for `api`, `auth` and `gateway`, a development compose for infrastructure and a production compose with replicas, migrate jobs and shared storage. Deployment is environment-driven (twelve-factor config, secrets from a store) and migration never runs at service startup outside Development.

Topology, scale-out design and the runbook outline: [standards](plan/04-standards.md) (4.9) and [delivery and risks](plan/07-delivery-and-risks.md).
