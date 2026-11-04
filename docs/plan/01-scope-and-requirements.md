# House Broker Plan — 1. Scope and Requirements

## 1. Scope and Requirement Traceability

| # | Source | Requirement | Phase | Tier |
|---|--------|-------------|-------|:----:|
| R1 | Raw | User authentication (OAuth 2.0 / OpenID Connect with OpenIddict + ASP.NET Core Identity) | 3, 4 | 1 |
| R2 | Raw | Differentiate house seekers vs brokers | 3, 4 | 1 |
| R3 | Raw | CRUD for listings, MSSQL, data integrity | 2, 6 | 1 |
| R4 | Raw | Listing details: type, location, price, features | 2, 6 | 1 |
| R5 | Raw | API exposed for third parties (OAuth scopes, OpenAPI, versioning) | 1, 3, 4, 6, 7, 13 | 1 |
| R6 | Raw | Proper validation | 6 | 1 |
| R7 | Raw | Caching (Tier 1: in-memory provider; Redis is R20, Tier 3) | 8 | 1 |
| R8 | Raw | Commission engine (tiered %) | 5 | 1 |
| R9 | Raw | Commission stored/displayed, visible only to the owning broker | 5, 6 | 1 |
| R10 | Raw | Commission rates configurable via DB | 2, 5 | 1 |
| R11 | Raw | Search by location, price range, property type | 7 | 1 |
| R12 | Raw | Filters to refine results | 7 | 1 |
| R13 | Raw | Detail view: images, description, broker contact (subject to authorization) | 6, 7 | 1 |
| R14 | Raw | Unit tests: commission, listing service, controllers (mocked) | 5, 6, 13 | 1 |
| R15 | Raw | Clean Architecture + layer comments | 0, 1, 15 | 1 |
| R16 | Raw | README with setup and DB instructions | 15 | 1 |
| R17 | Added | Correlation id on every request, response and log line | 1, 13 | 1 |
| R18 | Added | Idempotent resource creation | 9 | 3 |
| R19 | Added | Concurrency control. Basic `rowversion` + `ETag`/`If-Match` on listings (Phases 2, 6) is Tier 1; serialized tier-set edits (Phase 5) are Tier 1; `If-Match` on tier edits and the parallel-request checks (Phase 9) are Tier 3 | 2, 6, 9 | 1 / 3 |
| R20 | Added | Horizontal scalability (multiple API and SSO instances; Redis cache + backplane) | 8 (hooks), 14 | 3 |
| R21 | Added | Production deployment readiness | 14, 15 | 3 |
| R22 | Added | Angular web client (search, detail, broker area, admin) | 10, 11, 12 | 2 |
| R23 | Added | Path bases: API at `/api`, SSO at `/sso` (Tier 1); web UI at `/` and gateway routing (Tiers 2–3) | 1, 14 | 1 / 3 |
| R24 | Added | Localization: English (default) and Nepali; API messages and SSO pages (Tier 1); Angular UI text (Tier 2); lookups in default language only; locale-aware formats | 1, 2, 3, 6, 7, 10, 11, 12 | 1 / 2 |
| R25 | Added | Clock and time zones: UTC storage, injectable clock (Tier 1); zone-correct display in the SPA (Tier 2) | 1, 2, 10, 12, 14 | 1 / 2 |
| R26 | Added | Accessibility: WCAG 2.1 AA target for the SPA and SSO pages (SSO page basics in Phase 3, Tier 1; full pass in Phase 12, Tier 2) | 3, 10, 11, 12 | 1 / 2 |
| R27 | Added | Master-data seeding: lookups and commission tiers in every environment | 2, 3 | 1 |

`Raw` = traced to [raw-requirements.md](../../raw-requirements.md). `Added` = plan-added quality, operability or scope items that are not in the raw brief. The `Tier` column says which delivery tier builds the item (see [07-delivery-and-risks.md](07-delivery-and-risks.md)).

**MVP priority:** implement all `Raw` requirements (R1–R16) first. Some `Added` items are marked Tier 1 on purpose: R17, R19 (basic), R23 (API/SSO), R24/R25 (API/SSO side) and R27 are cross-cutting foundations that are cheap to build in from the start and expensive to retrofit (error-code contract, UTC and `TimeProvider`, path bases, `rowversion`). They are built inside the Tier 1 phases and must not grow beyond what those phases list. Every other `Added` item (R18, R20–R22, R26 and the SPA side of R24/R25) waits until the raw scope is complete.

### Interpretation notes for raw requirements

