# House Broker Plan — 5. Phased Task Breakdown

## 5. Phased Task Breakdown

### Phase 0: Setup and Quality Foundations (0.5 day)
- [ ] Create repo, `.gitignore`, `.editorconfig`, `global.json`; define commit convention
- [ ] Write `AGENTS.md` (outline in 4.4) with glossary and an Angular section
- [ ] Add analyzers (warnings as errors, SonarAnalyzer), Central Package Management (`Directory.Packages.props`) with lock files, NuGet audit warnings as errors; pin the planned packages (`NetEscapades.AspNetCore.SecurityHeaders`, `Asp.Versioning.Mvc`, `ZiggyCreatures.FusionCache` + `.Serialization.SystemTextJson`; `.Backplane.StackExchangeRedis` and the Redis cache package are pinned in Phase 14)
- [ ] Add npm packages: `@jsverse/transloco`, `@jsverse/transloco-locale`, `@jsverse/transloco-keys-manager`
- [ ] Architecture-test project skeleton

**Exit:** repo initialized; `AGENTS.md` and tooling in place.

### Phase 1: Solution Skeleton and Foundation (1.7 days)
- [ ] Create the solution: 8 src projects (Domain.Shared, Domain, Application.Contracts, Application, Infrastructure, ServiceDefaults, Api, Auth) and 5 test projects (Application.Tests, Api.Tests, Auth.Tests, Architecture.Tests, TestSupport), with references exactly as in 3.1
- [ ] `Directory.Build.props` (nullable, warnings-as-errors, XML docs enabled)
- [ ] `Domain.Shared`: roles, permissions, scopes, limits (max images, image size, page size), `ListingStatus`, error codes
- [ ] `Application.Contracts`: `Result<T>` and `Error`; folder layout for DTOs and service interfaces
- [ ] Add Mapperly (`Riok.Mapperly`) and mapping conventions; mapping profiles live in `Application`; public and owner DTO mappings are explicit
- [ ] `ServiceDefaults`: Serilog setup and enrichers; correlation-id middleware with header sanitization, response header, CORS exposure and a `correlationId` field on `ProblemDetails` (4.8); `AddProblemDetails()` + `IExceptionHandler` with a `DomainException` mapping; security headers via `NetEscapades.AspNetCore.SecurityHeaders`; `ForwardedHeaders` with trusted proxies; `UsePathBase` from configuration; health checks; rate-limit helpers; feature-management wiring (4.3)
- [ ] Api composition root with per-layer `DependencyInjection.cs` extension methods; PathBase `/api`; API versioning with `Asp.Versioning.Mvc` and `v{version}/...` routes; a single `ToActionResult` mapping to status codes and localized `ProblemDetails`; `TimeProvider` registered
- [ ] **Spike:** `NetEscapades.AspNetCore.SecurityHeaders` CSP on both services and `Asp.Versioning.Mvc` under PathBase; record the pinned versions and the CSP directives
- [ ] Localization foundation (4.11): `Domain.Shared` (`SupportedCultures`, default culture, `ErrorCodes`); `Error` carries code + arguments, no message text; `RequestLocalization` with `Accept-Language` in `ServiceDefaults`; `Content-Language` and `Vary` handling; `IStringLocalizer`-based message mapping in `ToActionResult` with English fallback
- [ ] Time foundation (4.12): `TimeProvider` registered in each host; UTC JSON settings (ISO 8601 `Z`, offset-less input rejected); `BannedApiAnalyzers` and culture analyzers (CA1304, CA1305, CA1307, CA1309, CA1310, CA1311) set as errors
- [ ] Auth host skeleton: PathBase `/sso`, health endpoints, shared defaults
- [ ] **Spike:** Swashbuckle on .NET 10 with PathBase (relative swagger endpoint, `/api` server entry); fall back to built-in OpenAPI if incompatible
- [ ] Options classes and validation; user-secrets; Kestrel request-size limits; CORS allowlist from configuration
- [ ] `docker-compose.yml` for development infrastructure (SQL Server; Redis is added in Phase 14)
- [ ] Layer-purpose comment header in each project explaining the layer's responsibility, allowed dependencies and key patterns (README per folder or assembly summary); satisfies raw requirement R15

**Exit:** both services run under their path bases (`/api/health/live`, `/sso/health/live`); Swagger loads under `/api/swagger`; zero warnings; structured logs show correlation ids; the dependency rules pass; `Accept-Language: ne` returns a Nepali message on a sample failing request; a banned time API fails the build.

