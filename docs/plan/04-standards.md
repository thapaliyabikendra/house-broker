# House Broker Plan — 4. Cross-Cutting Standards

## 4. Cross-Cutting Standards

### 4.1 Swagger / OpenAPI documentation

Every endpoint has:
- `<summary>`: one line stating what it does
- `<remarks>`: details covering who may call it (role, permission and scope), business rules, side effects (cache invalidation, commission recalculation), pagination and filter semantics, and a request example
- `<param>` docs and a `<response code="...">` for each status code, backed by `[ProducesResponseType]` (including `ProblemDetails` for 400/401/403/404/409/412/428/429)
- Grouped, ordered tags: Listings, Images, Lookups, Admin: Commission Rates

Also:
- DTO properties are documented with constraints (min/max, length, format); enums render as strings
- Owner-only endpoints state that `commissionAmount` is included and visible only to the owning broker
- `Idempotency-Key`, `If-Match` and `ETag` are documented where they apply
- `Accept-Language` is documented as a global header parameter and `Content-Language` on responses; the error-code catalog is linked from the document description; instants are documented as ISO 8601 UTC
- The document has a title, description, version, contact, the `/api` server entry, and an OAuth2 security scheme (authorization code + PKCE) listing each scope with a description, so "Authorize" works through `/sso`
- Missing XML comments (CS1591) are build errors for controllers and DTOs
- `swagger.json` is exported to `docs/`; the Angular client is generated from it

### 4.2 Logging (Serilog, both services)

| Level | Used for | Examples |
|-------|----------|----------|
| Debug | Diagnostics, off outside Development | cache hit/miss and key, chosen commission tier id, normalized search filters, EF commands |
| Information | Business events and one summary line per request | user registered (UserId, Role), login succeeded, token issued (client id, grant type), listing created/updated/deleted (ListingId, BrokerId), commission rate changed (AdminId, tier id; old and new values are written to AuditEvents only, redacted) |
| Warning | Abnormal but handled | login failed (no credentials logged), lockout, forbidden access attempt, concurrency conflict, rate limit hit, refresh-token reuse |
| Error | Failed operations and unhandled exceptions | `IExceptionHandler`, storage or DB failure, invalid commission-tier configuration |
| Fatal | Startup failures | missing signing certificate, DB unreachable at boot |

- **Enrichers:** Service name, Environment, MachineName, request culture, `CorrelationId` (rules in 4.8), TraceId/SpanId, `UserId` (`sub`), `UserRole`, `ClientId`, ClientIp, request method/path via `UseSerilogRequestLogging`
- **Rules:** message templates with named properties (never string interpolation); timestamps are always UTC and log messages are English only (never localized); an exception is logged once, in the middleware, not log-and-rethrow; never log passwords, tokens, email or phone; commission amounts are logged at Debug at most; log levels per namespace in `appsettings.{Environment}.json`
- **No database logs table:** Serilog is the application log. The `AuditEvents` table is a separate business/security audit trail (redacted old/new values), not a sink for application logs

### 4.3 Feature flags

Config-backed via `Microsoft.FeatureManagement`, defined in `appsettings.json`, all enabled by default:

| Flag | Service | Effect when disabled |
|------|---------|----------------------|
| `ListingCache` | API | Cache is bypassed (kill switch for debugging stale data) |
| `ImageUploads` | API | Upload endpoints return 503 `ProblemDetails`; existing images still served |
| `BrokerSelfRegistration` | SSO | Registering as Broker is rejected; Seeker registration still works |

Each flag is documented in the README. If time is short, this is the first item to cut.

### 4.4 Code quality

