# House Broker Plan — 2. Decisions

## 2. Decisions

### Stack and architecture

| Area | Decision |
|------|----------|
| Framework | .NET 10 (LTS) |
| API style | Controllers (`[ApiController]`), URL versioning |
| Solution structure | Clean Architecture with `Domain.Shared`, `Domain`, `Application.Contracts`, `Application`, `Infrastructure`, `Api`; a separate `Auth` service (SSO); a `ServiceDefaults` library for host-level cross-cutting code (see 3.1) |
| Application layer | Plain application services; Mapperly source-generated mapping; no MediatR |
| Mapping | Mapperly (`Riok.Mapperly`) in the Application layer. Compile-time generated, no reflection; explicit owner/public DTO mappings so commission fields cannot leak |
| Validation | FluentValidation in the Application layer |
| Data access | EF Core, code-first migrations. One SQL Server database, `HouseBroker`, shared by the API and SSO: business tables in the `dbo` schema, Identity/OpenIddict tables in the `identity` schema |
| Database naming | Table names are plural PascalCase; entity classes are singular PascalCase |
| Database setup | SQL Server 2022 via Docker Compose; migrations and seed applied automatically in Development; in production through `migrate` jobs (see Reliability) |
| Data seeding | Idempotent `DatabaseSeeder` in Infrastructure, run by the `migrate` command (and by Development auto-migrate). Master data in every environment: property types (Code, Name, SortOrder), features (Code, Name, SortOrder) and the 3 commission tiers. No `*Translations` tables; lookup names are stored once in the default language. Demo users, broker profiles, listings and clients in Development only. Seeding upserts by natural key, so reruns are safe |
| Domain modelling | DDD-lite (see 3.7) |
| Development method | Selective strict TDD (see 4.5) |
| Design principles | SOLID; pragmatic pattern use (see 3.8) |
| Error handling in services | `Result<T>` for expected outcomes (not found, forbidden, validation, conflict, precondition); exceptions for unexpected failures and domain invariant violations. Errors carry stable codes; messages are localized only at the API boundary (see 4.11). `Result<T>`, `Error` and `ToActionResult` stay hand-written: they encode the stable error-code contract and the localized `ProblemDetails` mapping, which a general-purpose result package would not remove |
| Security headers | `NetEscapades.AspNetCore.SecurityHeaders` in `ServiceDefaults` for HSTS, `nosniff`, `DENY`, `Referrer-Policy` and CSP, instead of custom header middleware |
| API versioning | `Asp.Versioning.Mvc` for URL versioning (`v{version}/...` route templates), instead of custom route constraints |
| Client UI | Angular single-page app (decisions below) |

### Services and path bases

| Area | Decision |
|------|----------|
| Public origin | One origin through an nginx gateway: `/` Angular app, `/api` API, `/sso` Auth service. No CORS is needed for the first-party client |
| Web path base | The SPA is served at `/` with `baseHref=/`; the gateway serves it with fallback to `/index.html` |
| API path base | `/api`, applied with `UsePathBase` from the `PathBase` setting. Route templates start with `v{version}/...`, so public URLs are `/api/v1/...` and never `/api/api/v1` |
| SSO path base | `/sso`. Endpoints: `/sso/connect/*`, discovery at `/sso/.well-known/*`, pages at `/sso/account/*` |
| Proxy behavior | The gateway forwards `/api` and `/sso` unchanged and serves the SPA from `/`; the API and Auth strip their prefixes with `UsePathBase`, and the SPA is built with `baseHref=/`. Swagger, health endpoints, redirects, cookies and Angular router links are all path-base aware |
| Development | The Angular dev server serves the SPA at `/` and proxies `/api` and `/sso` to the local services, so the browser sees one origin in development too |
| Issuer | The public issuer URL (`https://<host>/sso`) is configured per environment. The API validates tokens against it and reads the discovery document over an internal address (spike in Phase 4) |

### Authentication and authorization

