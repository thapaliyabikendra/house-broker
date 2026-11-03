# House Broker Plan — 8. Out of Scope

## 8. Out of Scope (roadmap, documented in README)

- Email confirmation, password reset, MFA, social login; admin UI for OAuth clients (clients are registered by CLI)
- Languages beyond `en` and `ne`, a stored per-user language preference, right-to-left layouts, translation of broker-written content, transliteration-aware search, multi-currency and currency conversion
- Time-zone-aware business features (open-house slots, scheduled publishing)
- Backend-for-frontend cookie pattern for the SPA
- Integration tests (WebApplicationFactory / Testcontainers) and end-to-end browser tests
- Deployment automation (registry push, deploy jobs), Kubernetes/Helm manifests, infrastructure as code, autoscaling rules, multi-region
- SQL Server Full-Text Search or Elasticsearch; object storage (S3 / Azure Blob) for images; SQL Server FILESTREAM/FileTable or Remote Blob Store (images use plain `varbinary(max)`)
- Distributed rate limiting (Redis or API gateway), security alerting, log aggregation (Seq)
- Mutation testing (Stryker.NET) on the commission calculator
- Domain events, specification pattern, CQRS/MediatR, generic repository, abstract factory