### Phase 2: Domain and Persistence (1.25 days)
- [ ] Domain entities, enums and DDD-lite behavior methods with invariants (3.7), written test-first where in TDD scope
- [ ] `AppDbContext` (plain EF context) + Fluent API configurations (`decimal(18,2)`, string lengths, FKs, `ListingImageData.Content` as `varbinary(max)`); entity classes `BrokerProfile`, `ListingStatusHistory`, `ListingPriceHistory` and `AuditEvent` mapped to plural PascalCase tables (`BrokerProfiles`, `ListingStatusHistories`, `ListingPriceHistories`, `AuditEvents`)
- [ ] Indexes: `(City, Locality)`, `Price`, `PropertyTypeId`, `Status`, filtered on `IsDeleted = 0`; history indexes on `(ListingId, ChangedAt)` and `AuditEvent.OccurredAt`
- [ ] Global soft-delete query filter; `RowVersion` on `Listing` and `CommissionRate`; GUID v7 primary keys on every table; audit columns (`CreatedAt`/`CreatedBy`/`UpdatedAt`/`UpdatedBy`, plus `DeletedAt`/`DeletedBy` for soft delete) populated by a `SaveChanges` interceptor; EF SQL Server execution strategy
- [ ] UTC persistence (4.12): all instants `datetime2(3)` with a UTC-enforcing value converter and `SYSUTCDATETIME()` defaults; all text columns `nvarchar`; database collation `Latin1_General_100_CI_AI`
- [ ] Lookups: `Code`, `Name` and `SortOrder` on `PropertyTypes` and `Features`; no translation tables; seed names in the default language
- [ ] Repository per aggregate (`IListingRepository`, writes) and a separate read interface `IListingQueries` (Application), implemented in Infrastructure; `IUnitOfWork` translating `DbUpdateConcurrencyException` into a `PreconditionFailed` result; `Listing.Create` factory; `HouseBroker.TestSupport` with `ListingBuilder` and request builders
- [ ] Initial migration; `migrate` command on the Api host
- [ ] `DatabaseSeeder` in Infrastructure (idempotent, upsert by natural key, updating only changed values): master data in every environment — property types, features and the 3 commission tiers; demo broker profiles and listings in Development only (fixed demo user ids from `Domain.Shared`, matching the Auth demo users)
- [ ] Seeder tests: running twice creates no duplicates; master data is present after a clean `migrate`; demo data is skipped outside Development

**Exit:** `dotnet HouseBroker.Api.dll migrate` builds the schema from scratch; master data present; running `migrate` twice is safe; domain tests and the UTC round-trip test green.

### Phase 3: SSO Authorization Server (2.2 days)
- [ ] `AuthDbContext`: Identity (Guid keys), OpenIddict stores, Data Protection keys; initial migration; `migrate` command
- [ ] Identity: password policy (min length 10), unique email, lockout (5 failures / 15 minutes); roles Broker, Seeker, Admin with permission claims from `Domain.Shared`; Admin created from configuration/secrets (no hard-coded password)
- [ ] OpenIddict server: authorization, token, userinfo and logout endpoints under `/sso`; discovery and JWKS; authorization code + PKCE (required), `client_credentials`, refresh tokens (rolling); implicit and password flows disabled; scopes and the `housebroker-api` resource registered; explicit public issuer setting
- [ ] Tokens: asymmetric signing certificates (development certificates locally, mounted certificates in production); access tokens signed, not encrypted; lifetimes 15 min access, 7 days refresh; claim destinations (`sub`, `role`, `permission`, `scp`; `name`, `email`, `phone_number` only when the matching scopes are granted)
- [ ] Razor Pages: login, register (role choice Seeker or Broker, broker needs name and phone, `BrokerSelfRegistration` flag), logout, consent (third-party clients only), access denied; anti-forgery; local-only `returnUrl`; cookies `HttpOnly`, `Secure`, `SameSite=Lax`; framing denied
- [ ] SSO page accessibility basics (R26): labelled inputs, associated error messages with `aria-live`, visible focus, logical heading order, `lang` attribute, sufficient contrast; the full WCAG pass stays in Phase 12
- [ ] Localization: culture from `ui_locales` (then the culture cookie, then `Accept-Language`); `.resx` resources in `en` and `ne` for all pages; a localized `IdentityErrorDescriber`; localized scope descriptions on the consent page; a language selector on the login and register pages; correct `lang` attribute
- [ ] Clients: seed `housebroker-web` (first party, implicit consent), `housebroker-swagger` (Development/Staging), a demo partner (Development); `register-client` command; exact-match redirect URIs from configuration
- [ ] OpenIddict token and authorization pruning through a small interval-based `BackgroundService` calling `IOpenIddictTokenManager.PruneAsync` / `IOpenIddictAuthorizationManager.PruneAsync` (no Quartz dependency); Data Protection keys persisted to the database
- [ ] `auth` rate-limit policy; Information/Warning logs for registration, login, lockout, token issuance, refresh-token reuse (no credentials logged)
- [ ] Demo users (Development): two brokers, one seeker, with fixed ids shared through `Domain.Shared`
- [ ] Auth data seeder (idempotent): roles and permission claims in every environment; demo users and clients in Development only; the production Admin comes from configuration/secrets
- [ ] **Unit tests (Auth.Tests):** claim destinations and scope-to-claim mapping, registration role allowlist, `returnUrl` validation, `register-client` command, `ui_locales` negotiation, `.resx` parity between `en` and `ne`