- `AGENTS.md` (created in Phase 0) contains: project purpose and glossary; architecture and dependency rules (3.1); commands (build, test, format, `dotnet ef`, `migrate`, docker compose, npm scripts); coding conventions; domain rules; testing rules; API rules; logging and security rules; an Angular section pointing to 4.10; definition of done; never-do list
- Conventions: nullable on; async with `CancellationToken`; no business logic in controllers; constructor injection; Mapperly source-generated mapping (no reflection-based mappers); never return entities from controllers; no `[AllowAnonymous]` without a comment explaining why; inject `TimeProvider` instead of `DateTime.Now/UtcNow`; no `.Result`/`.Wait()`; no raw SQL string concatenation; keep the API stateless (no in-process state that affects correctness); new create-`POST` endpoints support `Idempotency-Key` and mutating `PUT`s require `If-Match`; services return `Result<T>` for expected outcomes; add a pattern only where it removes duplication or isolates a named seam (3.8); prefer composition over inheritance; interfaces only at layer boundaries or real test seams; DI only, no static singletons; all user-facing text comes from resources or translation keys (never hard-coded); culture-sensitive formatting always passes an explicit culture (invariant for machine formats, the request culture for human text); identifiers are compared with `StringComparison.Ordinal`/`OrdinalIgnoreCase`; no wall-clock scheduling
- `AGENTS.md` guides an AI coding assistant, so enforcement is done by tooling: `.editorconfig`, `TreatWarningsAsErrors`, Roslyn and SonarAnalyzer rules (cognitive-complexity rule at its default threshold), architecture tests, and `dotnet format --verify-no-changes`
- **Build hygiene:** `Directory.Packages.props` (Central Package Management) with lock files; NuGet audit on, with vulnerability warning NU1901 treated as an error; NU1902–NU1904 are deliberately not errors (transitive .NET 10 framework advisories not yet resolvable by top-level bumps; see `api/Directory.Build.props`) and are reported by the `dotnet list package --vulnerable` gate; `AnalysisLevel` latest-recommended with `EnforceCodeStyleInBuild`; `BannedApiAnalyzers` (`BannedSymbols.txt`) forbidding `DateTime.Now/UtcNow/Today`, `DateTimeOffset.Now/UtcNow` and `TimeZoneInfo.Local`; culture analyzers CA1304, CA1305, CA1307, CA1309, CA1310 and CA1311 as errors
- **Quality tests (architecture test project):** the dependency table in 3.1; every controller action has an explicit `[Authorize]`/policy or `[AllowAnonymous]`; no public DTO exposes a `Commission*` property; controllers reference only Contracts; Mapperly profiles live in Application
- Mapping tests cover each Mapperly profile, including owner vs public DTOs; generated mapping is excluded from coverage
- Coverage is reported with Coverlet; about 80% on Domain + Application is a guide, not a gate
- **Definition of done:** builds with zero warnings, tests green, XML docs present, migration included, README updated; for web changes also lint, format check, tests, accessibility lint, and a regenerated API client
- Angular standards are in 4.10

### 4.5 TDD workflow

- **Strict red-green-refactor:** commission calculator, domain invariants, validators, `ListingService`, `IdempotencyService`, claim-destination and scope-mapping rules in Auth, Angular stores, interceptors and formatting pipes
- **Test-after:** controllers, caching, infrastructure wiring, Angular components
- **Not unit-tested:** EF configuration and migrations (verified by running against the SQL Server container)

### 4.6 Rate limiting

Each service uses the built-in `Microsoft.AspNetCore.RateLimiting` middleware. All numbers live in a `RateLimiting` section of `appsettings.json` so they can be tuned without code changes.

| Service | Policy | Applied to | Partition | Default limit |
|---------|--------|-----------|-----------|---------------|
| API | Global | Every endpoint except `/health/*` | User id if authenticated, otherwise client IP | 100 requests/min (sliding window) |
| API | `search` | `GET /listings`, lookups | Client IP | 60 requests/min (sliding window) |
| API | `write` | Listing create/update/delete, admin rate changes | User id | 30 requests/min |
| API | `upload` | Image upload | User id | 10 requests/min |
| SSO | `auth` | Login, register, token endpoint | Client IP | 5 requests/min for login and register (fixed window); 30/min for the token endpoint |
| SSO | Global | All other SSO endpoints | Client IP | 100 requests/min |

- Rejections return **429** as `ProblemDetails` with `Retry-After`, plus a Warning log (policy name, partition type, CorrelationId)
- Identity lockout complements the `auth` limit: 5 failed logins lock the account for 15 minutes
- `ForwardedHeaders` is configured with an explicit trusted-proxy list so a client cannot spoof `X-Forwarded-For` to dodge IP partitioning
- Limits apply per application instance, so the effective limit grows with the number of replicas; in production the gateway enforces the global per-IP limit in front of all instances. A distributed limiter is roadmap
- Every limited endpoint documents the 429 response in Swagger; the Angular client shows the retry delay

### 4.7 Security (OWASP)

Controls are mapped to the OWASP Top 10:2025 and the OWASP API Security Top 10:2023. The last column names the phase that delivers each control.

