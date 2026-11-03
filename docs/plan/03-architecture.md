# House Broker Plan — 3. Target Architecture

## 3. Target Architecture

### 3.1 Solution layout

```
api/
  HouseBroker.sln
  src/
    HouseBroker.Domain.Shared/        Constants shared everywhere: roles, permissions, scopes, limits, enums,
                                      error codes, supported cultures, default currency. No dependencies. Referenced by every .NET project.
    HouseBroker.Domain/               Entities, aggregates, domain rules. Depends on Domain.Shared.
    HouseBroker.Application.Contracts/ DTOs, request/response models, service interfaces, Result<T> and Error.
                                      Depends on Domain.Shared. Safe to reference from any .NET client.
    HouseBroker.Application/          Use-case implementations, validators, commission calculator, Mapperly mapping
                                      profiles, interfaces for infrastructure (repositories, cache, storage).
                                      Depends on Domain, Contracts.
    HouseBroker.Infrastructure/       EF Core DbContext, migrations, repositories, caching, SQL-backed image storage,
                                      idempotent master-data seeder. Depends on Application.
    HouseBroker.ServiceDefaults/      Host-level cross-cutting code shared by Api and Auth: Serilog setup,
                                      correlation id, ProblemDetails/IExceptionHandler registration,
                                      security headers (`NetEscapades.AspNetCore.SecurityHeaders`), forwarded headers,
FusionCache (`ZiggyCreatures.FusionCache` + `.Serialization.SystemTextJson`; Redis backplane added in Phase 14) DI registration,
                                      PathBase, request localization, UTC JSON settings, health checks, rate-limit
                                      helpers, OpenTelemetry.
                                      Depends on Domain.Shared only.
    HouseBroker.Api/                  Controllers, composition root, Swagger. PathBase /api.
    HouseBroker.Auth/                 SSO: Identity, OpenIddict, Razor Pages (not MVC). PathBase /sso.
                                      Infrastructure-centric, so one project by design. Depends on
                                      Domain.Shared and ServiceDefaults only.
  tests/
    HouseBroker.Application.Tests/    Domain, commission and listing service tests
    HouseBroker.Api.Tests/            Controller and authorization-handler tests with mocks
    HouseBroker.Auth.Tests/           Claim destinations, registration rules, client registration
    HouseBroker.Architecture.Tests/   Dependency rules, auth metadata, DTO leakage
    HouseBroker.TestSupport/          Shared test builders and fakes
  Directory.Build.props, Directory.Packages.props, global.json
ui/
  src/app/{core,shared,features,api}  Angular app; served at /
  angular.json, package.json, proxy.conf.json, README.md
docs/                                 swagger.json, Postman collection, deployment.md, third-party-guide.md,
                                      db-roles.sql, concurrency-checks.md
deploy/                               nginx.conf
.github/workflows/ci.yml
AGENTS.md, README.md, docker-compose.yml, docker-compose.prod.yml, .editorconfig
```

**Dependency rules**

| Project | May reference |
|---------|---------------|
| Domain.Shared | nothing |
| Domain | Domain.Shared |
| Application.Contracts | Domain.Shared |
| Application | Domain, Application.Contracts |
| Infrastructure | Application |
| ServiceDefaults | Domain.Shared |
| Api | Application.Contracts and ServiceDefaults for controllers; Application and Infrastructure only in the composition root (DI extension methods) |
| Auth | Domain.Shared, ServiceDefaults |

- Controllers depend on the interfaces in `Application.Contracts`, never on `Application` or `Infrastructure` types (architecture test)
- Domain, Domain.Shared, Contracts and Application never reference EF Core, ASP.NET or Serilog; they use `ILogger<T>` only
- Auth never references Domain, Application or Infrastructure
- `Result<T>`, `Error` and the service interfaces live in Contracts because they appear in interface signatures; validators and Mapperly mapping profiles live in Application because they are implementation
- Mapperly is a compile-time source generator, so it adds no runtime dependency to Domain or Contracts; generated mapping code never lives in Domain
- The Auth service is an infrastructure-centric identity provider (Identity, OpenIddict, Razor Pages), so Clean Architecture layering applies to the business API, and Auth is kept as one small, separate service. This deliberate deviation from the raw Clean Architecture requirement is documented in the README (Phase 15)
- Each project carries a layer-purpose comment header stating its responsibility and allowed dependencies, satisfying the raw requirement for comments explaining each layer

