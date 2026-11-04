# House Broker deployment runbook

## Overview

Production runs a single origin gateway with three application services:

- `gateway` (nginx, built from `deploy/Dockerfile.gateway`): serves the Angular SPA and routes `/api` and `/sso`
- `api` (.NET Api host): `/api`
- `auth` (OpenIddict SSO): `/sso`
- `api-migrate` and `auth-migrate` one-shot jobs
- SQL Server, Redis

The production topology is defined in `docker-compose.prod.yml`.

## Prerequisites

- Docker with Compose v2
- SQL Server reachable with `ConnectionStrings:HouseBroker` (the compose file provisions it)
- Redis for shared cache/backplane
- Production signing certificate mounted into the SSO service, or a mounted PKCS#12 file referenced by `OpenIddict:SigningCertificate:Path`
- SSO data-protection key storage configured
- `Auth:Issuer` set to the public issuer URL, for example
  `https://<host>/sso`
- A built SPA in `ui/dist` (mounter by the gateway as `/usr/share/nginx/html`)

## Configuration

Configuration is provided through environment variables, `appsettings.Production.json`,
or secret files (`*_FILE`). Validate all options on startup with `ValidateOnStart`.

Important settings:
- `PathBase`: `/api` for Api, `/sso` for Auth
- `ConnectionStrings:HouseBroker`
- `ConnectionStrings:Redis`
- `Cache:Provider` = `Memory` or `Redis`
- `Cache:Redis:Configuration`
- `Auth:Issuer`
- `OpenIddict:SigningCertificate:Path`
- `OpenIddict:SigningCertificate:Password`
- `Kestrel:Limits:MaxRequestBodySize`
- Rate-limit policy sections

## Environment variable examples (Compose)

| Variable | Example |
|---|---|
| `Cache__Provider` | `Redis` |
| `Cache__Redis__Configuration` | `redis:6379` |
| `ConnectionStrings__Redis` | `redis:6379` |
| `Auth__Issuer` | `https://<host>/sso` |
| `OpenIddict__SigningCertificate__Path` | `/secrets/signing.pfx` |

## Running migrations

Migrations are run explicitly by `api-migrate` and `auth-migrate`:

```powershell
dotnet HouseBroker.Api.dll migrate
dotnet HouseBroker.Auth.dll migrate
```

Production orchestrates these as `service_completed_successfully` dependencies
before starting API/Auth instances. A failed migration blocks the services from starting.

## Health and readiness

- `/api/health/live`: process alive
- `/api/health/ready`: SQL Server and Redis (when enabled) reachable
- `/sso/health/live`: process alive
- `/sso/health/ready`: SQL Server reachable
- `http://localhost/` at the gateway: SPA available
- Health output is status-only outside Development.

## Time and globalization

- Container timezone must be `TZ=UTC`.
- ICU globalization data for `ne-NP` must be present in the image.
- Verify Nepali date/number formatting during container smoke testing.
- All hosts must stay synchronized with NTP.

## Rollout

1. Build the SPA (`npm --prefix ui run build`).
2. Build images (`docker compose -f docker-compose.prod.yml build`).
3. Start Redis and SQL Server.
4. Run migration jobs.
5. Start `api` (scaled) and `auth`.
6. Start gateway.
7. Smoke test `/`, `/api/health/ready`, `/sso/health/ready`,
   `/api/v1/meta`, `/sso/.well-known/openid-configuration`,
   search, sign-in, and a broker create flow.

## Rollback

- Keep the previous image tags.
- Stop at the gateway first, then applications.
- To roll back schema, deploy a previous image and run the matching migration
  rollback only if the release notes say it is supported.
- Do not auto-run migrations during service startup outside Development.

## Security notes

- Gateway adds CSP and security headers.
- Gateway applies per-IP rate limits; app rate limits still apply per instance.
- API and auth are not exposed directly in production except through the gateway.