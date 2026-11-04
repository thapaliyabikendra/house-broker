# House Broker

> **Status: implementation in progress.** The .NET API/SSO services, Angular app, tests, and documentation listed below are implemented; the delivery plan remains the source of truth for scope and sequencing.

A property marketplace MVP: seekers search listings, brokers manage their own listings and see commission, admins manage commission tiers, and OAuth partners get scoped read access. Delivered as a .NET 10 Clean Architecture API, a separate OpenIddict/Identity SSO service, and an Angular SPA behind one nginx origin.

- Business view: [docs/business-overview.md](docs/business-overview.md)
- Technical view: [docs/technical-overview.md](docs/technical-overview.md)
- Diagrams: [docs/diagrams.md](docs/diagrams.md)

## What's in the box

| Component | Purpose |
|-----------|---------|
| `HouseBroker.Api` | Business API: listings, search, images, commission, admin. Path base `/api`. Cache: `ICacheService` over in-memory or `IDistributedCache` (`Microsoft.Extensions.Caching.StackExchangeRedis` when `Cache:Provider=Redis`). |
| `HouseBroker.Auth` | OIDC / OAuth 2.0 SSO (ASP.NET Core Identity + OpenIddict). Path base `/sso`. |
| Angular SPA (`ui/`) | Browser client using authorization code + PKCE. Served at `/`. |
| nginx gateway (`deploy/`) | Single origin: `/`, `/api`, `/sso`; TLS termination point. |
| SQL Server | One database: `HouseBroker` (business + identity). |
| Redis | Multi-instance cache provider (Memory is used in development). |

## Repository map

| Path | Contents |
|------|----------|
| `docs/plan/` | Scope, decisions, architecture, standards, phases, testing, delivery, out-of-scope |
| `docs/diagrams.md` | System context, container, layering, data model and key flow diagrams |
| `docs/business-overview.md`, `docs/technical-overview.md` | High-level summaries |
| `raw-requirements.md` | Original assignment text (do not edit) |
| `api/` | .NET solution: 8 `src` projects, 5 test projects |
| `ui/` | Angular app served at `/` |
| `deploy/` | nginx gateway config and production Compose example |
| `.github/workflows/ci.yml` | .NET + web CI jobs and Docker build |

## Architecture at a glance

```
Browser ──► gateway (nginx, single origin)
              ├─ /      → Angular SPA
              ├─ /api/  → HouseBroker.Api ×N ──► SQL Server (HouseBroker: listings + image blobs), Redis
              └─ /sso/  → HouseBroker.Auth ×N ──► SQL Server (HouseBroker)
```

The API validates tokens locally against the issuer's cached discovery document and JWKS, so the services share no secret and the API stays stateless. Only the SSO service keeps session state (Data Protection keys in its database).

Dependency rules, layer responsibilities and the full project layout are in [docs/plan/03-architecture.md](docs/plan/03-architecture.md); the rules are enforced by architecture tests.

## Prerequisites

- .NET 10 SDK
- Node.js LTS + npm (for the Angular app)
- Docker Desktop (or any Docker + Compose v2)
- Git

Pinned versions live in `global.json`, `ui/package.json` and the CI workflow.

## Quick start

```powershell
# 1. Local infrastructure: SQL Server 2022 (+ Redis in the production compose)
docker compose up -d

# 2. Apply migrations and the idempotent seed
dotnet HouseBroker.Api.dll migrate
dotnet HouseBroker.Auth.dll migrate

# 3. Run the services (ports come from launchSettings/appsettings)
dotnet run --project api/src/HouseBroker.Api
dotnet run --project api/src/HouseBroker.Auth

# 4. Run the SPA with a dev proxy for /api and /sso
npm --prefix ui ci
npm --prefix ui start

# 5. Tests and quality gates
dotnet test api/HouseBroker.sln
npm --prefix ui run lint; npm --prefix ui run test; npm --prefix ui run build
```

### Development secrets

The Auth host reads its local secrets from user-secrets, never from source control. Set them once for the `HouseBroker.Auth` project:

