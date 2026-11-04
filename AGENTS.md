# AGENTS.md

Guidance for AI coding agents working in this repository. Read this before changing code or docs.

## Project status

- **Implementation in progress.** The .NET API/SSO services, Angular app, tests, CI and documentation are implemented; the delivery plan remains the source of truth for scope and sequencing. See [README.md](README.md) for current build/test gates.
- **The plan is the source of truth:** [plan.md](plan.md) and [docs/plan/](docs/plan/). Do not invent architecture, APIs or decisions that contradict them.
- If a requirement is missing or ambiguous, update the relevant plan document and call it out; do not silently choose a different design.
- Work phase by phase using [05-phases.md](docs/plan/05-phases.md); each phase has an explicit exit criteria.

## What we are building

A property marketplace MVP: a .NET 10 Clean Architecture API, a separate OpenIddict/Identity SSO service, and an Angular SPA behind one nginx origin. Seekers search listings; brokers manage their own listings and see commission; admins manage commission tiers; OAuth partners get scoped read access.

- Business view: [docs/business-overview.md](docs/business-overview.md)
- Technical view: [docs/technical-overview.md](docs/technical-overview.md)

## Repository map

| Path | Contents |
|------|----------|
| `docs/plan/` | Scope, decisions, architecture, standards, phases, testing, delivery, out-of-scope |
| `docs/business-overview.md`, `docs/technical-overview.md` | High-level summaries |
| `raw-requirements.md` | Original assignment text (do not edit) |
| `api/` | .NET solution: 8 `src` projects, 5 test projects |
| `ui/` | Angular app served at `/` |
| `deploy/` | nginx gateway config |
| `.github/workflows/ci.yml` | .NET + web CI jobs and Docker build |

## Glossary

- **Listing** — the aggregate root for a property. Owned by one broker.
- **Broker** — creates and manages listings; sees commission on their own listings only.
- **Seeker** — browses and searches; sees only `Active` listings.
- **Admin** — manages commission rate tiers only.
- **Partner** — machine client using `client_credentials`, read-only scopes.
- **Owner view** — the broker-only projection that includes commission (`OwnerListingDto`).
- **Commission tier** — a DB-configured price band with a flat rate.
- **Lookup** — an admin-seeded reference table (`PropertyTypes`, `Features`, `CommissionRates`).

## Architecture and dependency rules (non-negotiable)

```
Domain.Shared → Domain → Application.Contracts → Application → Infrastructure
Api, Auth and ServiceDefaults are hosts/support projects.
```

| Project | May reference |
|---------|---------------|
| `Domain.Shared` | nothing |
| `Domain` | `Domain.Shared` |
| `Application.Contracts` | `Domain.Shared` |
| `Application` | `Domain`, `Application.Contracts` |
| `Infrastructure` | `Application` |
| `ServiceDefaults` | `Domain.Shared` |
| `Api` | Contracts/ServiceDefaults in controllers; Application/Infrastructure only in the composition root |
| `Auth` | `Domain.Shared`, `ServiceDefaults` |

- Controllers depend on Contracts interfaces, never on Application/Infrastructure types.
- Domain, Contracts and Application never reference EF Core, ASP.NET or Serilog.
- Auth never references Domain, Application or Infrastructure.
- Architecture tests enforce these rules and must stay green.
- Full detail: [03-architecture.md](docs/plan/03-architecture.md).

## Commands

> These commands match the implemented solution. Confirm local paths in `README.md` and the actual `.sln` before running.

```powershell
# .NET
dotnet restore api/HouseBroker.sln
dotnet build api/HouseBroker.sln --no-restore
dotnet test api/HouseBroker.sln --no-build
dotnet format api/HouseBroker.sln --verify-no-changes
dotnet list api/HouseBroker.sln package --vulnerable --include-transitive
dotnet run --project api/src/HouseBroker.Api
dotnet run --project api/src/HouseBroker.Auth
dotnet HouseBroker.Api.dll migrate
dotnet HouseBroker.Auth.dll migrate

# Local infrastructure
docker compose up -d
docker compose -f docker-compose.prod.yml up --scale api=3

# Angular (once scaffolded)
npm --prefix ui ci
npm --prefix ui run lint
npm --prefix ui run format:check
npm --prefix ui run test
npm --prefix ui run build
npm --prefix ui run api:generate
npm --prefix ui run i18n:check
```

## .NET conventions

