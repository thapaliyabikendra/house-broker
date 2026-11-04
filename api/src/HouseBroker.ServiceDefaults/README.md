# HouseBroker.ServiceDefaults

Shared host defaults used by the API and SSO hosts.

## Functionality

- Serilog configuration and correlation ID middleware.
- Global exception handler and ProblemDetails.
- Security headers, forwarded headers, rate-limit helpers.
- Culture negotiation middleware and UTC JSON converters.
- `TimeProvider` registration and OpenTelemetry hooks.

## Boundaries

- May reference `HouseBroker.Domain.Shared` only.
- No business logic; cross-cutting concerns only.
