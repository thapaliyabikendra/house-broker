# House Broker concurrency checks

Manual and scripted checks for the parallel-request scenarios covered by
Phases 9 and 14. Run against a real SQL Server instance (for example the
`docker-compose.yml` SQL Server service), not SQLite.

## Setup

```powershell
# Start infrastructure
docker compose up -d

# Apply migrations
dotnet run --project api/src/HouseBroker.Api -- migrate
dotnet run --project api/src/HouseBroker.Auth -- migrate

# Obtain tokens for the demo broker/admin and a fresh listing id before each case.
```

## 1. Stale listing update (`PUT /api/v1/listings/{id}`)

1. Load a listing and capture its `version`/`ETag`.
2. Submit two concurrent `PUT /api/v1/listings/{id}` requests with the same `If-Match` value.
3. Expected: one request returns `200 OK`; the other returns `412 Precondition Failed`.

## 2. Upload limit race (`POST /api/v1/listings/{id}/images`)

1. Create a listing with 9 images.
2. Submit two concurrent image uploads targeting the same listing.
3. Expected: only one upload succeeds; the second is rejected with `409 Conflict` (or `400 BadRequest`) and the final image count is 10 or fewer.

## 3. Concurrent commission-tier edits (`PUT /api/v1/admin/commission-rates/{id}`)

1. Read a tier with its `version`.
2. Submit two concurrent edits with the same `If-Match`.
3. Expected: one succeeds, one returns `412 Precondition Failed`.
4. Run tier-set validation after the test and confirm no gaps or overlaps.

## 4. Duplicate idempotent create (`POST /api/v1/listings`)

1. Generate one `Idempotency-Key`.
2. Submit the same create payload twice, optionally across different API instances.
3. Expected: both requests return the same listing id and the second response carries `Idempotent-Replayed: true`; only one listing is created.

## 5. Cached invalidation across instances

1. Read a listing through API instance A.
2. Update it through API instance B.
3. Re-read through API instance A.
4. Expected: the new representation appears (no stale cache).
