# House Broker deployment runbook

## Overview

Production runs a single origin gateway with three application services:

- `gateway` (nginx): serves the Angular SPA and routes `/api` and `/sso`
- `api` (.NET Api host): `/api`
- `auth` (OpenIddict SSO): `/sso`
- `api-migrate` and `auth-migrate` one-shot jobs
- SQL Server and Redis

## Prerequisites

- Docker with Compose v2
- SQL Server reachable with `ConnectionStrings:HouseBroker`
- Redis for shared cache/backplane
- Production signing certificate mounted into the SSO service
- SSO data-protection key storage configured
- `Auth:Issuer` set to the public issuer URL, for example
  `https://<host>/sso`

## Configuration

Configuration is provided through environment variables, `appsettings.Production.json`,
or secret files (`*_FILE`). Validate all options on startup with `ValidateOnStart`.

Important settings:
- `PathBase`: `/api` for Api, `/sso` for Auth
- `ConnectionStrings:HouseBroker`
- `Cache:Provider` = `Memory` or `Redis`
- `Auth:Issuer`
- `Kestrel:Limits:MaxRequestBodySize`
- Rate-limit policy sections

## Running migrations

Migrations are run explicitly by the one-shot commands:

```powershell
dotnet HouseBroker.Api.dll migrate
dotnet HouseBroker.Auth.dll migrate
```

Production orchestrates these as `service_completed_successfully` dependencies
before starting API/Auth instances.

## Health and readiness

- `/health/live`: process alive
- `/health/ready`: SQL Server (and Redis when enabled) reachable
- Health output is status-only outside Development.

## Time and globalization

- Container timezone must be `TZ=UTC`.
- ICU globalization data for `ne-NP` must be present in the image.
- Verify Nepali date/number formatting during container smoke testing.

## Rollout

1. Build images.
2. Start Redis and SQL Server.
3. Run migration jobs.
4. Start `api` (scaled) and `auth`.
5. Start gateway.
6. Smoke test `/`, `/api/health/ready`, `/sso/.well-known/openid-configuration`,
   search, sign-in, and a broker create flow.

## Rollback

- Keep the previous image tags.
- Stop at the gateway first, then applications.
- To roll back schema, deploy a previous image and run the matching migration
  rollback only if the release notes say it is supported.
- Do not auto-run migrations during service startup outside Development.

## NTP

- Keep all hosts synchronized with NTP.
- Clock drift beyond the configured token skew tolerance breaks token
  validation and idempotency expiry.