**Exit:** the authorization code + PKCE flow completes through the Swagger client; `client_credentials` returns a read-scoped token for the demo partner; five wrong passwords lock the account; the discovery document is served under `/sso/.well-known`; the login and consent pages render in Nepali when `ui_locales=ne`.

### Phase 4: API Resource Server and Authorization (0.7 day)
- [ ] **Spike:** token validation against the public issuer while reading discovery over an internal address (OpenIddict validation, falling back to JwtBearer with a metadata address)
- [ ] Validate issuer, audience, lifetime and signature; clock skew at most 1 minute
- [ ] Fallback authorization policy (default deny); `[HasPermission]` policy provider and handler implementing permission ∩ scope (3.6) and the partner-token rules; resource-based ownership handler
- [ ] `ICurrentUserService` (sub, roles, permissions, scopes, client id) in Application
- [ ] `BrokerProfiles` sync from token claims on broker writes; 403 `insufficient_scope` when the claims are missing
- [ ] Swagger OAuth2 security scheme and `housebroker-swagger` integration
- [ ] Unit tests for the handlers and the sync; controller tests with mocked dependencies

**Exit:** a Seeker token cannot call broker endpoints (403); a read-scope partner token cannot write; a broker cannot touch another broker's listing.

### Phase 5: Commission Engine (0.7 day, **test-first**)
- [ ] `ICommissionRateProvider` reading active tiers from the DB through the cache abstraction
- [ ] `CommissionCalculator` (pure, deterministic): tier lookup, inclusive boundaries, rounding; tiers selected by `IsActive` and `EffectiveFrom` against the injected clock
- [ ] `CommissionRates` bound model: `MinPrice`/`MinInclusive`/`MaxPrice?`/`MaxInclusive` (see 01-scope); the calculator matches with the inclusivity flags, never with plain inclusive bounds
- [ ] Tier-configuration validation (first tier starts at 0; adjacent tiers share one boundary value with exactly one side inclusive, so no gaps or overlaps; exactly one open-ended top tier). Scope of the check: the active tier set effective now (`IsActive` and `EffectiveFrom <= now`) is validated on every admin write; a row with a future `EffectiveFrom` is validated together with the set that will be effective at that instant, so a future-dated change can never produce a gap or overlap when it takes effect
- [ ] Tier writes (create/edit) run in a `Serializable` transaction with whole-set re-validation inside it, so two parallel admin edits cannot jointly break the set (the parallel-request check scripts stay in Phase 9)
- [ ] Integration into listing create/update: store `CommissionAmount`, `CommissionRatePercent`, `CommissionRateId`
- [ ] Admin endpoints for rate management with cache invalidation; Swagger docs per 4.1; Information log and append-only `AuditEvents` row (redacted old/new values) on rate change; `write` rate-limit policy
- [ ] **Unit tests:** all three tiers, all boundary values (including `49,99,999.50` and `1,00,00,000.01`), tier-set validation (gap, overlap, double-inclusive boundary, missing open top tier), rounding, no-tier-found error, rate changed in DB gives a new result, price change recalculates, a tier with a future `EffectiveFrom` is ignored (`FakeTimeProvider`)