```powershell
dotnet user-secrets set "Auth:AdminPassword" "<admin password>" --project api/src/HouseBroker.Auth
dotnet user-secrets set "Auth:DemoUserPassword" "<demo password>" --project api/src/HouseBroker.Auth
dotnet user-secrets set "Auth:DemoPartnerSecret" "<partner secret>" --project api/src/HouseBroker.Auth
dotnet user-secrets set "Auth:SwaggerClientSecret" "<swagger secret>" --project api/src/HouseBroker.Auth
```

`Auth:AdminPassword` is required, so the Auth host does not start without it. Without `Auth:DemoUserPassword` the demo users are not seeded (a warning is logged). Without `Auth:DemoPartnerSecret` or `Auth:SwaggerClientSecret` those clients are not seeded.

Production-style topology (gateway, 2 API replicas, SSO, migrate jobs, SQL Server, Redis):

```powershell
docker compose -f docker-compose.prod.yml up --build
docker compose -f docker-compose.prod.yml up --scale api=3
```

### URLs and path bases

| Surface | Path base | Example |
|---------|-----------|---------|
| SPA | `/` | `https://<host>/` |
| API | `/api` | `https://<host>/api/v1/listings` |
| API health | `/api` | `https://<host>/api/health/live`, `/api/health/ready` |
| SSO | `/sso` | `https://<host>/sso/.well-known/openid-configuration` |
| SSO health | `/sso` | `https://<host>/sso/health/live`, `/sso/health/ready` |
| Swagger | `/api` | `https://<host>/api/swagger` |

Route templates start with `v{version}/...`, so public URLs are `/api/v1/...` and never `/api/api/...`.

## Configuration reference

Configuration follows twelve-factor rules: environment variables (for example `Section__Key`) override `appsettings`, options are validated on startup, and no secrets live in the repo or images (`*_FILE` is supported for connection strings and certificate passwords).

| Setting | Default | Notes |
|---------|---------|-------|
| `PathBase` | `/api` (Api), `/sso` (Auth) | Applied with `UsePathBase`; also used for redirects, cookies and Swagger |
| `Auth:AdminPassword`, `Auth:DemoUserPassword`, `Auth:DemoPartnerSecret`, `Auth:SwaggerClientSecret` | — | Local secrets (user-secrets in Development, secret store elsewhere); never in source |
| `ConnectionStrings` | — | One connection string for `HouseBroker` (business + identity schemas); from user-secrets or the secret store |
| `Database:AutoMigrate` | `true` in Development, `false` elsewhere | Production migrations run only through `migrate` jobs |
| `Cache:Provider` | `Memory` (development), `Redis` (multi-instance) | Cache failures log a warning and fall through to the database |
| `Pricing:CurrencyCode` | `NPR` | One currency per deployment, no conversion; exposed by `GET /api/v1/meta` |
| `Localization:SupportedCultures` | `en`, `ne` | Adding a language is translation files plus this list |
| `RateLimiting` | see 4.6 | Global plus named policies (`search`, `write`, `upload`, `auth`); 429 carries `Retry-After` |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | unset | Enables OpenTelemetry export when set |
| `TZ` | `UTC` | Containers run in UTC; no server-side local-time conversion |

Feature flags (`Microsoft.FeatureManagement`, config-backed):

| Flag | Service | Effect when off |
|------|---------|-----------------|
| `ListingCache` | API | Cache bypassed (kill switch) |
| `ImageUploads` | API | Upload endpoints return 503; existing images still served |
| `BrokerSelfRegistration` | SSO | Broker sign-up rejected; Seeker sign-up still works |

The full variable table, secret handling and certificate rollover are documented in `docs/deployment.md`.

## Seeded data and demo access

Seeding is idempotent (upsert by natural key), so reruns are safe.

