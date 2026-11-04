# Test strategy and E2E coverage (deferred)

## Status

The automated end-to-end suite is deferred. Unit and contract suites remain in `api/tests` and `ui`.

## Intended stack when implemented

- Browser automation: Playwright (`@playwright/test`)
- Browser matrix: Chromium default; Firefox/WebKit opt-in
- API requests: Playwright `request` helper
- Target environment: existing `docker-compose.prod.yml`
- Multi-instance checks: on demand / release validation

## Intended coverage

1. Smoke: gateway, health, SPA, meta, search, headers, lookups
2. Authentication: seeker/broker/admin login, logout, refresh, PKCE callback validation
3. Search/detail: filters, pagination, detail, localization, anon commission field protection
4. Broker CRUD/lifecycle: create, publish, update with `If-Match`, price/commission recalculation, sold, archive, delete, ownership (`403`)
5. Commission: boundary values `49,99,999.50`, `50,00,000`, `1,00,00,000`, `1,00,00,000.01`
6. Admin rates: authorization, valid updates, invalid tier sets, non-retroactive rate changes
7. Partner API: `client_credentials`, read scopes, denied writes, no commission fields
8. Contact/concurrency: effective permission ∩ scope, `401`/`403`, `412`/`428`, parallel updates
9. Localization/time: `Accept-Language`, cache culture key, timestamp `Z` serialization
10. Image upload: upload, authorization, invalid payload, feature flag off
11. Feature flags/caching: cache invalidation and bypass behavior
12. Scale-out: multi-instance consistency and Redis cache propagation

## Test data approach

- Use documented demo users and seeded data.
- Create mutated listings with unique names/timestamps.
- Restore captured admin commission tiers after tests.
- Tag scenarios: `@smoke`, `@mutating`, `@admin`, `@scale`.
```

Is this too much? Fine. Then create manual Chrome MCP checklist.