**OWASP Top 10:2025**

| Risk | Controls | Phase |
|------|----------|-------|
| A01 Broken Access Control (incl. BOLA/BFLA) | Fallback authorization policy requires an authenticated user (default deny); `[AllowAnonymous]` only on the endpoints marked Anonymous in 3.4; permission ∩ scope check on every action (3.6); resource-based ownership check on every listing mutation and on `mine`; separate public and owner DTOs; partner tokens limited to read scopes; architecture test that every action has explicit auth metadata | 4, 6 |
| A02 Security Misconfiguration | HTTPS redirection + HSTS; `NetEscapades.AspNetCore.SecurityHeaders` applies the security headers on both services (`X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy`, restrictive `Content-Security-Policy` with `frame-ancestors 'none'`, `Cache-Control: no-store` on authenticated responses); `Server` header removed; CORS allowlist from config for third-party browser clients (never `*` with credentials); first-party client is same-origin; Swagger UI and detailed errors only in Development/Staging; secrets only in user-secrets, environment or secret store; Kestrel request-size and header limits; demo data and demo clients seeded only in Development; SPA served with a strict CSP | 1, 2, 14 |
| A03 Software Supply Chain Failures | Central Package Management, lock files, NuGet audit as errors, `dotnet list package --vulnerable --include-transitive`; `npm ci` with lockfile, `npm audit` gate, minimal dependencies, pinned versions, only actively maintained packages | 0, 12, 13 |
| A04 Cryptographic Failures | HTTPS only; Identity's salted password hashing; client secrets stored hashed by OpenIddict; asymmetric token signing with certificates from the secret store; access tokens carry only `sub`, role, permission, scope and (when granted) profile claims; no PII in logs | 1, 3 |
| A05 Injection (incl. XSS) | EF Core parameterized queries only (raw SQL forbidden by convention and analyzers); FluentValidation length limits and allowlists on every input including search text; LIKE wildcards escaped; free-text fields normalized to Unicode NFC and stored as plain text with HTML tags rejected; Angular's default encoding with `innerHTML` and `bypassSecurityTrust*` banned by lint | 6, 7, 12 |
| A06 Insecure Design | Abuse cases reviewed up front: password guessing, listing spam, commission scraping, enumeration, upload abuse, token theft. Mitigated by rate limits, owner-only DTOs, scope limits, page-size caps, upload limits, consent for third-party apps; broker sign-up switchable by flag | 3–7 |
| A07 Authentication Failures | Authorization code + PKCE only; exact-match redirect URIs; `state` and `nonce` validated; 15-minute access tokens with rotating refresh tokens; password policy (min length 10); lockout after 5 failures for 15 min; rate-limited login and token endpoints; generic login error; role allowlist at registration (no `Admin`); local-only `returnUrl` (no open redirects); SSO cookies `HttpOnly`, `Secure`, `SameSite=Lax`; API validates issuer, audience, lifetime and signature with clock skew of at most 1 minute | 3, 4 |
| A08 Software or Data Integrity Failures | Explicit request DTOs (no mass assignment of `BrokerId`, `Status` or commission fields); `System.Text.Json` only (no polymorphic type-name deserialization); uploads verified by file signature (magic bytes); signed tokens only; migrations applied only by `migrate` jobs outside Development | 2, 6, 14 |
| A09 Logging & Alerting Failures | Security events logged with CorrelationId in both services: failed login, lockout, token issuance, forbidden access, rate-limit hit, refresh-token reuse, concurrency conflict, commission-rate changes; no secrets or PII; business/security-critical events also write the append-only `AuditEvents` table; alerting is out of scope | 1, 3, 13 |
| A10 Mishandling of Exceptional Conditions | Global middleware returns `ProblemDetails` without stack traces or internals outside Development; fail closed (any authorization error denies); each listing mutation runs in one transaction, so a listing is never saved without its commission snapshot; image upload leaves no orphan metadata row or blob on failure; consistent 400/409/412/500 mapping | 1, 6 |

**OWASP API Security Top 10:2023 (items not already covered above)**

