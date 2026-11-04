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
- A built SPA in `ui/dist` (mounted by the gateway as `/usr/share/nginx/html`)

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
## Certificate rotation

The SSO host loads one signing certificate from `OpenIddict:SigningCertificate:Path` (and `:Password`). Today a rotation therefore means a restart with the new file, and tokens signed with the old key stop validating once the API refreshes its JWKS (the API caches it).

The plan (4.9) requires an overlap so that tokens stay valid during rotation: publish the new key beside the old one, wait for the API JWKS cache to refresh, then retire the old key. The current configuration does not support two signing keys at once, so that overlap is not yet available. Until it is implemented, schedule a rotation for a low-traffic window and expect partners and users to re-authenticate. Do not describe a rotation as zero-downtime until the overlap is implemented.

## Partner onboarding

Partners get read-only clients (`listings.read`, `contacts.read`) registered on the SSO host:

```powershell
dotnet HouseBroker.Auth.dll register-client
```

Set `Client:Id`, `Client:Name`, `Client:RedirectUris` (empty for `client_credentials`) and `Client:Scopes` (read scopes only) first, from the environment or the secret store. The command prints the client id and secret once. Give them to the partner over a secure channel and never store the printed secret in source control or logs.

## Scaling

- API: `docker compose -f docker-compose.prod.yml up --scale api=3`. Set `Cache:Provider=Redis` when running more than one instance, otherwise cache invalidation does not cross instances.
- SSO: the production compose runs one `auth` instance. Scaling SSO requires the shared Data Protection keys already stored in the database; verify session behavior across instances before scaling it.
- Rate limits are per instance; the gateway enforces the per-IP limit in front of all instances.

## Backup and restore

- The single `HouseBroker` database holds business data, image blobs, identity data and the Data Protection keys. Back it up as a whole with SQL Server full backups (`BACKUP DATABASE HouseBroker TO DISK = ...`).
- Restore to the same release (or a release whose migrations are a superset). Run the `api-migrate` and `auth-migrate` jobs after a restore so the schema and seed match the running images.
- Restoring the Data Protection keys with the database keeps existing SSO sessions valid.

## Troubleshooting by correlation id

- Every response carries `X-Correlation-Id`, and every `ProblemDetails` carries `correlationId`. Ask the caller for that value.
- Logs are compact JSON on stdout in containers. Search them for the id, for example `docker compose logs api auth | grep <correlation-id>`.
- Business and security events for the same request are in the `AuditEvents` table (`CorrelationId` column).
- Partner and user errors show the stable `code` in the response; use it to find the rule that failed (see the error catalog in the partner guide).
