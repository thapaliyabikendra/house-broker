# House Broker — Diagrams

> Derived from the plan. If an architecture decision changes, update [02-decisions.md](plan/02-decisions.md) or [03-architecture.md](plan/03-architecture.md) first, then this page. Rendering requires Mermaid support (GitHub renders it natively).

## 1. System context and containers

```mermaid
flowchart TB
    subgraph Users
        seeker([Seeker])
        broker([Broker])
        admin([Admin])
        partner([Partner system])
    end

    subgraph Edge["Single origin (TLS)"]
        gw["gateway (nginx)<br/>/ · /api · /sso"]
    end

    subgraph App["Application services"]
        spa["Angular SPA<br/>authorization code + PKCE"]
        api["HouseBroker.Api ×N<br/>listings · search · commission · admin"]
        auth["HouseBroker.Auth ×N<br/>Identity + OpenIddict SSO"]
    end

    subgraph Data["Data and infrastructure"]
        sql[("SQL Server<br/>HouseBroker (business + identity)<br/>listings + image blobs")]
        redis[("Redis<br/>cache (multi-instance)")]
        otlp["OTLP collector<br/>(optional)"]
    end

    seeker --> browser["Browser"]
    broker --> browser
    admin --> browser
    partner -->|"client_credentials"| gw
    browser -->|"HTTPS"| gw
    gw -->|"/"| spa
    gw -->|"/api/"| api
    gw -->|"/sso/"| auth
    spa -->|"bearer token → /api only"| gw
    api --> sql
    auth --> sql
    api --> redis
    api -.->|"discovery + JWKS (cached)"| auth
    api -.->|"traces/metrics"| otlp
    auth -.->|"traces/metrics"| otlp
```

## 2. Clean Architecture dependency rules

Solid arrows are project references; dotted arrows are host/composition-root use only.

```mermaid
flowchart TD
    DS["Domain.Shared<br/>constants, roles, scopes, error codes"]
    D["Domain<br/>entities · invariants · CommissionCalculator"]
    C["Application.Contracts<br/>DTOs · service interfaces · Result&lt;T&gt;"]
    A["Application<br/>use cases · validators · Mapperly profiles"]
    I["Infrastructure<br/>EF Core · repositories · cache · storage · seeder"]
    SD["ServiceDefaults<br/>Serilog · middleware · PathBase · health · OTEL"]
    API["HouseBroker.Api (host)<br/>controllers + composition root"]
    AUTH["HouseBroker.Auth (host)<br/>Identity + OpenIddict + Razor Pages"]

    D --> DS
    C --> DS
    A --> D
    A --> C
    I --> A
    SD --> DS

    API -.->|"controllers"| C
    API -.-> SD
    API -.->|"composition root"| A
    API -.->|"composition root"| I
    AUTH -.-> DS
    AUTH -.-> SD
```

Enforced by `HouseBroker.Architecture.Tests`. Domain, Contracts and Application never reference EF Core, ASP.NET or Serilog.

## 3. Data model (`HouseBroker` database)