**Exit:** commission tests green; changing a DB rate changes new calculations without a code change; two parallel tier edits against SQL Server leave a valid tier set (manual check in Tier 1, scripted in Phase 9).

### Phase 6: Listing Management (1.55 days)
- [ ] DTOs and service interfaces in `Application.Contracts`: `CreateListingRequest`, `UpdateListingRequest`, `ListingSummaryDto`, `ListingDetailDto`, `OwnerListingDto` (with commission)
- [ ] FluentValidation validators (title length, price range, valid property type, location, bedrooms/bathrooms ranges, feature ids exist, postal code format (5 digits); HTML tags rejected in free text); request DTOs expose only client-settable fields; `ListingService` written test-first
- [ ] `ListingService`: create, update, soft delete, get by id, get mine; ownership enforcement; status transitions; write `ListingStatusHistories` on lifecycle changes and `ListingPriceHistories` on price/commission changes; each mutation runs in a single transaction; every method returns `Result<T>`
- [ ] Image upload/delete via `IImageStorage` (SQL-backed default) with type, size and count validation; file-signature (magic-byte) check, not just `Content-Type`; server-generated file names; metadata and `varbinary(max)` blob written in one transaction; no orphan metadata row or blob on failure; `upload` rate-limit policy; primary image handling; `ImageUploads` flag; image-serving endpoint with `ETag` and `Cache-Control`
- [ ] Thin controllers (call the service, map through `ToActionResult`) with `ProducesResponseType` and proper status codes (201 + Location, 204, 400, 401, 403, 404, 409 for state conflicts, 412/428 for stale or missing `If-Match`, 429); `write` rate-limit policy; full Swagger docs per 4.1
- [ ] Concurrency (4.8): `version` on detail and owner DTOs; `ETag` on `GET /listings/{id}` and `GET /listings/mine/{id}`; required `If-Match` on `PUT`; image and feature changes touch the listing root
- [ ] Mapping that includes commission only for the owning broker; broker contact read from `BrokerProfile`
- [ ] Localized errors (4.11): validators use `WithErrorCode`; `.resx` messages in `en` and `ne` for every code; text input normalized to NFC and trimmed; instants in DTOs are ISO 8601 UTC and offset-less input is rejected; `Accept-Language` documented in Swagger
- [ ] Information logs for create/update/delete; Warning for forbidden and conflict
- [ ] **Unit tests:** `ListingService` (create computes commission, update recalculates, non-owner forbidden, not-found, validation failures); controller tests with mocked services; `If-Match` missing/stale mapping; localized `ProblemDetails` (en, ne, unsupported falls back); every error code has an English and a Nepali message; Mapperly profiles map owner and public DTOs correctly

**Exit:** a broker can fully manage their listings through Swagger (authorized via `/sso`); the public response never contains commission; a stale `PUT` returns 412.

### Phase 7: Showcase: Search, Filters, Detail (0.9 day)
- [ ] `ListingSearchQuery` (location text, min/max price, property type, bedrooms, features, sort, page/pageSize with a max cap)
- [ ] Query composed with `IQueryable`, projected to summary DTOs (no N+1, `AsNoTracking`); implemented behind `IListingQueries` with small `IQueryable` filter extension methods (no specification framework)
- [ ] Paged response envelope (items, total, page, pageSize)
- [ ] Detail endpoint: description, images, features; broker name/phone/email only when the caller holds `contacts.read`
- [ ] Anonymous lookup endpoints `GET /api/v1/lookups/property-types` and `GET /api/v1/lookups/features` returning default-language names (no translation table in MVP); `GET /api/v1/meta` (currency code, supported cultures, default culture, limits); results sorted by `CreatedAt` descending with `Id` as tie-breaker
- [ ] Query validation (min ≤ max, page bounds, text length cap, LIKE wildcards escaped); `search` rate-limit policy; Debug logging of normalized filters
- [ ] Unit tests for search criteria composition; verify seekers see only `Active` listings; contact hidden without the scope

**Exit:** search by location + price range + type works with paging and sorting; detail returns images, and broker contact only for permitted callers.

