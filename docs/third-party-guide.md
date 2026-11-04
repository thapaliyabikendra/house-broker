# House Broker third-party partner guide

This guide describes how an OAuth partner integrates with the House Broker API.
Use it together with the OpenAPI document at `/api/openapi/v1.json` (or the
generated `docs/swagger.json` snapshot).

## Base URLs

- SPA/gateway: `https://<host>/`
- API: `https://<host>/api/v1`
- SSO: `https://<host>/sso`

All public API URLs are content-negotiated with `Accept-Language: en|ne`.

## Authentication

Partner clients use the OAuth 2.0 `client_credentials` flow against
`/sso/connect/token`.

```text
POST /sso/connect/token
Content-Type: application/x-www-form-urlencoded

grant_type=client_credentials
&client_id=<your-client-id>
&client_secret=<your-client-secret>
&scope=listings.read contacts.read
```

- Tokens are JWTs signed by the SSO service.
- Pass the token as `Authorization: Bearer <access_token>` to `/api/...` only.
- Request only the scopes your client needs; effective access is `permission ∩ scope`.
- Partners are read-only. Write scopes such as `listings.write` are rejected for
  partner clients.

### Available read scopes

| Scope | Purpose |
|------|---------|
| `listings.read` | Read public listing summaries and details. |
| `contacts.read` | Read broker contact fields on listing detail. |

## Common contract

- Correlation id: send or accept `X-Correlation-Id`; the API echoes it on the
  response. Values must match `[A-Za-z0-9_-]{1,64}`.
- Rate limits: anonymous search uses the `search` policy; read calls still
  participate in global limits. A `429 Too Many Requests` response includes
  `Retry-After`.
- Times are ISO 8601 UTC with `Z` (for example `2026-11-04T05:27:42Z`).
- Text is returned in Unicode NFC.
- Localized responses include `Content-Language` and `Vary: Accept-Language`.

## Endpoints most relevant to partners

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/v1/public/listings` | Search listings with paging/sorting/filters. |
| GET | `/api/v1/public/listings/{id}` | Listing detail. Contact is only returned with `contacts.read`. |
| GET | `/api/v1/lookups/property-types` | Property type lookup. |
| GET | `/api/v1/lookups/features` | Feature lookup. |
| GET | `/api/v1/meta` | Supported cultures, currency, limits. |

## Search query parameters

Common filters include `location`, `minPrice`, `maxPrice`, `propertyTypeId`,
`bedrooms`, `featureIds`, `sort`, `page`, `pageSize`. Search only returns
`Active` listings. `pageSize` is capped server-side.

## Concurrency and caching

- Detail responses include an `ETag` based on `rowversion`.
- Broker write operations require `If-Match`; partners do not perform writes.
- Cache entries are separated by public/owner and culture to avoid leaking
  owner-only data.

## Error format

Errors use RFC 7807 `ProblemDetails` with additional fields:

```json
{
  "type": "https://housebroker.example/errors/listing-not-found",
  "title": "Listing not found",
  "status": 404,
  "detail": "Listing 11111111-2222-... was not found.",
  "code": "Listing.NotFound",
  "correlationId": "abc123",
  "errors": { "field": ["message"] }
}
```

Common statuses:
- `400` validation error
- `401` missing/invalid token
- `403` insufficient scope or permission
- `404` resource not found
- `429` rate limited (`Retry-After` provided)

## Registering a partner client

Use the SSO `register-client` command (server-side) with exact redirect URIs and
scopes from configuration:

```powershell
dotnet HouseBroker.Auth.dll register-client
```

Configuration keys:
- `Client:Id`
- `Client:Secret` (optional for public clients)
- `Client:Name`
- `Client:RedirectUris`
- `Client:Scopes`

## Cultures, time, and currency

- `Accept-Language: en|ne` selects the error-message culture (English fallback).
- Localized responses include `Content-Language` and `Vary: Accept-Language`.
- Every instant is serialized as ISO 8601 UTC with `Z` (for example
  `2026-11-04T05:27:42Z`); offset-less input is rejected.
- Embedded list values use Unicode NFC.
- Currency is deployment-global: `meta.currencyCode` (default `NPR`) and matching
  `Pricing:Culture` formatting. Amounts are never converted server-side.

## CORS and pagination

- CORS is driven by `Cors:AllowedOrigins`; with no configured origins the
  service falls back to permissive development behavior, so production must set
  the allowlist explicitly.
- Paginated read responses use `{ items, total, page, pageSize }`. `page` is
  1-based and `pageSize` is capped server-side. Sort defaults to `CreatedAt` desc
  with `Id` as tie-breaker (machine ordering; human display may differ).

## Error-code catalog

All error responses use the same RFC 7807 `ProblemDetails` shape
(`code`, `title`, `status`, `detail`, `correlationId`, and, when applicable,
`field`/`errors`). `ProblemDetails.code` contains the stable code below; the
human-readable `detail` is localized via `Accept-Language` (English fallback).
`ErrorKind` always maps to the HTTP status through `ResultExtensions`. Codes
that originate outside `Result<T>` (idempotency, feature flags, CORS/policy)
still use the same ProblemDetails envelope.

| Code (`ErrorCodes`) | Kind (`ErrorKind`) | HTTP status | Meaning |
|--------------------|--------------------|-------------|---------|
| `validation.failed` | Validation | 400 | Generic validation failure. |
| `validation.required` | Validation | 400 | A required field is missing. |
| `validation.out_of_range` | Validation | 400 | A value is outside the allowed range (including price ranges and page bounds). |
| `validation.offset_required` | Validation | 400 | An instant was supplied without an offset (`Z`/offset is required). |
| `validation.unsupported_culture` | Validation | 400 | The requested culture is not supported. |
| `not_found` | NotFound | 404 | A lookup value does not exist. |
| `resource.not_found` | NotFound | 404 | The requested resource does not exist (or is not visible to the caller). |
| `forbidden` | Forbidden | 403 | The caller lacks permission, or a partner/user token lacks the required scope (`insufficient_scope`). |
| `not_owner` | Forbidden | 403 | The caller is not the listing owner. |
| `conflict` | Conflict | 409 | The operation conflicts with the current resource state. |
| `precondition.required` | PreconditionRequired | 428 | A required `If-Match` header is missing on a mutating PUT. |
| `precondition.failed` | PreconditionFailed | 412 | The `If-Match` ETag/row version is stale. |
| `rate_limited` | RateLimited | 429 | A rate-limit policy was exceeded; see `Retry-After`. |
| `feature.disabled` | (n/a) | 503 | A required feature flag is disabled. |
| `idempotency.conflict` | Conflict | 409 | An idempotency key was reused with a different request. |
| `idempotency.in_progress` | Conflict | 409 | An idempotency key is already being processed. |

Additional endpoint-specific statuses:

- `400` validation error (ProblemDetails with `field`/`errors`)
- `401` missing/invalid token
- `403` insufficient scope or permission
- `404` resource not found
- `422` invalid or too-long `Idempotency-Key` header
- `429` rate limited (`Retry-After` provided)

All codes are returned in `ProblemDetails.code`; localized text depends on
`Accept-Language`.

- Partner access is read-only.
- Rate limits are currently per-instance.
- Broker text and notes are not translated; only API/UI chrome and error codes
  are localized.
- Tokens are stored by the SPA in `sessionStorage`; partner clients should
  follow their own secure token storage guidance.
