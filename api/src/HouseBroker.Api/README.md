# HouseBroker.Api

HTTP host and composition root. Controllers, authorization, OpenAPI, rate limiting, and wiring per layer via `DependencyInjection.cs`. Exposes `/api` PathBase and API versioning. Controllers depend on contract interfaces only.