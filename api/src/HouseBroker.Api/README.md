# HouseBroker.Api

HTTP host and composition root for the marketplace API.

## Functionality

- Thin controllers (public, broker, admin) delegating to contract services.
- Authorization (permission + scope + ownership).
- OpenAPI/Swagger, rate limiting, health checks.
- API versioning, localization and ProblemDetails mapping.
- Dependency injection wiring per layer in `DependencyInjection.cs`.

## Boundaries

- Controllers depend on contract interfaces only.
- Application/Infrastructure are referenced only in the composition root.
- Path base `/api`; never produces `/api/api/...`.