| Area | Decision |
|------|----------|
| Authorization server | OpenIddict + ASP.NET Core Identity (Guid keys) in `HouseBroker.Auth`, with Razor Pages for login, register, logout and consent |
| Auth UI | Razor Pages, not MVC: page-focused UI with less controller/routing boilerplate, matching the Identity and OpenIddict samples. MVC adds no benefit for ~5 pages |
| Flows | Authorization code + PKCE (Angular app, Swagger UI, third-party apps acting for a user); `client_credentials` for partner systems (read scopes only); refresh tokens with rotation. Implicit and password flows are disabled; PKCE is required |
| Tokens | Signed JWT access tokens (15 min), refresh tokens (7 days, rolling), asymmetric signing, audience `housebroker-api`. The API validates through discovery and JWKS, so no shared secret exists between services |
| Roles | `Broker`, `Seeker`, `Admin` (Admin manages commission rates only) |
| Permissions | Permission claims from Identity's `AspNetRoleClaims`, emitted in the access token. Effective access = the user's permission ∩ the client's granted scope (matrix in 3.6). Ownership is resource-based in the service layer |
| Scopes | `listings.read`, `contacts.read`, `listings.write`, `commission.read`, `admin.commission`, plus `openid profile email phone offline_access` |
| Clients | `housebroker-web` (Angular, first party, no consent screen); `housebroker-swagger` (Development/Staging); partner clients registered with a CLI verb `register-client`; a demo partner in Development. No admin UI for clients (roadmap) |
| Partner (machine) tokens | `client_credentials` clients may only hold `listings.read` and `contacts.read`. Writing on behalf of a broker needs the authorization code flow with that broker's consent |
| Broker profile | The API keeps a `BrokerProfiles` table (name, email, phone), created and updated from the token's `name`, `email` and `phone_number` claims when a broker writes. Listing contact details are read from it. A broker write without those claims gets 403 `forbidden` |
| Access rules | Search and listing detail are anonymous. Broker contact details need a token with `contacts.read` and (for users) the `listings:contact:view` permission |
| Registration | On the SSO register page: Seeker or Broker only (never Admin); a broker needs name and phone; the `BrokerSelfRegistration` flag can disable broker sign-up |
| Out of scope (documented) | Email confirmation, password reset, MFA and social login |

### Localization

| Area | Decision |
|------|----------|
| Languages | English (`en`, default, formatting `en-IN` for lakh/crore grouping) and Nepali (`ne`, formatting `ne-NP`). The list is configuration-driven, so adding a language is translation files only. Nepali strings are drafts and need native-speaker review. Right-to-left languages are not supported, but layouts use CSS logical properties to keep that door open |
| Negotiation (web) | Saved choice (`localStorage`), then browser languages, then `en`. The `<html lang>` attribute follows. Switching applies without a reload |
| Negotiation (API and SSO) | API: `Accept-Language` (the Angular client sends the active language). SSO: the OIDC `ui_locales` parameter, then the culture cookie, then `Accept-Language`. Responses carry `Content-Language`; localized responses carry `Vary: Accept-Language` |
| What is translated | Angular UI text; SSO pages (including Identity errors and consent scope descriptions); API error messages. Not translated: lookup names (property types and features), broker-written titles and descriptions, and locations (documented limitation) |
| Error contract | Application returns stable error codes with arguments; the API boundary turns them into messages with `IStringLocalizer`. Every `ProblemDetails` carries `code` and per-field codes. The Angular client prefers its own translation of the code and falls back to the server message |
| Resources | `.resx` for the API and SSO (English default plus `ne`), never `en.json`; Angular uses Transloco JSON (`ui/src/assets/i18n/en.json`/`ne.json` plus feature-scoped `features/<name>/i18n/en.json`/`ne.json`); lookup names are stored once on `PropertyTypes.Name` and `Features.Name` (default language); translating lookup names is out of MVP scope |
| Angular i18n library | Transloco (`@jsverse/transloco`): runtime translation (one build for all languages, live switching), lazy-loaded per feature, typed keys. `@jsverse/transloco-locale` supplies the generic date/number/percent/currency pipes; six `hb*` wrappers (`hbDate`, `hbDateTime`, `hbNumber`, `hbPrice`, `hbPriceShort`, `hbRelativeTime`) sit on top of it for NPR lakh/crore prices, relative time and Intl-based formats |
| Localization packages (API) | Built-in `Microsoft.Extensions.Localization` (`IStringLocalizer` + `.resx`) with `RequestLocalization` middleware; no third-party localization engine and no JSON resource files server-side |
| Formats | Dates, numbers, relative times and prices go through `hb*` pipes built on `Intl` with the active locale; the built-in `date`, `currency`, `number` and `percent` pipes are not used in templates |
| Currency | One currency per deployment, set by `Pricing:CurrencyCode` (default `NPR`) and exposed by `GET /api/v1/meta`; no conversion. Lakh/crore grouping suits Nepali rupees; English shows the `NPR` code, Nepali shows the localized symbol |
| Text handling | All text columns are `nvarchar`; input is normalized to Unicode NFC and trimmed at the API boundary; database collation `Latin1_General_100_CI_AI`; machine formats (JSON, logs, SQL parameters, cache keys) always use the invariant culture |
| Consistency checks | `npm run i18n:check` runs `@jsverse/transloco-keys-manager` (`extract`/`find` in CI mode) to fail on missing or unused keys and on translation drift; a separate check fails on use of the built-in formatting pipes. `.resx` parity between `en` and `ne` is enforced by tests |