### 3.2 Data model

**`HouseBroker` database (shared by the API and SSO)**
- Business tables live in the `dbo` schema; Identity/OpenIddict/Data Protection tables live in the `identity` schema, so the two contexts share one database without colliding.
- `Listings` (Id [GUID v7], BrokerId [Guid], Title, Description, PropertyTypeId [Guid], Price, Province, City, Locality, PostalCode, Bedrooms, Bathrooms, AreaSqFt, Status [int, `ListingStatus` enum], CommissionAmount, CommissionRatePercent, CommissionRateId [Guid], CreatedAt, CreatedBy, UpdatedAt, UpdatedBy, IsDeleted, DeletedAt, DeletedBy, RowVersion)
- `PropertyTypes` (Id [GUID v7], Code [unique], Name [nvarchar], SortOrder, CreatedAt, CreatedBy?, UpdatedAt, UpdatedBy?); `Features` (same shape with `Name`); `ListingFeatures` (Id [GUID v7], ListingId, FeatureId, unique on `(ListingId, FeatureId)`, CreatedAt, CreatedBy?). No `PropertyTypeTranslations`/`FeatureTranslations`: lookup names live on the lookup row in the default language (normalization + data integrity; feature set still reference-data rather than appsettings)
- `ListingImages` (Id [GUID v7], ListingId, FileName, ContentType, ContentLength, ContentHash, SortOrder, IsPrimary, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy); `ListingImageData` (ListingImageId PK/FK, Content `varbinary(max)`) keeps LOB bytes in a separate table so listing and search reads never load them; the payload table carries no audit columns
- `CommissionRates` (Id [GUID v7], MinPrice, MinInclusive, MaxPrice?, MaxInclusive, RatePercent, IsActive, EffectiveFrom, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy, RowVersion). Admin creates or edits rates through the API; each change also writes an append-only `AuditEvents` row with the old and new values. The snapshot on `Listings` remains non-retroactive
- `BrokerProfiles` (BrokerId = token `sub` [Guid] PK, FullName, Email, PhoneNumber, CreatedAt, CreatedBy?, UpdatedAt, UpdatedBy?). Read-model of a broker's contact details (synced from token claims); kept separate from Identity even though both live in the same database, so the API never reads the `identity` schema
- `IdempotencyKeys` (Id [GUID v7], UserId [Guid], Key, Method, Route, RequestHash, State, StatusCode, ResponseBody, ResourceId, CreatedAt, ExpiresAt); unique index on `(UserId, Key)`; no `CorrelationId` column (trace via logs/`AuditEvents`); `UserId` is the actor, so no `CreatedBy`/`UpdatedBy`
- `ListingStatusHistories` (Id [GUID v7], ListingId, FromStatus?, ToStatus, ChangedBy? [Guid], ChangedAt, Reason?); append-only; indexed on `(ListingId, ChangedAt)`
- `ListingPriceHistories` (Id [GUID v7], ListingId, OldPrice, NewPrice, OldCommissionAmount, NewCommissionAmount, OldCommissionRatePercent, NewCommissionRatePercent, OldCommissionRateId?, NewCommissionRateId, ChangedBy? [Guid], ChangedAt); append-only; indexed on `(ListingId, ChangedAt)`
- `AuditEvents` (Id [GUID v7], OccurredAt, ActorId? [Guid], ActorType, ActorClientId?, Action, EntityType, EntityId? [Guid], CorrelationId?, OldValues?, NewValues?); append-only. There is no generic application logs table; JSON values are redacted and never contain secrets, tokens or contact PII
- Seed data (idempotent, upsert by natural key): property types, features and the 3 commission tiers in every environment; demo broker profiles and listings in Development only
- Conventions: table names are plural PascalCase and entity classes are singular PascalCase. Every entity primary key is `uniqueidentifier` generated in the application with `Guid.CreateVersion7()`; every FK is `uniqueidentifier`; lookups keep a unique natural `Code`. Every mutable entity carries `CreatedAt`/`CreatedBy`/`UpdatedAt`/`UpdatedBy`; soft-deleted entities also carry `DeletedAt`/`DeletedBy`. Actor columns store the Auth `sub` (`Guid`, null for system/seed) and have no cross-database FK. All text columns `nvarchar`; all instants `datetime2(3)` in UTC (defaults use `SYSUTCDATETIME()` as a safety net); database collation `Latin1_General_100_CI_AI`. Audit columns are populated centrally by an EF `SaveChanges` interceptor using `TimeProvider` and `ICurrentUserService`

