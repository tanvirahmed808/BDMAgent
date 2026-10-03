# Implementation plan

Repository inspection: four .NET 9 projects, SQL Server EF Core, six small controllers, static dashboard, SMTP and optional AI, one follow-up worker. No tests, migrations, source discovery, OAuth or Calendar. Existing unsafe status transitions, unbounded reads, HTML injection, missing suppression and credentials in configuration need correction. No AGENTS.md found. Local SQL Server is available; Docker engine is not running.

1. Preserve projects; add normalized identities, suppression, campaign rules, concurrency, audit and integration state.
2. Centralize CRM workflows with approval, send claims, ambiguous-delivery reconciliation, reply suppression and configurable follow-ups.
3. Implement authorized RSS, public procurement API and configurable JSON source adapters; bounded imports, deduplication and autonomous research/drafting.
4. Implement evidence-based structured AI and configured capability descriptions; explicit unavailable AI errors rather than fabricated research.
5. Implement Google OAuth with protected refresh tokens, Gmail threading and inbox sync, Calendar availability and deterministic event booking.
6. Add API authentication, validation, rate limits, resilient HTTP reads, health checks, workers, errors and configuration visibility.
7. Add SQL migrations, opt-in demo seeds, usable dashboard, container deployment and credential documentation.
8. Build and run SQL-backed workflow tests and local HTTP smoke checks. External live sends and Calendar calls require account authorization and must remain unverified without credentials.