| Risk | Controls |
|------|----------|
| API3 Broken Object Property Level Authorization | Commission fields exist only on `OwnerListingDto`; update requests cannot set server-owned properties; reflection test that public DTOs have no `Commission*` property |
| API4 Unrestricted Resource Consumption | Rate limits (4.6), page-size cap, search text length cap, upload size and count limits, request body size limit |
| API6 Unrestricted Access to Sensitive Business Flows | `auth` and `write` rate limits; `BrokerSelfRegistration` flag |
| API7 Server-Side Request Forgery | The API never fetches user-supplied URLs; images are uploaded, not linked. The only outbound call is to the configured issuer |
| API9 Improper Inventory Management | URL versioning; exported `swagger.json` lists every endpoint including Admin; no undocumented routes; clients and scopes documented in the third-party guide |
| API10 Unsafe Consumption of APIs | The API consumes no third-party APIs |

**Security checklist** (used in the Phase 13 review):
- Every new endpoint has explicit auth, a permission and a scope
- Ownership is checked for any resource addressed by id
- Request DTOs have validation and length limits; response DTOs expose nothing owner-only to the public
- No secret, token, PII or commission amount above Debug level in logs
- No raw SQL concatenation; new NuGet and npm packages are audited
- Errors return `ProblemDetails` only
- SPA: no unsafe HTML, no tokens in URLs or logs

### 4.8 Correlation id, idempotency and concurrency

**Correlation id**
- `X-Correlation-Id` is accepted only if it matches `^[A-Za-z0-9_-]{1,64}$` (prevents log injection); otherwise a new id is generated. The Angular client generates one per request and shows it in error messages
- When a W3C `traceparent` header is present, the TraceId is logged as well; with OpenTelemetry enabled the correlation id defaults to the TraceId
- Returned in the `X-Correlation-Id` response header (exposed to browsers via CORS `Access-Control-Expose-Headers`), in a `correlationId` extension on every `ProblemDetails`, and in every log line through `LogContext`
- Returned to callers and logged (Serilog, in-memory scope); not persisted in `IdempotencyKeys` or history rows. `AuditEvents.CorrelationId` is kept because that table is the durable audit trail
- Background work (key cleanup, token pruning) generates its own id per run

**Idempotency**
- Scope: JSON `POST` endpoints that create resources, namely `POST /listings` and `POST /admin/commission-rates`. `PUT` and `DELETE` are idempotent by design. Not applied to multipart image upload (bounded by the 10-image limit)
- The client sends `Idempotency-Key` (16–64 characters, `[A-Za-z0-9_-]`). It is optional: without it the request behaves normally. The Angular client sends one key per form submission and reuses it on retry
- Storage: the `IdempotencyKeys` table in SQL Server, so any instance can replay; the unique index on `(UserId, Key)` arbitrates races; keys are scoped per user (`sub`)
- Flow: insert an InProgress row → run the action → store the status code and body in the same transaction as the business change → later requests with the same key and body hash receive the stored response with an `Idempotent-Replayed: true` header
- Same key with a different body returns `422`; same key still InProgress returns `409` with `Retry-After`; keys older than 24 hours are treated as new. Only successful (2xx) responses are stored; a failed attempt releases the key
- Replays still pass through authorization and rate limiting
- Cleanup: a hosted service deletes expired keys in small batches; safe with several instances
- Tests (test-first): first call stores, replay returns the same response, body mismatch returns 422, concurrent duplicate returns 409, expiry, per-user scope, failure releases the key

**Concurrency**
- `Listing` and `CommissionRate` carry a SQL Server `rowversion`, exposed as an opaque `version` on `ListingDetailDto`, `OwnerListingDto` and the rate DTO, and as the `ETag` header on `GET /listings/{id}` and `GET /listings/mine/{id}`
- `PUT /listings/{id}` requires `If-Match`: missing gives `428 Precondition Required`, stale gives `412 Precondition Failed` (the ProblemDetails carries the current version). `DELETE` honors `If-Match` when sent
- Aggregate boundary = consistency boundary: adding or removing an image or feature touches the `Listing` root (new `rowversion`), so the 10-image limit and the single-primary-image rule hold under parallel uploads
- Commission-tier changes run in one `Serializable` transaction that re-validates the whole active band set (at least one band, exactly one open-ended top band, strictly increasing positive maxima)
- A listing update that races with a tier change keeps the snapshot it computed (commission is not retroactive by design)
- Duplicate registrations rely on Identity's unique email index; a unique-violation maps to a validation error
- EF Core's SQL Server execution strategy (`EnableRetryOnFailure`) retries transient faults, and transactions run through the strategy
- Cache stampede protection: single-flight loading per key for commission tiers and listing detail
- Verification: unit tests for `If-Match` parsing and result mapping; scripted parallel-request checks against the SQL Server container in `docs/concurrency-checks.md` (`rowversion` is a SQL Server feature, so SQLite cannot prove it)

