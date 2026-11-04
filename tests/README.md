# Tests

This directory contains test strategy documentation and future end-to-end test assets for House Broker.

## Current status

- Unit/contract tests already live under `api/tests` and `ui`.
- The automated E2E suite is intentionally **deferred**.
- Immediate validation is done through **Chrome DevTools MCP manual smoke testing** against the local stack.
- The detailed E2E reference plan is stored in `tests/plans/`; `docs/` is not used for temporary E2E planning.

## Proposed layout (for later implementation)

```text
tests/
  README.md
  plans/
  e2e/              # Playwright project (deferred)
  scripts/          # stack and reset helpers (deferred)
```

## Manual MCP pass checklist

See:

- `tests/plans/01-chrome-mcp-manual-testing.md`

## Deferred automated suite plan

See:

- `tests/plans/00-test-strategy-and-coverage.md`
```

That path still references current. Fine.