# E2E smoke scripts (Chrome DevTools Protocol)

Dependency-free scripts that drive a local Chrome over the DevTools protocol.
They complement the manual MCP checklist in `tests/plans/01-chrome-mcp-manual-testing.md`.

## Run

1. Start the local stack (see the root README): SQL Server, API `:5049`, Auth `:5222`, UI `:4200`.
2. Start Chrome headless with remote debugging:

   ```powershell
   & "C:\Program Files\Google\Chrome\Application\chrome.exe" --headless=new --remote-debugging-port=9222 --user-data-dir=$env:TEMP\hb-cdp --window-size=1280,900
   ```

3. Run a scenario:

   ```bash
   node tests/e2e/scenarios/spa-login-logout.mjs
   ```

Screenshots are written to `tests/e2e/artifacts/` (git-ignored).
Demo accounts come from the Development seed: `broker1@demo.com` / `Password123!`.
