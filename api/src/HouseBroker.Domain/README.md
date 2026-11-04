# HouseBroker.Domain

Core domain model and invariants for the property marketplace.

## Functionality

- `Listing` aggregate root (lifecycle, invariants, audit fields).
- Lookup entities and history/audit records (`ListingStatusHistories`, `ListingPriceHistories`, `AuditEvents`).
- Commission rate tier shape (`CommissionRate`).
- Domain exceptions (`DomainException` with stable error codes).

## Boundaries

- May reference only `HouseBroker.Domain.Shared`.
- No EF Core, ASP.NET, Serilog or third-party dependencies.
- Mutations happen through behavior methods only; setters are private.
