# Chrome DevTools MCP manual testing plan

Use this track before implementing Playwright automation.

## Stack

- Start production-style stack:
  - `docker compose -f docker-compose.prod.yml up -d --build`
- Or run the development stack for OIDC flows over HTTP:
  - API on `http://localhost:5049`
  - Auth on `http://localhost:5222`
  - UI on `http://localhost:4200`

## Tool workflow

1. `list_pages` and `select_page`
2. `navigate_page` to target route
3. `take_snapshot` to inspect UI and collect element `uid`s
4. Use `click`, `fill`, `fill_form`, `press_key`, `type_text`
5. `take_screenshot` for evidence
6. `list_console_messages` and `get_console_message`
7. `list_network_requests` and `get_network_request`
8. `resize_page` for responsive checks

## Smoke checks

- `/` loads and SPA shell renders
- `GET /api/v1/meta` returns currency and cultures
- `GET /api/v1/listings` returns paged data
- `GET /api/v1/lookups/property-types`
- health endpoints: `/api/health/live`, `/api/health/ready`, `/sso/health/ready`
- headers: `Content-Language`, `Vary`
- no duplicate `/api/api` path

## Role checks

- Anonymous: search, filters, listing detail, no commission fields
- Seeker: login, detail, contact visibility
- Broker: create, update, publish, sold, archive, delete, owner commission visibility, image
- Admin: commission tier load/update
- Partner: direct API only, no UI write action

## Manual findings to record

- console errors/warnings
- failed network requests
- missing selectors or stable test IDs
- auth redirect or session issues
- localization or timezone presentation issues
- unexpected data visible in public/partner responses
```

Now user asked "create test plan md files" and we have created. Good. Final response list files.