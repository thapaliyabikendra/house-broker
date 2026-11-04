# HouseBroker.Domain.Shared

Shared kernel used by every project in the solution.

## Functionality

- Roles, permissions, scopes and authorization claim names.
- `ListingStatus` and supported cultures.
- Limits and validation constants.
- Stable error codes and `DomainException`.
- Demo-user/seed descriptors shared by Auth and API.

## Boundaries

- Must not reference any other project.
- No framework dependencies.