### Phase 8: Caching (0.65 day; Tier 1 = in-memory provider)
- [ ] `ICacheService` with `MemoryCacheService` (Tier 1); the `RedisCacheService` L2 and backplane are added in Phase 14 behind the same abstraction and `Cache:Provider` switch; `ListingCache` flag honored; cache errors logged at Warning and fall through to the DB; single-flight loading to prevent stampedes
- [ ] Caching as decorators: `CachedListingQueries` (wraps `IListingQueries`) and `CachedCommissionRateProvider`, registered in DI; `ListingService` contains no cache code
- [ ] Shared contract-test suite written against `ICacheService` (Liskov); run against the Memory provider now and against Redis in Phase 14
- [ ] Cache commission tiers (long TTL), lookups, listing detail (5 min), search results (60 s, keyed by normalized query)
- [ ] Invalidation on listing create/update/delete and rate changes using namespace tokens (a write stores a fresh token that read keys embed; works on both providers)
- [ ] Public and owner DTOs cached under separate keys, contact-bearing responses per scope and localized values per culture, so owner-only or contact fields never reach public cache entries
- [ ] Debug logs for hit/miss
- [ ] Unit tests: hit/miss, invalidation on update, flag-disabled bypass, provider failure falls through to the DB

**Exit:** repeated requests hit the cache; writes invalidate it; disabling the flag bypasses it. (With Redis, a write on one instance invalidating reads on another is added and verified in Phase 14.)

### Phase 9: Idempotency and Concurrency Hardening (0.7 day)
- [ ] `IdempotencyKey` entity (table `IdempotencyKeys`), configuration and migration; `IIdempotencyService` written test-first (4.8)
- [ ] `[Idempotent]` action filter on `POST /listings` and `POST /admin/commission-rates`: key validation, body hash, replay with `Idempotent-Replayed: true`, 409/422 rules; the key and the business change commit in one transaction
- [ ] Hosted service that deletes expired keys in batches (own correlation id per run)
- [ ] Tier changes: the `Serializable` transaction and whole-set validation are built in Phase 5; here add the parallel-edit check script and surface `rowversion` on tiers as `version` with `If-Match` on tier edits
- [ ] Swagger: document `Idempotency-Key`, `If-Match`, `ETag`, and the 412, 422, 428 responses on the relevant endpoints
- [ ] `docs/concurrency-checks.md` with scripted parallel-request checks: two parallel `PUT`s (one gets 412), two parallel uploads at the 10-image limit, two parallel tier edits, a duplicate `POST` with the same key
- [ ] Unit tests: replay, body mismatch, in-flight duplicate, expiry, per-user scope, failure releases the key, tier-set validation logic

**Exit:** retrying a `POST` with the same key never creates a second resource; a stale `PUT` gets 412; the parallel-request checks pass against the SQL Server container.

### Phase 10: Angular Foundation and Sign-In (1.9 days)
- [ ] Create the workspace under `ui/` (strict, routing, SCSS, standalone) with pinned versions and Node; `baseHref=/`; Angular Material theme
- [ ] Tooling per 4.10: strict compiler options, `angular-eslint` + `typescript-eslint`, Prettier, `.editorconfig`, Husky + lint-staged, import-boundary lint rule, banned unsafe-HTML lint rules, build budgets; npm scripts (`lint`, `format:check`, `test`, `build`, `api:generate`, `api:check`)
- [ ] Dev proxy (`proxy.conf.json`) mapping `/api` and `/sso` to the local services; the dev server serves the SPA at `/`
- [ ] Generate the API client from `docs/swagger.json` with `ng-openapi-gen` into `ui/src/app/api/`
- [ ] Core: runtime config loader (`assets/config.json`); OIDC authentication (authorization code + PKCE, refresh-token rotation); interceptors (bearer for `/api` only, correlation id, `ProblemDetails` error mapping, 429 retry-after); global `ErrorHandler`; functional guards (authenticated, role/permission)
- [ ] Layout shell (header, navigation, sign in/out, user menu showing name and role, footer, skip link); loading, empty and error state components; `en-IN` formatting, NPR and lakh/crore pipe
- [ ] Localization (4.11): Transloco with scoped JSON (`en`, `ne`), typed keys, lazy loading and `en` fallback; language selector (saved choice, then browser language, then `en`) with live switching and `<html lang>`; `Accept-Language` interceptor; `ui_locales` on sign-in; error-code to translation mapping in the error interceptor; self-hosted Noto Sans and Noto Sans Devanagari fonts; CSS logical-properties convention
- [ ] Formatting and time (4.12): `hbDate`, `hbDateTime`, `hbRelativeTime`, `hbNumber`, `hbPrice`, `hbPriceShort` pipes on `Intl` using the active locale, the currency from `/api/v1/meta` (default NPR) and the browser time zone (fallback `Asia/Kathmandu`); injectable clock service; `i18n:check` script (key parity, unused/missing keys, banned built-in pipes)
- [ ] Unit tests: interceptors, guards, config loader, auth wrapper, formatting pipes per locale and time zone (Asia/Kathmandu, UTC, America/New_York), language negotiation (test-first)