- **R2 (seeker/broker differentiation):** the raw brief allows only two authentication roles. The plan keeps `Broker` and `Seeker`, but adds an `Admin` role plus Admin endpoints so `R10` is operable without direct database edits; this is an intentional extension on top of raw R2/R10, not a replacement for it.
- **R3 (CRUD):** deleting a listing is a soft delete (`IsDeleted`, `DeletedAt`/`DeletedBy`), which hides it from all queries; it is separate from the `Archived` status set by `POST /listings/{id}/archive`. There is no physical hard delete in the MVP. The lifecycle rules are in [AGENTS.md](../../AGENTS.md) and the decisions in [02-decisions.md](02-decisions.md).
- **R8–R10 ("estimated" commission, configurable via DB):** the commission is stored as a snapshot on the listing when the price is set or changed. Editing a tier in the DB changes calculations for new and re-priced listings only; existing snapshots are not recalculated (non-retroactive). This is the plan's reading of "estimated"; the README states it as an interpretation.
- **R10 (configurable commission rates):** tiers live in the `CommissionRates` table and are seeded plus administered through the API; edits are non-retroactive to existing listing snapshots.
- **R11/R13 (location):** location is structured as Province, City, Locality and PostalCode (5-digit Nepal postal code), matching the NPR / Nepali default. The deployment country is Nepal (confirmed); a different country would need only validator and label changes.
- **R13 (showcase detail):** the listing detail returns broker name, email and phone to any caller, anonymous included, because the raw brief asks the showcase detail to display broker contact details. The earlier plan gated contact behind `contacts.read` and a `listings:contact:view` permission; that gate was removed to meet the raw intent. The `contacts.read` scope remains for compatibility with partner clients but no longer gates any endpoint, and no permission is required. Contact PII is therefore public by design. Mitigations: the `BrokerProfiles` record is refreshed from the token claims on every broker read (`GetMineAsync`, `GetOwnerDetailAsync`) and on every write; the cached profile lives at most 5 minutes; and the public detail sits under the `search` rate-limit policy to slow bulk harvesting.
- **R13 ("display"):** Tier 1 has no web UI, so "retrieve and display" is delivered through the documented API (Swagger UI is the required channel; the Postman collection is a convenience and may be cut). If the reviewer expects a visual showcase, deliver Tier 2 (Phases 10–11, Showcase part first); this is called out as a delivery risk in [07-delivery-and-risks.md](07-delivery-and-risks.md).

**Deliberate deviation:** the raw brief asks for Clean Architecture. The business API follows it strictly; `HouseBroker.Auth` is intentionally a single infrastructure-centric SSO service (see [03-architecture.md](03-architecture.md) 3.1) and this exception is documented in the README.

### Commission rule (reference)

Amounts are in the deployment currency (`Pricing:CurrencyCode`, default NPR); the tier thresholds are plain numbers and are never converted. 50,00,000 = 5,000,000. 1 crore = 10,000,000.

| Price | Rate |
|-------|------|
| < 50,00,000 | 2.00% |
| 50,00,000 to 1,00,00,000 (inclusive) | 1.75% |
| > 1,00,00,000 | 1.50% |

Edge cases the tests must cover: `49,99,999`, `49,99,999.50`, `50,00,000`, `1,00,00,000`, `1,00,00,000.01`, `1,00,00,001`, price ≤ 0 (rejected by validation), rounding (2 decimals, `MidpointRounding.AwayFromZero`), and missing or overlapping tiers in the DB.

Boundary resolution: both inclusive thresholds use the middle tier, so the tier predicates are mutually exclusive and exhaustive: `Price < 5000000`, `Price >= 5000000 AND Price <= 10000000`, `Price > 10000000`.

**Tier bound model.** Because price is `decimal(18,2)`, a tier cannot be described by two plain inclusive bounds (that leaves gaps or overlaps between 49,99,999 and 50,00,000). Each `CommissionRates` row therefore stores `MinPrice`, `MinInclusive`, `MaxPrice?` (null = open-ended top tier) and `MaxInclusive`. A price matches a tier when it is `>=` (or `>` if not `MinInclusive`) `MinPrice` and `<=` (or `<` if not `MaxInclusive`) `MaxPrice`. Seeded tiers:

| Tier | MinPrice | MinInclusive | MaxPrice | MaxInclusive | Rate |
|------|---------:|:------------:|---------:|:------------:|-----:|
| 1 | 0 | yes | 5,000,000 | no | 2.00% |
| 2 | 5,000,000 | yes | 10,000,000 | yes | 1.75% |
| 3 | 10,000,000 | no | null | n/a | 1.50% |

Admin writes re-validate the complete active tier set (the set effective now; a future-dated row is validated together with the set that will be effective at its `EffectiveFrom`): sorted by `MinPrice`, the first tier starts at 0 (inclusive), each tier's `MaxPrice` equals the next tier's `MinPrice` with exactly one of the two sides inclusive (no gap, no overlap), and exactly one tier is open-ended.

Editing a boundary cascades to the adjacent active tier so the set stays contiguous: changing a tier's `MaxPrice`/`MaxInclusive` sets the successor's `MinPrice`/`MinInclusive` to the same boundary (inclusivity flipped), and changing its `MinPrice`/`MinInclusive` sets the predecessor's `MaxPrice`/`MaxInclusive` likewise. Both rows are written in the same `Serializable` transaction, each gets its own `commission.rate.updated` audit event, and the whole set is validated before anything is saved. A cascade that breaks the set (for example a max beyond the successor's max) is rejected as a validation error on the edited tier's field and nothing is persisted. Open-ended edges have no neighbour to cascade to.

---