### 4.9 Scale-out and production deployment

**Stateless services: N identical instances behind the gateway**

| Concern | Design |
|---------|--------|
| API authentication | Access tokens validated locally from the issuer's cached JWKS; no session, no sticky sessions |
| SSO sessions | Cookie-based, so Data Protection keys are stored in the Auth database and shared by every SSO instance; OpenIddict state is in the same database |
| Cache | `ICacheService` is implemented over the in-memory cache or `IDistributedCache` (`Microsoft.Extensions.Caching.StackExchangeRedis`). `Cache:Provider=Memory` is L1-only (development or a single instance); `Cache:Provider=Redis` adds the Redis L2 cache, giving cross-instance invalidation through the token convention. Invalidation keeps the namespace-token convention: a write stores a fresh token that read keys embed, so it works on both configurations. Cache errors are logged at Warning and fall through to the database. Keys include the request culture wherever the value is localized |
| Idempotency and concurrency | Held in SQL Server, so any instance can serve a retry or detect a conflict |
| Rate limiting | Per-instance in the apps; the gateway also enforces a global per-IP limit; documented limitation |
| Images | Stored in SQL Server as `varbinary(max)` (`ListingImageData`, 1:1 with `ListingImages`); served by an API endpoint that streams the blob with `Content-Type`, `Content-Length`, `Cache-Control` and an `ETag` derived from the content hash. Any API instance can serve any image; no shared volume |
| Database changes | `migrate` one-shot jobs run before the services start; compose enforces the order with `depends_on: condition: service_completed_successfully` |
| Background work | Only idempotent jobs (idempotency-key cleanup, OpenIddict token pruning), so every instance may run them |
| Time and ids | Ids generated in the app (GUID v7); times from `TimeProvider` in UTC |

**Production readiness**
- **Containers:** multi-stage Dockerfiles for `api`, `auth` and `gateway` (Angular build + nginx); non-root user; ICU globalization data present (invariant-globalization mode off, so `ne-NP` formatting and resources work); `TZ=UTC`; `.dockerignore`; no secrets baked in; `HEALTHCHECK` on the live endpoints; read-only root filesystem with a writable volume only for temp files
- **Compose topologies:** `docker-compose.yml` for development infrastructure (SQL Server; Redis from Phase 14). `docker-compose.prod.yml` runs the gateway, 2 API replicas, 1 SSO instance, both `migrate` jobs, SQL Server and Redis. Scale demo: `docker compose -f docker-compose.prod.yml up --scale api=3`
- **Gateway (`deploy/nginx.conf`):** routes `/api/` and `/sso/` with the prefix preserved; serves the SPA at `/` with fallback to `/index.html`; `limit_req` per IP (stricter on `/sso/connect/token` and `/sso/account/login`); request-size limit; forwarded headers; CSP and security headers for the SPA; hashed assets cached long, `/index.html` never cached; TLS termination point
- **Probes:** `/health/live` (process only) and `/health/ready` (SQL Server, Redis if configured, and a clock-drift check against the database that only warns above 5 s) on each service; only a status is returned outside Development
- **Config:** environment variables override `appsettings` (`Section__Key`); `appsettings.Production.json` holds no secrets; options validated on startup with `ValidateOnStart` (a missing certificate or connection string logs Fatal and exits); required variables tabulated in `docs/deployment.md`
- **Secrets:** environment variables or Docker/orchestrator secrets (`*_FILE` supported for connection strings and certificate passwords); SSO signing and encryption certificates mounted from the secret store; rollover = add the new certificate, wait for the API's JWKS cache to refresh, then retire the old one
- **Resilience:** request timeouts (default 30 s, longer for uploads), DB command timeout, `CancellationToken` flowing from `RequestAborted` to EF, graceful shutdown aligned to the gateway's drain period
- **Performance:** response compression for JSON; `Cache-Control` and `ETag` with 304 for lookups, listing detail and images; `AsNoTracking` reads; the indexes from Phase 2
- **Database:** one database (`HouseBroker`); least-privilege logins scripted in `docs/db-roles.sql`: the API login (read/write on `dbo`), the SSO login (read/write on `identity`), and a migrator login with schema rights used only by the `migrate` jobs; backup and restore procedure documented
- **Observability:** compact JSON logs to stdout in containers; OpenTelemetry traces and metrics (ASP.NET Core, HttpClient, SqlClient, runtime) over OTLP when configured; log fields include service, CorrelationId, TraceId and host name
- **CI:** `.github/workflows/ci.yml` runs the .NET and UI jobs listed in Section 2 and a Docker build; no registry push or deploy step
- **Runbook (`docs/deployment.md`):** topology diagram, variable table, first deploy, rolling update (run the `migrate` jobs, then roll the services; migrations must stay backward compatible with the previous version), rollback (restore the backup and redeploy the previous images), scaling, certificate rotation, partner onboarding, troubleshooting by CorrelationId