**SSO tables (same `HouseBroker` database, `identity` schema)**
- ASP.NET Core Identity tables (Guid keys), including `AspNetRoleClaims` for permissions; `FullName` and `PhoneNumber` on the user
- OpenIddict applications, authorizations, scopes and tokens
- Data Protection keys
- Same conventions: `nvarchar` text, UTC `datetime2(3)` instants; one database for business + identity

### 3.3 Topology

```
Browser ──► gateway (nginx, single origin, TLS termination point)
              ├─ /       → Angular static files
              ├─ /api/   → api  ×N ──► SQL Server (HouseBroker: listings + image blobs), Redis
              └─ /sso/   → auth ×N ──► SQL Server (HouseBroker)

api validates access tokens with the issuer's discovery document and JWKS (cached)
```

### 3.4 API surface (public URLs; controllers use `v1/...` routes under PathBase `/api`)

| Method | Route | Access |
|--------|-------|--------|
| GET | `/api/v1/listings` (search + filters + paging) | Anonymous |
| GET | `/api/v1/listings/{id}` (returns `ETag`) | Anonymous (broker contact: `contacts.read`) |
| POST | `/api/v1/listings` (supports `Idempotency-Key`) | Broker, `listings.write` |
| PUT | `/api/v1/listings/{id}` (requires `If-Match`) | Owning broker, `listings.write` |
| DELETE | `/api/v1/listings/{id}` | Owning broker, `listings.write` |
| GET | `/api/v1/listings/mine` (includes commission) | Broker, `commission.read` |
| GET | `/api/v1/listings/mine/{id}` (owner view with commission, `ETag`) | Owning broker, `commission.read` |
| POST / DELETE | `/api/v1/listings/{id}/images` | Owning broker, `listings.write` (flag: `ImageUploads`) |
| GET | `/api/v1/listings/{id}/images/{imageId}` (file, cacheable) | Anonymous |
| GET | `/api/v1/lookups/property-types`, `/features` (default-language names only) | Anonymous |
| GET | `/api/v1/meta` (currency code, supported cultures, default culture, limits) | Anonymous |
| GET / POST / PUT | `/api/v1/admin/commission-rates` (POST supports `Idempotency-Key`) | Admin, `admin.commission` |
| GET | `/api/health/live`, `/api/health/ready` | Anonymous |

### 3.5 SSO surface (PathBase `/sso`)

| Endpoint | Purpose |
|----------|---------|
| `/sso/.well-known/openid-configuration`, `/sso/.well-known/jwks` | Discovery and signing keys |
| `/sso/connect/authorize`, `/token`, `/userinfo`, `/logout` | OAuth 2.0 / OIDC endpoints |
| `/sso/account/login`, `/register`, `/logout`, `/consent`, `/access-denied` | Razor Pages (anti-forgery, local-only `returnUrl`, framing denied) |
| `/sso/health/live`, `/sso/health/ready` | Probes |
| CLI: `register-client`, `migrate` | `dotnet HouseBroker.Auth.dll <verb>`; `register-client` prints a partner's client id and secret once |

### 3.6 Permission and scope matrix

Effective access requires both the user's permission (for user tokens) and the client's scope.

| Permission claim | Required scope | Seeker | Broker | Admin |
|------------------|----------------|:------:|:------:|:-----:|
| `listings:view` | `listings.read` | ✔ | ✔ | ✔ |
| `listings:contact:view` | `contacts.read` | ✔ | ✔ | ✔ |
| `listings:create` | `listings.write` | | ✔ | |
| `listings:update` (own only) | `listings.write` | | ✔ | |
| `listings:delete` (own only) | `listings.write` | | ✔ | |
| `listings:images:manage` (own only) | `listings.write` | | ✔ | |
| `commission:view` (own listings only) | `commission.read` | | ✔ | |
| `commission:rates:manage` | `admin.commission` | | | ✔ |

