# HouseBroker.Auth

OpenIddict/Identity SSO host providing authentication and token issuance.

## Functionality

- ASP.NET Core Identity users, roles and claims.
- OpenIddict authorization/token/userinfo/logout endpoints and clients.
- Admin/broker/seeker seeding and client registration service.
- Razor Pages and localized Identity error describer.
- Path base `/sso`.

## Boundaries

- May reference `HouseBroker.Domain.Shared` and `HouseBroker.ServiceDefaults` only.
- No Domain/Application/Infrastructure dependencies.
- Auth-centric, intentionally infrastructure-focused service.
