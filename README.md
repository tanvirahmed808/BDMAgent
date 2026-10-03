# BDM Agent Platform — V1

One deployable B2B business-development platform for selling .NET/C#, Angular, PHP, WordPress, API/integration, cloud-modernisation and dedicated-development services.

## Included
- ASP.NET Core 9 + Clean Architecture
- SQL Server + EF Core
- Lead CRM, scoring and qualification
- AI research, technology classification and personalised outreach drafting
- Mandatory human approval before first send
- SMTP sender adapter
- Follow-up queue (follow-ups become approval drafts; no blind auto-spam)
- Reply ingestion + AI classification
- Interested / meeting / do-not-contact pipeline states
- Campaign configuration
- Dashboard + Swagger API
- Docker Compose for app + SQL Server

## Run with Docker
1. Copy `.env.example` to `.env` and set a strong SQL password.
2. Optional AI: set `AI_API_KEY`. Without it, deterministic fallback drafting/research still works.
3. Optional sending: set SMTP values. For Gmail, use an app password or OAuth-capable provider setup; never put your normal password in source code.
4. Run `docker compose up --build`.
5. Open `http://localhost:8080` or `http://localhost:8080/swagger`.

## Workflow
Create/import lead → research → score/qualify → draft outreach → approve → send → schedule follow-up draft → ingest/classify reply → meeting/closing hand-off.

## Important integration note
The Gmail and Google Calendar connections granted inside ChatGPT are available to ChatGPT, not automatically exported as OAuth credentials into this downloaded .NET application. The app therefore uses provider adapters/configuration rather than embedding account credentials. This keeps the package portable and secure.

## Production hardening before scale
Add authenticated users/roles, OAuth provider credentials, source-specific lead connectors, rate limiting, unsubscribe list enforcement, migrations/backups, monitoring, and jurisdiction-specific outreach compliance.