### Time, clock and time zones

| Area | Decision |
|------|----------|
| Storage | Every instant is stored in UTC as `datetime2(3)` through a UTC-enforcing EF value converter; the domain and DTOs use `DateTimeOffset` with offset 0 |
| Clock | `TimeProvider` is the only source of "now" (production: the system clock; tests: `FakeTimeProvider`). `DateTime.Now`, `UtcNow`, `Today`, `DateTimeOffset.Now/UtcNow` and `TimeZoneInfo.Local` are banned by analyzer, so a violation fails the build |
| Wire format | ISO 8601 UTC with a `Z` suffix on output; input instants must carry an offset (or `Z`) and are normalized to UTC; offset-less values are rejected with a validation error |
| Display | The browser's time zone by default (via `Intl`), in the active locale; absolute times show the zone name; relative times ("3 days ago") use `Intl.RelativeTimeFormat`. If the browser cannot report a zone, `Asia/Kathmandu` is used |
| Server zone | Containers run with `TZ=UTC`; logs use UTC timestamps; no server code converts to local time. No wall-clock scheduling: background jobs run on intervals |
| Ordering | Lists sort by `CreatedAt` descending with `Id` as the tie-breaker; concurrency and ordering guarantees never depend on timestamps (they use `rowversion`) |
| Clock skew | Token validation tolerates at most 1 minute; hosts run NTP; the readiness check warns when application and database clocks differ by more than 5 s; idempotency and cache TTLs tolerate small drift by design |
| Future date/time features | Not in the MVP. If added (for example open-house slots), store the local date and time with an IANA zone id and convert with a tested helper |

### Commission engine

| Area | Decision |
|------|----------|
| Semantics | Flat rate chosen by price tier, applied to the whole price; the 50L and 1 crore boundaries are inclusive in the 1.75% band |
| Storage | Snapshot stored on the listing (`CommissionAmount`, `CommissionRatePercent`, `CommissionRateId`); recalculated when the price changes; later rate changes are not retroactive |
| Configuration | `CommissionRates` table (MinPrice, MinInclusive, MaxPrice nullable, MaxInclusive, RatePercent, IsActive, EffectiveFrom, RowVersion; bound model in 01-scope), seeded with the 3 tiers; Admin-only CRUD endpoints; cache invalidation on change |
| Visibility | Exposed only through the owner DTO (`OwnerListingDto`); public DTOs do not contain the field. Admins do not see per-listing commission |

### Listing domain

