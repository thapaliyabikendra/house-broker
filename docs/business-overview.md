# House Broker — Business Overview

> High-level view for stakeholders. Requirements and delivery detail live in the [plan](../plan.md); this page does not restate them.

## What we are building

An MVP for a **property marketplace** where house seekers discover listings and brokers manage them. The product is delivered as a public API (usable by third-party partners) plus a first-party web client, with one sign-in experience for people and OAuth credentials for partner systems.

## Who it serves

| Audience | Need |
|----------|------|
| **House seeker** | Browse, search and filter properties; view details and, when signed in, broker contact details |
| **Broker** | Create and maintain listings, upload images, and see the commission earned on their own listings |
| **Admin** | Manage the commission rate tiers that drive broker earnings |
| **Third-party partner** | Integrate read access to listings and contacts using standard OAuth/OIDC credentials |

## Value and business rules

- **Listing lifecycle** — brokers move a property through Draft → Active → Sold → Archived; seekers only ever see Active listings.
- **Commission engine** — each listing carries an estimated broker commission based on a flat rate chosen by price tier (2% below 50 lakh, 1.75% up to 1 crore, 1.5% above). The snapshot is stored with the listing, recalculated when price changes, and never applied retroactively.
- **Confidentiality** — commission is visible only to the broker who owns the listing; it is absent from all public and partner responses.
- **Configurable pricing** — commission tiers are data, not code; admins change them without a release.
- **Open integration** — the API is a first-class product, documented for third parties, with scoped, auditable access.

## Reach and experience

- Two languages at launch (English default, Nepali), with locale-aware dates, prices and number formats; prices default to NPR.
- Responsive, accessible web client (WCAG 2.1 AA target).
- English is the fallback everywhere; broker-written content is shown as written and is not translated.

## Success criteria

1. A seeker can find a property by location, price range and type and view its details end to end.
2. A broker can manage listings and images and sees commission on their own listings only.
3. A partner can integrate using only the published OpenAPI document and integration guide.
4. Commission, ownership and access rules are enforced and covered by automated tests.
5. The system runs as a documented, containerized, multi-instance deployment.

## Boundaries and known limitations

Email confirmation, password reset, MFA, social login, multi-currency, integration/e2e test suites, deployment automation and advanced search backends are deliberately out of the MVP. The full list, with the rationale and roadmap, is in [out-of-scope](plan/08-out-of-scope.md).

## Delivered in tiers

The work is sequenced so value ships early: **Tier 1** API + single sign-on (submit-ready), **Tier 2** adds the Angular web client, **Tier 3** adds idempotency, scale-out and production packaging. Effort and cut order are in [delivery and risks](plan/07-delivery-and-risks.md).

## Where to go next

| Question | Document |
|----------|----------|
| What exactly is required? | [Scope and requirements](plan/01-scope-and-requirements.md) |
| What was decided and why? | [Decisions](plan/02-decisions.md) |
| When does each piece get built? | [Phases](plan/05-phases.md) |
| What is explicitly not included? | [Out of scope](plan/08-out-of-scope.md) |