- Nullable on; async everywhere with `CancellationToken`; no `.Result`/`.Wait()`.
- Warnings are errors; analyzers (Roslyn, SonarAnalyzer, banned APIs, culture analyzers) fail the build.
- Constructor injection and DI only. No static singletons. `I[Service]` interfaces only at layer boundaries or real test seams.
- Mapperly source-generated mapping; no reflection-based mappers. Explicit owner/public DTO mappings so commission cannot leak.
- FluentValidation in the Application layer.
- Controllers are thin: call a service, map with `ToActionResult`. No business logic.
- Never return entities from controllers; return DTOs.
- Application services return `Result<T>` for expected outcomes (NotFound, Forbidden, Validation, Conflict, PreconditionFailed/Required). Exceptions only for unexpected failures and `DomainException`.
- Application never builds user-facing text: return stable `Error` codes and arguments; localize at the API boundary.
- `[AllowAnonymous]` requires a comment explaining why.
- No raw SQL string concatenation. EF Core parameterized queries only.
- Add a pattern only where it removes duplication or isolates a named seam; prefer composition over inheritance. No MediatR, generic repository, specification pattern or domain events.

## Domain rules

- `Listing` is the aggregate root; behavior methods protect invariants; setters are private; violations raise `DomainException`.
- Lifecycle: `Draft → Active → Sold → Archived`; seekers only see `Active`; soft delete.
- Table names are plural PascalCase; entity classes are singular PascalCase.
- Ids are GUID v7 generated in the application for every entity primary key and foreign key; lookups keep a unique natural `Code`.
- Every mutable entity carries audit columns: `CreatedAt`/`CreatedBy`/`UpdatedAt`/`UpdatedBy` in UTC; soft-deleted entities also carry `DeletedAt`/`DeletedBy`. Actor values are Auth `sub` GUIDs (`null` = system/seed), set by an infrastructure interceptor.
- `ListingStatusHistories` and `ListingPriceHistories` are append-only; commission-rate changes also write an `AuditEvents` row with redacted old/new values.
- Commission is a **flat rate by price tier** applied to the whole price:
  - `< 50,00,000` → 2%
  - `50,00,000 … 1,00,00,000` → 1.75% (both boundaries inclusive)
  - `> 1,00,00,000` → 1.5%
- Tiers store `MinPrice`/`MinInclusive`/`MaxPrice?`/`MaxInclusive`; match with the flags, never with plain inclusive bounds, and re-validate the whole tier set on every admin write (no gaps, no overlaps, one open-ended top tier).
- The snapshot (`CommissionAmount`, `CommissionRatePercent`, `CommissionRateId`) is stored on the listing and recalculated when price changes. Rate changes are **never retroactive**.
- Commission appears only in the owner DTO; public/partner DTOs must never contain `Commission*` properties. Admins do not see per-listing commission.
- Commission tiers live in the database, seeded idempotently, and are editable by Admin only.

## API rules

- Controllers + URL versioning; public URLs are `/api/v1/...` under `UsePathBase("/api")`. Never produce `/api/api/...`.
- Auth: authorization code + PKCE for users, `client_credentials` for partners. Effective access = user permission ∩ client scope; ownership is checked in the service layer.
- Default deny; every action has explicit auth metadata. Partners can only read.
- Search and listing detail are anonymous. Broker contact needs `contacts.read` (and `listings:contact:view` for users).
- `POST` that creates a resource must support `Idempotency-Key`; `PUT` requires `If-Match` and honors `rowversion` (428 missing, 412 stale).
- Errors are RFC 7807 `ProblemDetails` with `code`, `correlationId` and field errors. No stack traces outside Development.
- Document every endpoint with XML comments, `[ProducesResponseType]`, and `ProblemDetails` for error codes; Swagger is the public contract.
- Accept `X-Correlation-Id` only if it matches `[A-Za-z0-9_-]{1,64}`; echo it on responses and include it in logs.
- Rate-limit policies (`search`, `write`, `upload`, `auth`, global) come from configuration; 429 carries `Retry-After`.

## Data, caching and reliability

- One SQL Server database, `HouseBroker`, shared by the API and SSO (business tables in `dbo`, Identity/OpenIddict in `identity`). EF Core code-first migrations.
- In production, migrations run only through the one-shot `migrate` commands, never at service startup. Auto-migrate only in Development.
- Seeding is idempotent (upsert by natural key). Master data everywhere; demo users/listings in Development only.
- No generic application logs table; use Serilog. `AuditEvents` is for business/security audit only and stores redacted old/new JSON, never secrets, tokens or contact PII.
- Caching is implemented as decorators (`CachedListingQueries`, `CachedCommissionRateProvider`); `ListingService` contains no cache code. Cache failures log a warning and fall through to the database.
- Cache keys include culture for localized values and separate public from owner/contact responses.
- Use `rowversion` for concurrency; GUID v7 timestamps are only an index locality hint, never ordering guarantees.

## Time and localization

- `TimeProvider` is the only source of "now". `DateTime.Now/UtcNow/Today`, `DateTimeOffset.Now/UtcNow` and `TimeZoneInfo.Local` are banned and fail the build.
- Store every instant as UTC `datetime2(3)`; serialize ISO 8601 with `Z`; reject offset-less input.
- Sort by `CreatedAt` descending with `Id` as the tie-breaker.
- Supported cultures: `en` (default, `en-IN` formatting) and `ne` (`ne-NP`). All text columns `nvarchar`; normalize input to Unicode NFC and trim at the API boundary.
- All user-facing text comes from `.resx`/translation keys, never hard-coded. Machine formats use the invariant culture; human formats take an explicit culture.
- Angular uses Transloco and `hb*` pipes on `Intl`; the built-in `date`, `currency`, `number` and `percent` pipes are banned in templates.
- Localized responses carry `Content-Language` and `Vary: Accept-Language`.