### 4.10 Angular code standards

**Language and compiler**
- TypeScript `strict` plus `noImplicitOverride`, `noImplicitReturns`, `noFallthroughCasesInSwitch`, `noPropertyAccessFromIndexSignature`; Angular `strictTemplates` and `strictInjectionParameters`
- No `any` (lint error); no non-null assertion (`!`) without an explanatory comment

**Structure and naming**
- Follow the official Angular style guide for the pinned version, as generated by the CLI; kebab-case file names; one concept per file; component selectors prefixed `hb-`
- Folders: `core/` (singletons: auth, config, interceptors, error handling), `shared/` (presentational components, pipes, directives), `features/<name>/{pages,components,data,models}`, `api/` (generated, never edited by hand)
- A feature never imports from another feature; shared code moves to `shared/` or `core/`. Enforced by an ESLint import-boundary rule (verified in Phase 10)

**Components and templates**
- Standalone components only, `ChangeDetectionStrategy.OnPush`, signals (`signal`, `computed`, `input()`, `output()`, `model()`), `inject()` instead of constructor injection, native control flow (`@if`, `@for` with `track`, `@defer` where it helps)
- Container (smart) components own data and routing; presentational components take inputs and emit outputs
- No logic-heavy templates and no function calls in templates other than signals; derive values with `computed`
- Prefer `toSignal` and the `async` pipe over manual subscriptions; use `takeUntilDestroyed` when a subscription is unavoidable

**Services, state and HTTP**
- Feature data services wrap the generated API services; components never inject `HttpClient`
- Signal-based stores per feature hold state and expose read-only signals and intent methods
- RxJS: no nested subscribes, proper flattening operators, cancel on destroy, errors handled in one place
- Interceptors: auth (bearer for `/api` only), correlation id, `ProblemDetails` error mapping, retry-after handling for 429; a global `ErrorHandler`; errors are never swallowed and user-facing messages include the CorrelationId

**Forms and routing**
- Typed reactive forms; one shared helper produces validation messages; server field errors from `ProblemDetails` are mapped onto controls; client-side rules mirror the server's but the server stays authoritative
- Lazy routes with `loadComponent`/`loadChildren`, functional guards and resolvers, route titles, `withComponentInputBinding`; guards improve UX only, the API enforces access

**Lint, format and tooling**
- `angular-eslint` (recommended + template + accessibility rules), `typescript-eslint` strict, Prettier, `.editorconfig`; `no-console` except warn/error; `innerHTML` and `bypassSecurityTrust*` banned
- Husky + lint-staged run lint and format on commit; CI gates: lint, `format:check`, tests, build, `api:check` (generated client is current), `npm audit` (no high or critical)
- Node version pinned (`.nvmrc` and `engines`); `npm ci` only; Angular packages pinned to one version line; build budgets set in `angular.json` (initial bundle: 750 kB raw warning, 1 MB error; the Angular, Material/CDK and Transloco baseline is about 650 kB raw, about 160 kB transferred, so the earlier 500 kB limit could not be met without dropping shared components)

**Testing**
- TestBed with `HttpTestingController` or provider mocks; test behavior, not implementation; Arrange-Act-Assert; stores, interceptors, guards, pipes and forms are tested first-class; no snapshot-only tests; coverage reported, about 80% on `core/` and `features/*/data` as a guide

