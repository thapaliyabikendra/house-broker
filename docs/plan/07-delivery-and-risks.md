# House Broker Plan — 7. Delivery Tiers, Risks and Cut Order

## 7. Delivery Tiers, Risks and Cut Order

### Delivery tiers

| Tier | Phases | Cumulative effort | What you can submit |
|------|--------|-------------------|---------------------|
| 1: API + SSO (**submission scope**) | 0–8, 13, 15 | ≈ 11.7 days | All raw requirements R1–R16 behind OAuth (in-memory cache), plus the foundation items built into those phases: R17, basic R19, API/SSO side of R23–R25, R27. Documented through Swagger (Postman optional) |
| 2: + Angular (optional) | + 10, 11, 12 | ≈ 17.9 days | Adds the web client (R22), the SPA side of R24/R25 and the accessibility target (R26) |
| 3: + hardening (optional) | + 9, 14 | ≈ 20.6 days | Adds idempotency (R18), Redis scale-out (R20), production packaging (R21), gateway routing and the rest of R19 |

Effort check: Tier 1 = 0.5+1.7+1.25+2.2+0.7+0.7+1.55+0.9+0.65+1.0+0.5 = 11.65; Tier 2 adds 1.9+3.3+1.0 = 6.2 (17.85); Tier 3 adds 0.7+2.0 = 2.7 (20.55).

Tier 1 is the assignment submission scope: it satisfies every raw requirement. **Decision (visual showcase):** Tier 1 is submitted with Swagger as the "display" channel. If a visual showcase is wanted, deliver **Tier 2a** first: Phase 10 (1.9) + the Showcase part of Phase 11 (1.1) ≈ 3.0 days, cumulative ≈ 14.7 days (anonymous search and detail only; broker and admin screens and Phase 12 follow in the rest of Tier 2).

**Decision (SSO size):** the separate OpenIddict SSO stays in Tier 1. The raw brief requires an API usable by third parties, and the partner `client_credentials` flow with scopes is what delivers that; a single Identity-plus-JWT host would save about a day but drop it. Tier 1 effort is reduced instead by the in-memory cache, no Quartz and optional Postman. Tiers 2–3 are optional hardening and are delivered only if funded. The cut order below applies within the selected tier.

### Risks and mitigations

| Risk | Mitigation |
|------|-----------|
| Commission leaks to public or other brokers | Separate owner DTO; tests asserting public JSON lacks the field; separate cache keys |
| Boundary ambiguity in tiers | Decided (inclusive middle tier); per-tier `MinInclusive`/`MaxInclusive` flags avoid decimal gaps; explicit boundary and tier-set tests |
| Reviewer expects a visual "display" of listings but Tier 1 has no UI | Swagger UI in Tier 1 (Postman collection optional); Showcase slice of Phase 11 is the first Tier 2 increment |
| OpenIddict or Swashbuckle incompatibility with .NET 10 | Spikes in Phases 1, 3 and 4; fallbacks: built-in OpenAPI, JwtBearer validation |
| Path-base pitfalls (Swagger URLs, redirects, cookie paths, issuer mismatch behind the proxy) | PathBase tested in Phase 1 and again behind the gateway in Phase 14; public issuer configured explicitly |
| Token validation across an internal network and a public issuer | Phase 4 spike; discovery address configurable separately from the issuer |
| SPA token theft (XSS) | Strict CSP, Angular encoding, banned unsafe HTML, dependency audits; BFF pattern on the roadmap |
| Stale cache across instances | Redis provider for multi-instance; Memory provider limited to development or a single instance; cache failures fall through to the DB. Keep the token-in-key invalidation convention. Use explicit `RemoveAsync` or tags only where a two-node contract test verifies them. The Redis cache package is pinned in `api/Directory.Packages.props`. |
| Concurrent migrations at startup | Dedicated `migrate` jobs; services do not migrate in production |
| Lost updates and duplicate creates under retries | `If-Match` + `rowversion`; `Idempotency-Key`; parallel-request checks |
| Rate limits defeated or misapplied behind a proxy | Trusted-proxy `ForwardedHeaders` config; gateway-level limits; limits tunable in configuration |
| Angular scope growth | Fixed page list in Phase 11; no state library, no e2e suite |
| Nepali translation quality and volume | Draft-quality translations flagged for native review; English fallback for any missing key; the language list is configuration-driven |
| Culture data missing in containers (invariant globalization) | Debian-based runtime image with ICU; Phase 14 check that `ne-NP` resources resolve inside the image |
| Accessibility regressions | WCAG 2.1 AA checklist in 4.10; accessibility lint in CI; manual keyboard and screen-reader pass in Phase 12; recheck both languages |
| Time-zone bugs (naive date handling, DST, half-hour and 45-minute offsets) | UTC everywhere, `TimeProvider`, banned APIs, display tests across several zones |
| **Supply-chain/pinning risk** | Minimal dependencies; each addition justified by removing a named custom mechanism; CI keeps `NuGet audit` / `npm audit` / lock files; pin exact package versions in `Directory.Packages.props` and `ui/package.json`; audit pipelines run on every PR |

### Cut order if time is short
1. Phase 14 extras: OpenTelemetry, CI workflow, nginx topology details (keep the Dockerfiles and `migrate` commands)
2. Phase 9 (idempotency; Tier 3 only, so already outside Tier 1); the basic `ETag`/`If-Match` handling in Phase 6 stays
3. Feature flags, coverage report, Postman collection
4. Nepali coverage of the admin screens and the consent page (English fallback keeps them working; the localization machinery stays), then the Angular admin page, then Angular image upload polish
5. Never cut: security controls, rate limiting, concurrency basics, SSO, the commission engine and its tests, the UTC and `TimeProvider` conventions, and the error-code contract