## Logging and security

- Serilog structured logging with named properties (never string interpolation); UTC timestamps; English-only messages; log an exception once, in middleware.
- Never log passwords, tokens, email, phone or commission amounts above Debug.
- OWASP Top 10:2025 and API Security Top 10:2023 controls are mandatory. See [04-standards.md](docs/plan/04-standards.md) (4.7).
- No secrets in the repo, images, bundle or logs. New NuGet/npm packages are audited.
- Security headers, strict CSP, HTTPS/HSTS, `nosniff`, `DENY` framing, no `Server` header.
- Angular: never use `innerHTML` or `bypassSecurityTrust*`; tokens only in `sessionStorage`; bearer token attached to `/api` only; no tokens in URLs or logs.

## Angular conventions

- Standalone components, `ChangeDetectionStrategy.OnPush`, signals (`signal`, `computed`, `input`, `output`, `model`), `inject()`, native control flow, typed reactive forms.
- Strict TypeScript and strict templates; no `any`; no non-null assertion without a comment.
- Folders: `core/`, `shared/`, `features/<name>/{pages,components,data,models}`, `api/`.
- `ui/src/app/api/` is generated from `docs/swagger.json` with `ng-openapi-gen`; never edit it by hand. Regenerate when the API changes and keep `api:check` green.
- Feature isolation is enforced by ESLint import boundaries.
- Components never inject `HttpClient`; feature data services wrap generated services.
- State is signal-based per feature; no NgRx.
- Follow WCAG 2.1 AA: labels, focus management, keyboard operation, `aria-live`, contrast, logical CSS properties.
- **Transloco packages:** `@jsverse/transloco`, `@jsverse/transloco-locale` for generic date/number/percent/currency pipes, `@jsverse/transloco-keys-manager` for i18n key parity checks in CI.
- Full detail: [04-standards.md](docs/plan/04-standards.md) (4.10–4.12).

## Testing

- **Strict TDD:** commission calculator, domain invariants, validators, `ListingService`, `IdempotencyService`, Auth claim/scope rules, Angular stores/interceptors/pipes.
- **Test after:** controllers, caching, infrastructure wiring, Angular components.
- **Not unit-tested:** EF configuration and migrations (verify against SQL Server).
- Stack: xUnit + Moq + plain assertions; SQLite in-memory only where DB behavior matters; `FakeTimeProvider` for time.
- Required test areas: boundary commission values, ownership, public DTO has no commission, `If-Match` mapping, idempotency replay/conflict/expiry, cache hit/miss/invalidation/fallback, localization negotiation and parity, audit columns and append-only history, architecture dependency rules.
- Coverage is a guide (~80% Domain + Application), not a gate.
- See [06-testing.md](docs/plan/06-testing.md).

## Definition of done

- Zero build warnings; tests green; XML docs present on controllers and DTOs.
- Migration included when the schema changes; seed remains idempotent.
- Audit columns are populated on writes; `ListingStatusHistories`/`ListingPriceHistories` and `AuditEvents` rows are written where the change requires them.
- Swagger/OpenAPI updated; `docs/swagger.json` regenerated and the Angular client refreshed.
- Localization keys added in every supported language; parity checks pass.
- README/docs updated for behavior, configuration or setup changes.
- For web changes: lint, format check, tests, accessibility lint, `api:check` and `i18n:check` pass.
- Security checklist in 4.7 reviewed for new endpoints.

## Never do

- Never commit secrets, connection strings, certificates or `.env` files.
- Never add source code that contradicts the plan; fix the plan first.
- Never expose commission in a public/partner DTO, cache entry or log above Debug.
- Never write secrets, tokens, emails, phone numbers or commission amounts into logs or `AuditEvents` JSON.
- Never bypass ownership checks, default-deny authorization or the permission ∩ scope rule.
- Never use `DateTime.Now/UtcNow`, `TimeZoneInfo.Local`, raw SQL concatenation, reflection-based mapping, `.Result`/`.Wait()`, or hard-coded user-facing text.
- Never edit generated Angular API files, generated migrations by hand, or seeded data outside the seeder.
- Never run migrations automatically outside Development.

## Known documentation inconsistencies (resolved)

1. **Angular Material vs. Material 3:** decisions say Angular Material + CDK. The final compatible package version and whether the M3 theme is enabled are pinned: Angular Material/CDK `^22.2.5`, M3 theme enabled via `mat.define-theme(...)` in `ui/src/styles.scss` (recorded in `ui/package.json` and `ui/README.md`).
