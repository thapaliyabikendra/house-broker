# HouseBroker.Infrastructure

Infrastructure adapters implementing application/infrastructure contracts.

## Functionality

- EF Core persistence (DbContext, configurations, migrations).
- Repositories and query implementations (including public/owner projections).
- Cached decorators (`CachedListingQueries`, `CachedCommissionRateProvider`).
- Cache services (memory and Redis).
- Image storage (`SqlImageStorage`).
- Idempotency service and cleanup host.
- Seed data (`DatabaseSeeder`) and commission-rate provider.

## Boundaries

- May reference `HouseBroker.Application`.
- Contains no business rules; only adapters and persistence.
- Cache failures log a warning and fall through to the database.
