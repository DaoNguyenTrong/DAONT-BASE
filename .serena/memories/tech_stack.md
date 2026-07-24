# Tech Stack

## Backend (`backend/`)
- .NET 10, ASP.NET Core, Clean Architecture (Domain -> Application -> Infrastructure -> API)
- EF Core + PostgreSQL
- Mapperly for entity<->DTO mapping
- JWT bearer auth (access + refresh token), MailKit for SMTP, Google.Apis.Auth for Google login
- Serilog logging
- xUnit + NSubstitute for tests (`backend/tests/StarterKit.Domain.Tests`, `StarterKit.Application.Tests`)

## Frontend (`frontend/`)
- Vue 3 + Vite + TypeScript, Pinia (setup-store style), naive-ui, vue-i18n (vi default, en),
  Tailwind CSS v4
- Package manager: bun
- Vitest + @vue/test-utils + msw for unit tests; Playwright for e2e

## Infra
- Local dev: `docker-compose.yml` at repo root — Postgres + Mailpit (SMTP + web UI at :8025)
- CI: `.github/workflows/release.yml` only (runs on `v*` tag push) — no PR/push CI today
