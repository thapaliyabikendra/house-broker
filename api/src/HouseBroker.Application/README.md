# HouseBroker.Application

Application services implementing the marketplace use cases.

## Functionality

- `ListingService` (CRUD, search, ownership checks, history snapshots).
- Commission calculator and commission-rate admin service.
- Validators (FluentValidation) and listing/commission validators.
- Mapperly mapping profiles with explicit public vs owner mappings.
- Commission tier validation (no gaps/overlaps, open-ended top tier).

## Boundaries

- May reference `HouseBroker.Domain` and `HouseBroker.Application.Contracts`.
- No EF Core, ASP.NET, or Serilog dependencies.
- Returns `Result<T>` with stable error codes; never user-facing text.