"Own only" is enforced by a resource-based ownership check (`sub` equals the listing's `BrokerId`). Partner (`client_credentials`) tokens carry no user, so they can satisfy only `listings.read` and `contacts.read`. `admin.commission` is granted only to the first-party client.

### 3.7 Domain approach (DDD-lite)

- **Aggregate:** `Listing` is the aggregate root, with `ListingImage` and `ListingFeature` inside it. A factory `Listing.Create(...)` and behavior methods (`ChangePrice`, `ApplyCommission`, `Publish`, `Archive`, `MarkSold`, `AddImage`) protect invariants; violations raise `DomainException`. Setters are private.
- **Domain logic:** `CommissionRate.Covers(price)` and `CommissionCalculator.Calculate(price, tiers)`, pure and framework-free.
- **Identity** lives in the Auth service; the domain refers to users only by `BrokerId`.
- **Not used:** domain events, specification pattern, strongly-typed IDs.
- A short ubiquitous-language glossary (Listing, Broker, Seeker, Commission Tier, Owner View) is kept in `AGENTS.md`.

### 3.8 Design principles and patterns

**Rule:** adopt a pattern only where it removes duplication or isolates a seam this plan names. Prefer composition over inheritance; put interfaces only at layer boundaries or where there is a real second implementation or test seam; use DI lifetimes, never static singletons.

**SOLID in this design**

| Principle | Where it shows up |
|-----------|-------------------|
| Single responsibility | Thin controllers; one service per use case; separate validators; pure `CommissionCalculator` |
| Open/closed | Commission tiers are DB data; cache and image storage have swappable providers |
| Liskov substitution | The FusionCache Memory and Redis-backed cache configurations behave identically behind `ICacheService`; one contract-test suite runs against both |
| Interface segregation | Small interfaces: `IListingRepository` (aggregate writes), `IListingQueries` (reads), `ICommissionRateProvider`, `ICacheService`, `IImageStorage`, `ICurrentUserService` |
| Dependency inversion | Application defines the interfaces, Infrastructure implements them; enforced by architecture tests |

**Patterns**

| Pattern | Decision | Where and why |
|---------|----------|---------------|
| Factory method | Use | `Listing.Create(...)`; DI registration picks the cache and storage providers from configuration |
| Builder | Tests only | `ListingBuilder` and request builders in `HouseBroker.TestSupport` |
| Decorator | Use | `CachedListingQueries` wraps `IListingQueries`; `CachedCommissionRateProvider` wraps `ICommissionRateProvider` |
| Repository + unit of work | Use | One repository per aggregate; reads go through `IListingQueries` |
| Adapter | Use | `IImageStorage` and `ICacheService` (FusionCache-backed) implementations |
| Object mapping | Use | Mapperly source-generated profiles in Application for entity ↔ DTO mapping; explicit owner/public mappings protect commission fields |
| Strategy | Seam only | A single `ICommissionCalculator`; a strategy only if a marginal-slab model is ever added |
| Options pattern | Use | Typed configuration with startup validation |
| Abstract factory, generic `IRepository<T>`, specification, MediatR | Not used | Nothing in this plan needs them |

**Result type.** A small hand-written `Result<T>` with an `Error` (stable code, arguments, optional field, kind: NotFound, Forbidden, Validation, Conflict, PreconditionFailed, PreconditionRequired) carries expected outcomes out of application services; it lives in `Application.Contracts`. Application never builds user-facing text: messages are localized at the API boundary (4.11). One `ToActionResult` extension in the Api maps kinds to status codes and localized `ProblemDetails` (Validation gives 400 with field errors, PreconditionFailed gives 412, and so on). Unexpected failures (500) and `DomainException` are handled by the built-in `IExceptionHandler` registered through `AddProblemDetails()`; `DomainException` is converted to a validation error at the application boundary. The unit of work translates `DbUpdateConcurrencyException` into a `PreconditionFailed` error. The Angular client reads the resulting `ProblemDetails` in one HTTP interceptor.

---
