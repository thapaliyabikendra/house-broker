# House Broker Plan — 6. Testing Strategy Summary

---

## 6. Testing Strategy Summary

| Area | Type | Key cases |
|------|------|-----------|
| Domain (`Listing`) | Unit, test-first | Invariants, status transitions, price change |
| Commission calculator | Unit, test-first | Tiers, boundaries (incl. decimals such as 49,99,999.50), inclusivity flags, tier-set gap/overlap validation, rounding, no matching tier |
| Rate provider / admin | Unit | Cache invalidation, tier-config validation |
| `ListingService` | Unit, test-first (mocked repo, user, commission provider, clock; no cache, which lives in decorators) | Ownership, commission snapshot, validation, not found, `Result` outcomes |
| Validators | Unit, test-first | Each rule, positive and negative |
| Authorization handlers | Unit | Permission ∩ scope, partner-token rules, ownership, `BrokerProfile` sync |
| Controllers | Unit (mocked services) | Status codes, 403/404/412/428 mapping, correct service call |
| Auth service | Unit | Claim destinations, registration role allowlist, `returnUrl`, `register-client` |
| Cache | Unit | Hit/miss, invalidation, flag bypass, provider failure falls through |
| Cache providers | Contract tests | The same suite passes for the Memory and Redis providers; FusionCache contract tests verify `ICacheService` integration, stampede protection, fail-safe, and auto-recovery. Keys include the request culture wherever the value is localized. |
| Idempotency | Unit, test-first | Replay, body mismatch 422, in-flight 409, expiry, per-user scope, failure releases key |
| Concurrency | Unit + scripted checks | `If-Match` parsing, 412/428 mapping; parallel-request scripts against SQL Server |
| Search | Unit / SQLite in-memory | Filter combinations, paging, Active-only |
| Localization | Unit + parity checks | `Accept-Language` negotiation (en, ne, unsupported), localized `ProblemDetails`, lookups in default language, `Vary`/`Content-Language`, `.resx` and JSON key parity, every `ErrorCodes` entry has en and ne messages, `ui_locales` in SSO, Angular pipe tests per locale, `@jsverse/transloco-keys-manager` CI parity check |
| Time | Unit | `FakeTimeProvider` for expiry and `EffectiveFrom`; UTC round-trip through EF; offset-less input rejected; ISO 8601 `Z` output; display in Asia/Kathmandu, UTC and America/New_York including a midnight-crossing case; banned time APIs fail the build |
| Mapping | Unit | Mapperly profiles map entities to DTOs; owner mappings include commission; public/partner mappings exclude every `Commission*` field |
| Seeding | Unit / SQL Server | Running `migrate` twice creates no duplicates; master data (lookups with default-language names, commission tiers) is present; demo data is Development-only; unchanged seeded rows keep their audit timestamps |
| Audit and history | Unit / SQL Server | Audit columns set on create/update/soft delete with the current actor; `ListingStatusHistories` and `ListingPriceHistories` are append-only; `AuditEvents` records commission-rate changes and redacts secrets/PII; no generic logs table |
| Architecture and security | Architecture + reflection tests | Dependency table; explicit auth metadata on every action; no `Commission*` on public DTOs; controllers reference only Contracts; Mapperly profiles live in Application |
| Angular | Unit (TestBed) | Interceptors, guards, config loader, stores, forms, data services, formatting pipes, language negotiation, key components |
| Accessibility | Automated + manual | Accessibility lint clean; keyboard-only path; focus management; labels and errors; contrast; 200% zoom; screen-reader pass in `en` and `ne` |
| Scale-out | Scripted checks | Multi-instance cache, idempotency, SSO sessions, token validation, `/`, `/api` and `/sso` path bases |

