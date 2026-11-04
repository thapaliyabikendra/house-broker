# HouseBroker.Application.Contracts

Application layer contracts consumed by hosts and controllers.

## Functionality

- DTOs (`ListingSummaryDto`, `OwnerListingDto`, commission DTOs, lookup DTOs).
- Request models and search queries.
- Service interfaces (`IListingService`, `ICommissionRateAdminService`, `ILookupService`, etc.).
- `Result<T>` / `NonGenericResult` and `Error`/`FieldError` types.
- Abstractions for infrastructure concerns (cache, image storage, unit of work, current user, transaction scope).

## Boundaries

- May reference only `HouseBroker.Domain.Shared`.
- No business logic or infrastructure dependencies.