**Exit:** the SPA signs in through `/sso`, shows the user's name and role, and calls `/api/v1/lookups/*` with the bearer token; lint, format, tests and build pass; switching to Nepali changes UI text, sign-in pages and number/date formats without a reload (lookup names stay default language in the MVP).

### Phase 11: Angular Features (3.3 days)
**Showcase (1.1 days)**
- [ ] Search page: filters (location, price range with lakh/crore display, property type, bedrooms, features), sorting, paging, debounced input, URL-synced state, skeleton and empty states
- [ ] Listing detail page: image gallery (`NgOptimizedImage`, `@defer`), description, features, price; broker contact shown to signed-in users with `contacts.read`, otherwise a "Sign in to view contact" prompt; anonymous browsing works end to end

**Broker area (1.4 days)**
- [ ] My listings: table with status chips and the commission column (shown only here)
- [ ] Create/edit form (typed reactive form, validation mirroring server rules, server field errors mapped onto controls); one `Idempotency-Key` per submission, reused on retry (the API honors it once Phase 9 is delivered; the header is harmless before that); `If-Match` with the loaded `version`; a 412 opens a "changed by someone else, reload" dialog; 429 shows the retry delay
- [ ] Image upload (client-side type, size and count checks, progress, primary image), status transitions, delete with confirmation

**Admin (0.4 day)**
- [ ] Commission rates management (table and form; the server remains authoritative for gap/overlap rules, errors shown inline; `version` handling)

**Localization of features (0.4 day)**
- [ ] All feature text in `en` and `ne` (feature-scoped files); listing times through `hbRelativeTime` with the absolute time and zone in a tooltip; prices through `hbPrice` and `hbPriceShort` (NPR by default); server error codes shown through translations

- [ ] Unit tests for each feature's store, forms and data service

**Exit:** a seeker can search and view listings; a broker can create, edit, publish and delete listings with images and sees commission on their own listings only; an admin can edit commission tiers.

### Phase 12: Angular Quality and Hardening (1 day)
- [ ] Accessibility pass (4.10): skip link, landmarks, heading order, labelled controls, inline errors, focus management, keyboard-only navigation, `aria-live`, contrast, 200% zoom/320 px reflow, reduced motion, image alt text; accessibility lint rules clean; manual keyboard + screen-reader (NVDA/Narrator) pass
- [ ] Localization checks: `i18n:check` in CI; layouts verified in both languages (text expansion, line breaks, Devanagari rendering with a fallback font stack); `lang` attributes correct; accessibility rechecked in both languages; Nepali review notes recorded
- [ ] CSP-compatible production build (no inline scripts); bundle budgets met; `npm audit` with no high or critical findings
- [ ] Coverage report; remaining unit tests; `api:check` passes
- [ ] UI CI job (lint, format check, test, accessibility lint, API-client freshness, build, `npm audit`)
- [ ] `ui/README.md`: setup, scripts, structure, standards summary

**Exit:** all web quality gates are green; the keyboard-only path works for search, detail and the broker form; both languages render correctly.

### Phase 13: Security, Quality and Third-Party Readiness Pass (1 day)
- [ ] Export `swagger.json` to `docs/`; review all summaries, remarks and examples
- [ ] Postman collection: authorization code + PKCE for a user, `client_credentials` for a partner, sample requests
- [ ] `docs/third-party-guide.md`: registering a partner client (`register-client`), requesting tokens, scopes, rate limits, `ETag`/`If-Match` (and `Idempotency-Key` once Phase 9 is delivered), error format and correlation ids
- [ ] Error-code catalog (code, kind, HTTP status, meaning) in `docs/third-party-guide.md`; document `Accept-Language`, `Content-Language`, ISO 8601 UTC instants and the currency setting
- [ ] Consistent error format, configurable CORS policy, documented pagination conventions
- [ ] Architecture tests passing; coverage report; log review for PII and secrets
- [ ] Security review: `dotnet list package --vulnerable --include-transitive` and `npm audit`; walk the checklist in 4.7 for every endpoint, the SSO pages and the SPA; confirm no stack traces in responses, security headers present, 429 behavior correct, no secrets in the repo or logs
- [ ] Review pass: no domain/infrastructure leakage