| Area | Decision |
|------|----------|
| Location | Structured: Province, City, Locality, PostalCode (5-digit Nepal postal code) |
| Property type and features | `PropertyTypes` and `Features` lookup tables; features are many-to-many |
| Other fields | Bedrooms, Bathrooms, AreaSqFt as columns |
| Identifiers | GUID v7 (`Guid.CreateVersion7()`) for every entity primary key and foreign key, generated in the application (index-friendly, not enumerable); lookups keep a unique natural `Code` |
| Lifecycle | `Status` (Draft, Active, Sold, Archived) + soft delete; seekers see only Active; `rowversion` concurrency token |
| Audit columns | Every mutable entity stores `CreatedAt`/`CreatedBy`/`UpdatedAt`/`UpdatedBy` in UTC; soft-deleted entities also store `DeletedAt`/`DeletedBy`. Actors are Auth `sub` values (null = system/seed), populated centrally by an EF `SaveChanges` interceptor using `TimeProvider` and `ICurrentUserService` |
| Images | Bytes and metadata both in MSSQL: `ListingImages` metadata plus a 1:1 `ListingImageData` row holding the `varbinary(max)` blob (max 5 MB each, JPEG/PNG/WebP, max 10 per listing). Access goes through `IImageStorage` (default `SqlImageStorage`), so a disk or object-storage provider can replace it later without touching Application |
| Search | EF Core LINQ with optional filters (location, min/max price, property type, bedrooms, features), paging, sorting, supporting indexes |

### Audit, logs and history

| Area | Decision |
|------|----------|
| Application logs | Serilog structured logs only; no generic `Logs` table in SQL Server |
| Audit trail | Append-only `AuditEvents` for business and security-critical changes (commission-rate changes, admin actions, partner access, forbidden attempts). It stores actor id/type/client, action, entity, correlation id and redacted old/new JSON; never secrets, tokens, emails, phone numbers or commission amounts |
| Listing history | `ListingStatusHistories` records every lifecycle transition (`FromStatus`, `ToStatus`, actor, time, optional reason); `ListingPriceHistories` records price changes with the old and new commission snapshot. Both are append-only and indexed by `(ListingId, ChangedAt)` |
| Commission-rate changes | Admin edits are allowed in place; each change also writes an `AuditEvents` row with old/new values, so the non-retroactive listing snapshots remain explainable without a separate rate-history table |
| Retention | The application never updates or deletes audit/history rows; retention and archival are deployment/operations policy |

### Angular client

| Area | Decision |
|------|----------|
| Framework | Angular, latest stable at project start, versions pinned; standalone components, signals, native control flow, `inject()`, typed reactive forms, OnPush |
| UI toolkit | Angular Material + CDK; responsive; WCAG 2.1 AA target (keyboard, focus, labels, contrast, `aria-live`, reduced motion) |
| Authentication | `angular-auth-oidc-client`: authorization code + PKCE, refresh-token rotation; the bearer token is attached to `/api` calls only |
| API client | Generated from the exported `swagger.json` with `ng-openapi-gen`, committed under `ui/src/app/api/`; an npm script regenerates it and CI fails when it is stale |
| State | Signal-based stores per feature; no NgRx |
| Structure | Feature-based folders, lazy-loaded routes, search state kept in URL query parameters |
| Configuration | Runtime `assets/config.json` (relative `/api` and `/sso`, client id) and `baseHref=/`, so one build runs in every environment |
| Localization | Transloco runtime translations in `en` and `ne`; `@jsverse/transloco-locale` for generic date/number/percent/currency formatting plus `hbPrice`, `hbPriceShort` and `hbRelativeTime` wrappers (lakh/crore, NPR from `/api/v1/meta`, relative time); time zone from the browser (see 4.11 and 4.12) |
| Serving | By the gateway at `/` with SPA fallback; hashed assets cached long, `/index.html` never cached |
| Tests | Unit tests with Vitest (`npm run test`); end-to-end tests are roadmap |
| Accessibility | WCAG 2.1 AA target; automated lint checks plus a manual keyboard/screen-reader pass (4.10) |
| Standards | Section 4.10 |

### Cross-cutting