**Accessibility (WCAG 2.1 AA target)**
- Semantic landmarks, one `h1` per page, logical heading order, a "skip to content" link, and meaningful page titles
- Every form control has a persistent visible label (not placeholder-only), programmatic association and inline error text; errors are announced and focus moves to the first invalid field
- Full keyboard operation with a visible focus ring; no keyboard traps; dialogs and menus manage focus on open/close and restore it on exit
- Async status, validation summaries and toast messages use `aria-live`; loading states are announced; icons have accessible names or are hidden
- Color is never the only signal; contrast meets AA (4.5:1 text); layout works at 200% zoom and 320 px reflow; `prefers-reduced-motion` disables non-essential animation
- Alt text for listing images (decorative images marked empty); accessible names for icon buttons; target size at least 24×24 px
- The SSO login, register, consent and error pages follow the same rules (labels, focus, errors, contrast, language attribute)
- `angular-eslint` accessibility rules are clean; a manual keyboard + screen-reader pass (NVDA or Narrator) is part of Phase 12

**Security**
- The OIDC library keeps tokens in `sessionStorage`; this is mitigated by a strict CSP, Angular's default encoding, banned unsafe-HTML APIs and dependency audits. A backend-for-frontend cookie pattern is roadmap (deferred)
- No secrets in the bundle; tokens never in URLs or logs; all URLs relative (`/api`, `/sso`), with the bearer attached to `/api` only

**Localization and time**
- Transloco with JSON files per language (`en`, `ne`) and feature scope, typed keys, lazy loading per route; fallback language `en`; missing keys log a warning in development. Generic date/number/percent/currency formatting comes from `@jsverse/transloco-locale` (`translocoDate`, `translocoDecimal`, `translocoPercent`, `translocoCurrency`)
- No hard-coded user-facing text in templates or TypeScript; server errors are shown by translating the `code`, falling back to the server message
- Language selector in the header; the choice is stored in `localStorage`, applied live and reflected in `<html lang>`; an interceptor sets `Accept-Language`; `ui_locales` is passed on sign-in
- Formatting only through `@jsverse/transloco-locale` (`translocoDate`, `translocoDecimal`, `translocoPercent`, `translocoCurrency`) plus `hbRelativeTime`, `hbPrice` and `hbPriceShort` (relative time; lakh/crore with translated unit labels). The `hb*` wrappers read the active locale and the currency from `/api/v1/meta` (default `NPR`)
- Dates arrive as ISO 8601 UTC and are displayed in the browser's time zone (fallback `Asia/Kathmandu`); absolute times show the zone name; no manual offset arithmetic; "now" comes from an injectable clock service so tests can fix time
- CSS logical properties (`margin-inline`, `padding-inline`, `inset-inline`) instead of left/right; plurals and interpolation through the translation library, never string concatenation
- Fonts are self-hosted (Noto Sans and Noto Sans Devanagari subsets) so the CSP needs no external font host
- `npm run i18n:check` (a CI gate) runs `@jsverse/transloco-keys-manager` (`extract` and `find` in CI mode) to verify key parity and report unused or missing keys; a small lint/check rejects the built-in `date`, `currency`, `number` and `percent` pipes in templates

**Performance and formatting**
- Lazy-loaded features, `NgOptimizedImage` for listing photos, debounced search input, `@defer` for the image gallery, stable `track` expressions
- Locale, currency and date rules are in the Localization and time block above

### 4.11 Localization

**Negotiation**
- Supported cultures: `en` (default, formatting `en-IN`) and `ne` (formatting `ne-NP`), defined once in `Domain.Shared` and overridable through `Localization:SupportedCultures`; the Angular client reads the list from `/api/v1/meta`
- API: `RequestLocalization` in ServiceDefaults with the `Accept-Language` provider; unsupported or malformed values fall back to the default culture; the chosen culture is logged and returned in `Content-Language`; responses that depend on language carry `Vary: Accept-Language`
- SSO: culture from `ui_locales` (stored in a culture cookie scoped to `/sso`), then `Accept-Language`, then the default