**Exit:** a third party can integrate using only the OpenAPI document, the guide and the Postman collection; quality tooling is green; the security checklist is clean for every endpoint.

### Phase 14: Scale-Out and Production Readiness (2.0 days)
- [ ] Redis: `RedisCacheService` (FusionCache L2 + backplane) selected by `Cache:Provider`; `docker-compose.yml` gains Redis; run the Phase 8 cache contract suite against it plus a two-node invalidation test
- [ ] `migrate` commands hardened for both hosts; options validation on startup (`ValidateOnStart`); `*_FILE` secret support; SSO certificates from the secret store
- [ ] `/health/live` and `/health/ready` on each service (SQL Server, Redis); status-only output outside Development
- [ ] Request timeouts, DB command timeout, graceful-shutdown settings, response compression; `Cache-Control`/`ETag`/304 for lookups, detail and images
- [ ] Multi-stage Dockerfiles for `api`, `auth` and `gateway` (Angular build + nginx); non-root, `HEALTHCHECK`, `.dockerignore`
- [ ] `docker-compose.prod.yml` and `deploy/nginx.conf`: gateway, 2 API replicas, SSO, both `migrate` jobs (`service_completed_successfully`), SQL Server, Redis; routing, SPA fallback, rate limits, CSP and caching headers per 4.9
- [ ] OpenTelemetry traces and metrics with an OTLP exporter enabled by environment variable
- [ ] `docs/db-roles.sql`: least-privilege logins for the API, SSO and migrator
- [ ] `.github/workflows/ci.yml`: .NET job, UI job, Docker build
- [ ] `docs/deployment.md` runbook (4.9)
- [ ] Container globalization and time: ICU data present and `ne-NP` resources resolve inside the images; `TZ=UTC`; clock-drift readiness warning; `docs/deployment.md` documents NTP
- [ ] Scale-out verification with `--scale api=3`: a cache write on one instance invalidates reads on another; a retried idempotent `POST` routed to a different instance replays; the SSO cookie session and Data Protection keys work across two SSO instances; the API validates tokens through the gateway issuer URL; Swagger, redirects, cookies and SPA routes work under `/`, `/api` and `/sso`; readiness fails when SQL Server is down; a `migrate` failure blocks the services from starting; a signing-certificate rollover keeps tokens valid; a rolling restart drops no requests

**Exit:** `docker compose -f docker-compose.prod.yml up --scale api=3` brings up a working multi-instance stack from a clean checkout and the checks above pass.

### Phase 15: Documentation and Submission (0.5 day)
- [ ] README: prerequisites, clone → `docker compose up` (infrastructure) → `migrate` → run Api, Auth and Angular → test; seeded credentials and demo clients; configuration reference; path bases (`/`, `/api`, `/sso`) and URLs
- [ ] Architecture section: layer diagram, project responsibilities, dependency rules, design decisions (Section 2), topology, the deliberate Clean Architecture exception for the Auth service, and the per-layer comment headers (R15)
- [ ] Commission rules and how to edit tiers
- [ ] Localization and time sections: supported languages (English default, Nepali), how to add a language, the Nepali draft-quality note, the currency setting (NPR default), UTC and ISO 8601 conventions, time-zone display rules, known limitations (broker text not translated, no transliteration, no stored per-user language, left-to-right only)
- [ ] Permission and scope matrix, feature flags, logging and security sections (controls, rate limits, known limitations: per-instance app rate limits, image blobs stored in SQL Server (database and backup growth), tokens in `sessionStorage`, no email confirmation/password reset/MFA)
- [ ] API usage walkthrough (sign in → create listing → search) and links to `docs/third-party-guide.md` and `docs/deployment.md`
- [ ] Known limitations and roadmap (Section 8)
- [ ] Commission section states that the stored amount is a non-retroactive snapshot (the plan's reading of "estimated")
- [ ] Final: clean-clone build, all tests pass, `docker-compose.prod.yml` smoke test with 2 API replicas (only when Tier 3 is delivered), dead code removed

**Exit:** a reviewer can run the system and its tests by following the README alone.