| Area | Decision |
|------|----------|
| Caching | Tier 1 ships the in-memory provider only; the Redis L2 is added in Phase 14 (Tier 3) behind the same abstraction. `ICacheService` is implemented over the in-memory cache or `IDistributedCache` (`Microsoft.Extensions.Caching.StackExchangeRedis`), chosen by `Cache:Provider`; single-flight stampede protection is provided in the cache services, and token-based invalidation keeps the culture-aware key convention. Cache failures never fail a request (see 4.9) |
| Logging | Serilog in both services; Console (human-readable in Development, compact JSON to stdout elsewhere) + optional rolling file (off in containers); standard in 4.2 |
| Feature flags | `Microsoft.FeatureManagement`, config-backed, 3 flags: `ListingCache` and `ImageUploads` (API), `BrokerSelfRegistration` (SSO) |
| API documentation | Swashbuckle.AspNetCore with XML-comment summaries/remarks, examples, and an OAuth2 (authorization code + PKCE) security scheme; compatibility with .NET 10 verified in the Phase 1 spike, with built-in `Microsoft.AspNetCore.OpenApi` + Scalar or Swagger UI as the fallback |
| Error handling (HTTP) | RFC 7807 `ProblemDetails` via the built-in `AddProblemDetails()` + `IExceptionHandler` pipeline; `ToActionResult` maps expected `Result` outcomes, and one handler maps unexpected exceptions and `DomainException`. No third-party ProblemDetails middleware |
| Rate limiting | Built-in ASP.NET Core rate limiter in each service: global limit plus named policies; configurable; 429 with `Retry-After`; per instance in the app, with a global per-IP limit at the gateway in production (see 4.6) |
| Health checks | `/api/health/live` and `/api/health/ready`; `/sso/health/live` and `/sso/health/ready` |
| Code quality | `AGENTS.md`, `.editorconfig`, analyzers with warnings-as-errors (including security rules), SonarAnalyzer, Central Package Management, architecture tests (NetArchTest), Coverlet coverage (4.4) |
| Security | Controls mapped to the OWASP Top 10:2025 and OWASP API Security Top 10:2023 (4.7) plus SPA rules (4.10) |
| Testing stack | xUnit + Moq + plain xUnit assertions; SQLite in-memory only where DB behavior matters |
| Out of scope | See Section 8 |

### Reliability, scale-out and deployment

| Area | Decision |
|------|----------|
| Correlation id | `X-Correlation-Id` accepted only if it matches `[A-Za-z0-9_-]{1,64}`, otherwise generated; returned on every response, added to every log line and `ProblemDetails`; TraceId logged alongside (4.8) |
| Idempotency | `Idempotency-Key` header on JSON POST endpoints that create resources (listing create, commission-rate create); keys persisted in SQL Server; same key + same body replays the stored response, different body gives 422, in-flight duplicate gives 409; 24 h retention (4.8) |
| Concurrency | Optimistic: `rowversion` exposed as `ETag` / `version`; `If-Match` required on `PUT /listings/{id}` (428 if missing, 412 if stale); commission-tier changes serialized and re-validated as a whole; transient SQL faults retried through the EF execution strategy (4.8) |
| Statelessness | The API keeps no in-process state that affects correctness: no sessions, no sticky sessions, tokens validated through JWKS. The SSO service uses cookies, so its Data Protection keys are stored in its database and shared by all instances |
| Database changes in production | Never applied at API or SSO startup. Each host has a `migrate` command (`dotnet HouseBroker.Api.dll migrate`, `dotnet HouseBroker.Auth.dll migrate`) that applies migrations and the idempotent seed and exits; compose and deployment run it as a one-shot job. Auto-migrate only when `Database:AutoMigrate=true` (Development default) |
| Packaging | Multi-stage Dockerfiles for `api`, `auth` and `gateway` (nginx serving the Angular build at `/` and routing `/api` and `/sso`); non-root; health checks. `docker-compose.yml` for development infrastructure; `docker-compose.prod.yml` for the full topology |
| Config and secrets | Twelve-factor: environment variables override `appsettings`; no secrets in images or repo; options validated on startup (fail fast); SSO signing/encryption certificates and connection strings come from the secret store; signing-key rollover by publishing the new certificate beside the old one |
| Observability | Serilog JSON to stdout; OpenTelemetry traces and metrics over OTLP when `OTEL_EXPORTER_OTLP_ENDPOINT` is set |
| CI | GitHub Actions: .NET job (restore, build, test, format check, vulnerable-package check) and UI job (lint, format check, test, accessibility lint, API-client freshness, build, `npm audit`), plus a Docker build. No registry push or deploy step |

---