- **Every environment:** roles and permission claims, property types, features, the 3 commission tiers, and the first-party `housebroker-web` client.
- **Development only:** demo brokers, a seeker, demo listings and a demo partner client. Demo passwords and the partner secret come from user-secrets (see [Development secrets](#development-secrets)); a missing value means that demo data is not seeded. The `housebroker-swagger` client is seeded in Development and Staging when `Auth:SwaggerClientSecret` is set.
- **Production admin:** created from configuration/secrets, never seeded with a known password.

## Commission

Commission is a flat rate chosen by the listing's price tier and applied to the whole price.

| Price | Rate |
|-------|------|
| `< 50,00,000` | 2.00% |
| `50,00,000 … 1,00,00,000` (both inclusive) | 1.75% |
| `> 1,00,00,000` | 1.50% |

Amounts are in the deployment currency (`Pricing:CurrencyCode`, default NPR); thresholds are plain numbers and are never converted. The result is rounded to 2 decimals with `MidpointRounding.AwayFromZero`.

The snapshot (`CommissionAmount`, `CommissionRatePercent`, `CommissionRateId`) is stored on the listing and recalculated when the price changes. Later tier edits are **not** retroactive. Commission appears only in the owner DTO (`OwnerListingDto`) for the owning broker; public, partner and admin views never include it. Tiers are DB-configured and editable by Admin only.

## Authentication and authorization

- **Users:** authorization code + PKCE (Angular, Swagger, third-party apps); refresh tokens with rotation.
- **Partners:** `client_credentials`, read scopes only (`listings.read`, `contacts.read`).
- **Effective access** = the user's permission ∩ the client's granted scope; ownership (`sub` equals the listing's `BrokerId`) is checked in the service layer.
- **Anonymous:** search, listing detail, lookups and `meta`. Broker contact needs `contacts.read` (and `listings:contact:view` for users).
- Roles: `Broker`, `Seeker`, `Admin` (Admin manages commission tiers only).

The permission/scope matrix is in [docs/plan/03-architecture.md](docs/plan/03-architecture.md) (3.6).

## Localization and time

- Languages: English (`en`, default, `en-IN` formatting) and Nepali (`ne`, `ne-NP`). Nepali strings are drafts and need native-speaker review.
- Web negotiation: saved choice, then browser languages, then `en`. API: `Accept-Language`. SSO: `ui_locales`, then culture cookie, then `Accept-Language`. Localized responses carry `Content-Language` and `Vary: Accept-Language`.
- Every instant is stored as UTC `datetime2(3)` and serialized as ISO 8601 with `Z`; offset-less input is rejected. `TimeProvider` is the only source of "now". Display uses the browser's time zone in the active locale.
- Not translated: broker-written titles/descriptions and locations (documented limitation).
- **Angular i18n:** Transloco JSON — root `ui/src/assets/i18n/en.json` / `ne.json` plus feature-scoped `features/<name>/i18n/en.json` / `ne.json`, lazy-loaded, fallback `en`. Server uses `.resx` files (`Errors.resx` + `Errors.ne.resx`) with `IStringLocalizer`. NOT `en.json`.
- `npm run i18n:check` runs `@jsverse/transloco-keys-manager` to fail on missing or unused keys and on translation drift; a separate check fails on use of the built-in `date`, `currency`, `number` and `percent` pipes.
- Cache keys include culture for localized values and separate public from owner/contact responses.

## Testing

```powershell
dotnet restore api/HouseBroker.sln
dotnet build api/HouseBroker.sln --no-restore
dotnet test api/HouseBroker.sln --no-build
dotnet format api/HouseBroker.sln --verify-no-changes
dotnet list api/HouseBroker.sln package --vulnerable --include-transitive

npm --prefix ui ci
npm --prefix ui run lint
npm --prefix ui run format:check
npm --prefix ui run test
npm --prefix ui run build
npm --prefix ui run api:check
npm --prefix ui run i18n:check
```

Strict TDD applies to commission calculation, domain invariants, validators, `ListingService`, idempotency, Auth claim/scope rules, and Angular stores/interceptors/pipes. See [docs/plan/06-testing.md](docs/plan/06-testing.md).

## Documentation map

| Document | Contents |
|----------|----------|
| [plan.md](plan.md) | Delivery plan and document map |
| [docs/plan/01-scope-and-requirements.md](docs/plan/01-scope-and-requirements.md) | Scope and R1–R27 traceability |
| [docs/plan/02-decisions.md](docs/plan/02-decisions.md) | Decisions (the decision log) |
| [docs/plan/03-architecture.md](docs/plan/03-architecture.md) | Solution layout, data model, API surface, DDD-lite |
| [docs/plan/04-standards.md](docs/plan/04-standards.md) | Cross-cutting standards and security checklist |
| [docs/plan/05-phases.md](docs/plan/05-phases.md) | Phased tasks and exit criteria |
| [docs/plan/06-testing.md](docs/plan/06-testing.md) | Testing strategy |
| [docs/plan/07-delivery-and-risks.md](docs/plan/07-delivery-and-risks.md) | Tiers, risks, cut order |
| [docs/plan/08-out-of-scope.md](docs/plan/08-out-of-scope.md) | Out of scope and roadmap |
| [docs/plan/09-e2e-testing.md](docs/plan/09-e2e-testing.md) | End-to-end testing strategy (Chrome MCP first, Playwright deferred) |
| [docs/diagrams.md](docs/diagrams.md) | Mermaid diagrams |
| [AGENTS.md](AGENTS.md) | Working agreement for AI coding agents |

## Known limitations

- No email confirmation, password reset, MFA or social login.
- Images are stored in SQL Server (`varbinary(max)`), not object storage (swappable behind `IImageStorage`); this grows the database and its backups.
- Rate limiting is per instance in the apps, with a global per-IP limit at the gateway in production.
- SPA tokens live in `sessionStorage`; the BFF pattern is on the roadmap.
- Nepali translations are drafts; adding a language needs translation files plus a configuration entry.
- Broker text and locations are not translated; search does not transliterate between scripts.
- No end-to-end test suite; no stored per-user language; left-to-right layouts only.
- Roadmap: admin UI for client registration, end-to-end tests, object storage, BFF.

## Deliberate deviation from the brief

The raw brief asks for Clean Architecture. The business API follows it strictly. `HouseBroker.Auth` is intentionally an infrastructure-centric SSO service (Identity + OpenIddict + Razor Pages) kept as one small, separate service; the layering and dependency rules apply to the API. See [docs/plan/03-architecture.md](docs/plan/03-architecture.md) (3.1).

## Local validation results (2026-11-04)

The full local gate list from this README was run on 2026-11-04. Results:

| Gate | Result |
|---|---|
| dotnet restore api/HouseBroker.sln | Pass |
| dotnet build api/HouseBroker.sln --no-restore | Pass (0 errors; package vulnerability advisories reported by separate gate) |
| dotnet test api/HouseBroker.sln --no-build | Pass (Auth 21, Application 28, Architecture 1, Api 32) |
| dotnet format api/HouseBroker.sln --verify-no-changes | Pass |
| dotnet list api/HouseBroker.sln package --vulnerable --include-transitive | Advisory report: known transitive advisories in .NET 10 test dependencies; exit code 0 |
| npm --prefix ui ci | Pass (dependencies installed) |
| npm --prefix ui run lint | Pass |
| npm --prefix ui run format:check | Pass |
| npm --prefix ui run test | Pass (32 tests, 16 files) |
| npm --prefix ui run build | Pass |
| npm --prefix ui run api:check | Pass |
| npm --prefix ui run i18n:check | Pass |
| docker compose -f docker-compose.prod.yml build | Pass (api, auth, gateway) |
| Scaled smoke (--scale api=3) | Pass (gateway 200, health/ready 200, meta returns NPR, SQL/Redis/migrations pass) |
| Anonymous search / lookups / meta smoke | Pass |
| Partner client_credentials token + read smoke | Pass |

Notes:
- Docker-based smoke uses the development environment override for ephemeral SSO signing keys; production requires a mounted signing certificate via OpenIddict:SigningCertificate:Path.
- Seed data for local smoke (Development only): demo brokers broker1@demo.com / broker2@demo.com, seeker seeker@demo.com, admin admin@housebroker.com (passwords from `Auth:DemoUserPassword` and `Auth:AdminPassword`), partner client demo-partner (secret from `Auth:DemoPartnerSecret`).
