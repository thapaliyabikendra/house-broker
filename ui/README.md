# House Broker UI

Angular 22 SPA for the House Broker marketplace. The app runs behind the shared nginx origin in production and signs in through the `/sso` host.

## Setup

```bash
npm ci
npm run api:generate
npm start
```

The dev server runs at `http://localhost:4200/` and proxies `/api` and `/sso` to the local services through `proxy.conf.json`.

## Scripts

- `npm run lint` - ESLint with Angular and TypeScript rules
- `npm run format:check` - Prettier formatting check
- `npm run test` - Vitest unit tests
- `npm run build` - Production build
- `npm run api:generate` - Regenerate `src/app/api` from `../docs/swagger.json`
- `npm run api:check` - Verify generated API client freshness
- `npm run i18n:check` - Verify `en`/`ne` translation key parity

## Structure

- `src/app/api` - generated OpenAPI client (do not edit by hand)
- `src/app/core` - auth, HTTP interceptors, config, data, i18n, time
- `src/app/shared` - layout, pipes, and reusable state components
- `src/app/features` - route-level feature pages

## Standards

- Standalone components with `OnPush` and signal-based state
- Typed reactive forms
- Functional HTTP interceptors for auth, correlation id, `Accept-Language`, and errors
- OpenID Connect authorization-code + PKCE against `/sso`
- All formatting uses `hb*` pipes instead of Angular built-in date/currency/number/percent pipes