```mermaid
erDiagram
    Listings ||--o{ ListingImages : "has"
    ListingImages ||--|| ListingImageData : "bytes in"
    Listings ||--o{ ListingFeatures : "has"
    Features ||--o{ ListingFeatures : "tagged in"
    PropertyTypes ||--o{ Listings : "classifies"

    CommissionRates ||--o{ Listings : "snapshot source"
    Listings ||--o{ ListingStatusHistories : "status timeline"
    Listings ||--o{ ListingPriceHistories : "price timeline"

    Listings {
        guid Id PK "GUID v7"
        guid BrokerId "token sub; no FK, read model"
        string Title
        string Description
        guid PropertyTypeId FK
        decimal Price
        string Province
        string City
        string Locality
        string PostalCode
        int Bedrooms
        int Bathrooms
        decimal AreaSqFt
        int Status "ListingStatus enum (Draft, Active, Sold, Archived)"
        decimal CommissionAmount
        decimal CommissionRatePercent
        guid CommissionRateId FK
        datetime CreatedAt "UTC"
        guid CreatedBy "null = system"
        datetime UpdatedAt "UTC"
        guid UpdatedBy "null = system"
        bool IsDeleted
        datetime DeletedAt "UTC, nullable"
        guid DeletedBy "nullable"
        bytes RowVersion
    }
    ListingImages {
        guid Id PK "GUID v7"
        guid ListingId FK
        string FileName
        string ContentType
        int ContentLength
        bytes ContentHash
        int SortOrder
        bool IsPrimary
        datetime CreatedAt "UTC"
        guid CreatedBy "null = system"
        datetime UpdatedAt "UTC"
        guid UpdatedBy "null = system"
    }
    ListingImageData {
        guid ListingImageId PK "FK to ListingImages"
        bytes Content "varbinary(max)"
    }
    ListingFeatures {
        guid Id PK "GUID v7"
        guid ListingId FK
        guid FeatureId FK
        datetime CreatedAt "UTC"
        guid CreatedBy "null = system"
    }
    PropertyTypes {
        guid Id PK "GUID v7"
        string Code "unique"
        string Name
        int SortOrder
        datetime CreatedAt "UTC"
        guid CreatedBy "null = system"
        datetime UpdatedAt "UTC"
        guid UpdatedBy "null = system"
    }
    Features {
        guid Id PK "GUID v7"
        string Code "unique"
        string Name
        int SortOrder
        datetime CreatedAt "UTC"
        guid CreatedBy "null = system"
        datetime UpdatedAt "UTC"
        guid UpdatedBy "null = system"
    }

    CommissionRates {
        guid Id PK "GUID v7"
        decimal MaxPrice "nullable = open-ended top tier"
        bool MaxInclusive
        decimal RatePercent
        bool IsActive "false = retired"
        datetime EffectiveFrom "UTC"
        datetime CreatedAt "UTC"
        guid CreatedBy "null = system"
        datetime UpdatedAt "UTC"
        guid UpdatedBy "null = system"
        bytes RowVersion
    }
    BrokerProfiles {
        guid BrokerId PK "token sub"
        string FullName
        string Email
        string PhoneNumber
        datetime CreatedAt "UTC"
        guid CreatedBy "null = system"
        datetime UpdatedAt "UTC"
        guid UpdatedBy "null = system"
    }
    IdempotencyKeys {
        guid Id PK "GUID v7"
        guid UserId "Auth sub; unique with Key"
        string Key "unique with UserId"
        string Method
        string Route
        string RequestHash
        string State
        int StatusCode
        string ResponseBody
        guid ResourceId
        datetime CreatedAt "UTC"
        datetime ExpiresAt "UTC"
    }
    ListingStatusHistories {
        guid Id PK "GUID v7"
        guid ListingId FK
        string FromStatus "nullable"
        string ToStatus
        guid ChangedBy "null = system"
        datetime ChangedAt "UTC"
        string Reason "nullable"
    }
    ListingPriceHistories {
        guid Id PK "GUID v7"
        guid ListingId FK
        decimal OldPrice
        decimal NewPrice
        decimal OldCommissionAmount
        decimal NewCommissionAmount
        decimal OldCommissionRatePercent
        decimal NewCommissionRatePercent
        guid OldCommissionRateId "nullable"
        guid NewCommissionRateId
        guid ChangedBy "null = system"
        datetime ChangedAt "UTC"
    }
    AuditEvents {
        guid Id PK "GUID v7"
        datetime OccurredAt "UTC"
        guid ActorId "nullable"
        string ActorType "User, Partner, System"
        string ActorClientId "nullable"
        string Action
        string EntityType
        guid EntityId "nullable"
        string CorrelationId "nullable"
        string OldValues "redacted JSON"
        string NewValues "redacted JSON"
    }
```

Table names are plural PascalCase; entity classes are singular PascalCase. Every primary key and foreign key is `uniqueidentifier` (GUID v7, generated in the application). Mutable entities carry `CreatedAt`/`CreatedBy`/`UpdatedAt`/`UpdatedBy`; soft-deleted entities also carry `DeletedAt`/`DeletedBy`. `ListingStatusHistories`, `ListingPriceHistories` and `AuditEvents` are append-only, and there is no generic application logs table.

All tables live in the single `HouseBroker` database; business tables use the `dbo` schema and Identity/OpenIddict tables use the `identity` schema.

## 4. Authentication: authorization code + PKCE

```mermaid
sequenceDiagram
    autonumber
    actor U as User
    participant SPA as Angular SPA
    participant GW as nginx gateway
    participant AUTH as HouseBroker.Auth
    participant API as HouseBroker.Api
    participant DB as SQL Server

    U->>SPA: Open the app
    SPA->>GW: GET /sso/connect/authorize?code_challenge=...
    GW->>AUTH: forward /sso/...
    AUTH-->>U: Login / consent pages (Razor, localized)
    U->>AUTH: Credentials
    AUTH->>AUTH: Validate, create authorization code
    AUTH-->>SPA: Redirect with authorization code
    SPA->>GW: POST /sso/connect/token (code + code_verifier)
    GW->>AUTH: forward /sso/...
    AUTH-->>SPA: Access token (15 min) + refresh (7 d rolling) + id token
    SPA->>GW: GET /api/v1/listings/mine (Bearer)
    GW->>API: forward /api/...
    API->>AUTH: Discovery document + JWKS (cached)
    API->>API: Validate issuer, audience, lifetime, signature
    API->>DB: Query listings by BrokerId
    DB-->>API: Rows
    API-->>SPA: OwnerListingDto (includes commission)
    Note over SPA,AUTH: Refresh-token rotation applies before access expiry
```

Partners use `client_credentials` and receive read-only scopes:

```mermaid
sequenceDiagram
    autonumber
    participant P as Partner system
    participant GW as nginx gateway
    participant AUTH as HouseBroker.Auth
    participant API as HouseBroker.Api

    P->>GW: POST /sso/connect/token (client_credentials)
    GW->>AUTH: forward /sso/...
    AUTH-->>P: Read-scoped access token (listings.read, contacts.read)
    P->>GW: GET /api/v1/listings (Bearer)
    GW->>API: forward /api/...
    Note over API: No user → permission ∩ scope reduces to read scopes
    API-->>P: Public DTOs (never commission)
```

## 5. Request authorization decision

```mermaid
flowchart TD
    R["Incoming request"] --> A{"Authenticated?"}
    A -->|"no"| N["401 Unauthorized"]
    A -->|"yes"| B{"Action marked anonymous?"}
    B -->|"yes"| OK["Execute action"]
    B -->|"no"| C{"User token?"}
    C -->|"no (partner)"| D{"Read scope grants access?"}
    D -->|"no"| F["403 insufficient_scope"]
    D -->|"yes"| OK2["Read-only action"]
    C -->|"yes"| E{"Permission ∩ client scope?"}
    E -->|"no"| F
    E -->|"yes"| G{"Own-only action?"}
    G -->|"no"| OK3["Execute action"]
    G -->|"yes"| H{"sub == listing.BrokerId?"}
    H -->|"no"| F2["403 forbidden"]
    H -->|"yes"| OK3
```

Ownership is a resource-based check in the service layer; default deny is the fallback policy.

## 6. Commission calculation and snapshot

```mermaid
flowchart TD
    A["Price set or changed"] --> B["CommissionCalculator.Calculate(price, active tiers)"]
    T[("CommissionRate<br/>IsActive && EffectiveFrom ≤ now<br/>(cached)")] --> B
    B --> C{"Price band (flat rate on the whole price)"}
    C -->|"below 50,00,000"| R1["2.00%"]
    C -->|"50,00,000 to 1,00,00,000 (inclusive)"| R2["1.75%"]
    C -->|"above 1,00,00,000"| R3["1.50%"]
    R1 --> S["Round to 2 dp<br/>MidpointRounding.AwayFromZero"]
    R2 --> S
    R3 --> S
    S --> N["Snapshot on Listing<br/>CommissionAmount<br/>CommissionRatePercent<br/>CommissionRateId"]
    N --> O["OwnerListingDto → owning broker"]
    N -.-> X["Never in public / partner DTOs<br/>or admin views"]
    H["Later tier edit"] -.->|"not retroactive"| N
```

Tier configuration is validated as a whole: at least one active band, exactly one open-ended top band, and bounded maxima that are strictly positive and strictly increasing. Lower bounds are derived from the previous band, so gaps and overlaps cannot occur.

## 7. Listing lifecycle

```mermaid
stateDiagram-v2
    [*] --> Draft
    Draft --> Active: Publish
    Draft --> Archived: Archive
    Active --> Sold: MarkSold
    Active --> Archived: Archive
    Active --> Active: ChangePrice → recalculate commission
    Sold --> Archived: Archive
    Archived --> [*]

    note right of Active
        Seekers only ever see Active listings.
        Archive is allowed from any status other than Archived.
    end note
    note left of Draft
        DELETE is a soft delete (IsDeleted), separate from the Archived status.
        A soft-deleted listing is hidden from all queries.
        RowVersion guards concurrent updates.
    end note
```

## 8. Cache read path and invalidation

```mermaid
flowchart TD
    R["Read: search / detail / lookups / commission tiers"] --> D{"Cached decorator<br/>(CachedListingQueries /<br/>CachedCommissionRateProvider)"}
    D -->|"hit"| H["Return cached DTO"]
    D -->|"miss"| S["IListingQueries → SQL Server"]
    S --> P["Store with TTL<br/>(public vs owner vs contact,<br/>per culture)"]
    P --> H
    W["Create / update / delete / rate change"] --> I["Bump namespace token<br/>(read keys embed the token)"]
    I --> D
    E["Cache failure or ListingCache flag off"] --> F["Log Warning<br/>fall through to the database"]
    F --> S
```

`ListingService` contains no cache code; caching is a decorator. A write on one instance invalidates reads on another with the Redis provider.

## 9. Idempotent resource creation

```mermaid
flowchart TD
    P["POST /listings or POST /admin/commission-rates"] --> K{"Idempotency-Key present?"}
    K -->|"no"| V["Process normally (key is optional)"]
    K -->|"yes"| L["Look up (UserId, Key)"]
    L --> N{"Existing record?"}
    N -->|"no"| C["Create record + business change<br/>in one transaction"]
    C --> S["Persist status code + response body"]
    S --> R["201 Created"]
    V --> R
    N -->|"yes, same request hash"| RP["Replay stored response<br/>Idempotent-Replayed: true"]
    N -->|"yes, different request hash"| CF["422 Unprocessable Entity"]
    N -->|"in-flight duplicate"| CI["409 Conflict"]
    X["Keys expire after 24 h"] -.-> L
```

Keys are scoped per user and cleaned up by an idempotent background job.
