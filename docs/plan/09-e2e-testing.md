# House Broker Plan - 9. End-to-End Testing

## Summary

House Broker currently relies on unit and contract tests under `api/tests` and `ui`. End-to-end testing is currently executed manually through Chrome DevTools MCP first. The automated Playwright suite is deferred until the manual pass and its findings are complete.

Detailed scenario and tool workflow details also live in:

- `tests/README.md`
- `tests/plans/00-test-strategy-and-coverage.md`
- `tests/plans/01-chrome-mcp-manual-testing.md`

## Current strategy

1. Start the local stack.
2. Use Chrome DevTools MCP for smoke, auth, search, listings, commission, admin, partner, localization and responsive checks.
3. Record console errors, failed requests, screenshots and missing stable selectors.
4. Convert those findings into a deferred Playwright suite.

## Later automated target

- Playwright project under `tests/e2e`
- Targets the existing production-style stack from `docker-compose.prod.yml`
- Focus areas: smoke, auth, search, broker CRUD, commission boundaries, admin commission rates, partner API, concurrency, localization, images, caching and scale-out.

## Notes

- Production-style stacks require HTTPS for OpenIddict authorization flows.
- Demo users and listings are only seeded in Development.
- Admin commission-rate tests must restore the original tier set.