**API messages**
- Application never builds user-facing text. `Error` = code + arguments; validators use `WithErrorCode`; domain exceptions carry codes
- Backend resources are `.resx`, never `en.json`: `IStringLocalizer` resources in the Api (`Resources/Errors.resx` plus `Resources/Errors.ne.resx`) map each code to a message with named placeholders; a missing translation falls back to English, never to the raw key. This is the built-in `Microsoft.Extensions.Localization` stack, with no custom localization engine
- `ProblemDetails` uses the built-in `AddProblemDetails()` and `IExceptionHandler` pipeline: `title` and `detail` localized; extensions `code`, `correlationId`, `errors` per field as `{ code, message, args }` (the application returns stable codes and raw arguments; the API formats the arguments for the request culture, with lakh grouping, and localizes the message with them), and `relatedIds` (ids of existing records that must be fixed, used by commission tier conflicts; the same rule-level failure is a 409 `conflict` in that case, and `detail` then names those tiers' ranges in a localized sentence). Expected `Result` outcomes are mapped by `ToActionResult`
- Response headers: `Content-Language: <culture>` and `Vary: Accept-Language` on responses whose body depends on the request language
- The `ErrorCodes` catalog lives in `Domain.Shared`; `docs/third-party-guide.md` lists each code with its kind and meaning; a unit test fails if any code lacks an English or Nepali message

**Lookups and content**
- `PropertyTypes` and `Features` have a stable `Code` and a default-language `Name`; lookup names are not translated in the MVP, so listing DTOs carry `id`, `code` and `name`; filters and writes use ids
- Cache keys and cached results include the culture; the server sorts lookups by `SortOrder`, not by translated name
- Broker-written text (title, description, locations) is stored and returned as written, with no machine translation; location search is case- and accent-insensitive but does not transliterate between scripts (documented limitation)

**SSO pages**
- Razor Pages use `IStringLocalizer`/`IViewLocalizer` with `.resx` (en, ne); Identity validation errors come from a localized `IdentityErrorDescriber`; the consent page shows localized scope descriptions; the login and register pages have a language selector that sets the culture cookie

**Text handling**
- All text columns are `nvarchar`; input is normalized to Unicode NFC and trimmed at the API boundary; length limits are counted in UTF-16 units (documented, with Devanagari combining marks in mind) and validation messages state the limit
- Machine formats (JSON, logs, SQL parameters, cache keys) use the invariant culture; culture-sensitive APIs always get an explicit culture (analyzers enforce this)

**Quality**
- Parity tests: `.resx` English vs Nepali, UI JSON English vs Nepali (also checked by `@jsverse/transloco-keys-manager` in CI); every `ErrorCodes` entry resolves in both cultures
- API tests for negotiation (en, ne, unsupported falls back), localized `ProblemDetails`, lookups in default language, `Vary` and `Content-Language`; SSO tests for `ui_locales`; Angular pipe tests per locale
- Nepali translations are drafts, flagged in the README. To add a language: copy the English files, translate, add the culture to configuration, run the parity checks

### 4.12 Clock and time zones

- **One clock:** `TimeProvider.System` is registered in every host and injected wherever "now" is needed (services, idempotency, key cleanup, tier selection by `EffectiveFrom`). Domain factories and methods take a `DateTimeOffset now` argument instead of reading a clock
- **Banned APIs:** `DateTime.Now/UtcNow/Today`, `DateTimeOffset.Now/UtcNow` and `TimeZoneInfo.Local` fail the build (`BannedApiAnalyzers`)
- **Persistence:** `datetime2(3)` in UTC. The EF value converter turns a `DateTimeOffset` of any offset into a UTC `DateTime` (Kind = Utc) and back to offset 0; a unit test round-trips values given with several offsets. Database defaults use `SYSUTCDATETIME()` only as a safety net
- **JSON:** one shared setting writes every `DateTimeOffset` as ISO 8601 with `Z` and millisecond precision; instants without an offset are rejected with a validation error code; Swagger documents the `date-time` format
- **Tests:** `FakeTimeProvider` (`Microsoft.Extensions.TimeProvider.Testing`) drives idempotency expiry, key cleanup, listing timestamps and `EffectiveFrom` handling
- **Display (Angular):** browser time zone via `Intl`, fallback `Asia/Kathmandu`; relative times for lists, absolute time with the zone name in tooltips and detail views
- **Operations:** `TZ=UTC` in containers; NTP on hosts; a readiness warning above 5 s of drift between the application and the database; log timestamps in UTC; background jobs run on intervals, never at a wall-clock time
- **What the clock is not used for:** concurrency and ordering use `rowversion`; GUID v7 timestamps are only a locality hint for the index
- **Edge cases covered by tests:** UTC+5:45 (Nepal, `Asia/Kathmandu`) display, a timestamp that crosses midnight between UTC and local time (23:30 UTC shows as the next day in NPT), a leap day, UTC, a daylight-saving zone (`America/New_York`) to prove nothing assumes a fixed offset, and `EffectiveFrom` boundaries

