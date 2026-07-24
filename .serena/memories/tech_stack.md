# Tech Stack

- .NET 10 / C# (nullable reference types enabled, `ImplicitUsings` enabled)
- ASP.NET Core Web API, Controllers (Newtonsoft.Json for serialization)
- EF Core 10 + Npgsql (PostgreSQL)
- Mapperly (Riok.Mapperly) for entity↔DTO mapping (compile-time source generator, not AutoMapper)
- JWT Bearer auth (`Microsoft.AspNetCore.Authentication.JwtBearer`) + custom API key auth scheme
- BCrypt.Net-Next for password hashing
- ASP.NET Core Data Protection (`Microsoft.AspNetCore.DataProtection.EntityFrameworkCore`) for secret protection
- Serilog (console + file sinks) for logging
- Scalar + Microsoft.OpenApi for API docs (dev-only)
- xUnit + NSubstitute for testing (`FeedbackHub.Domain.Tests`, `FeedbackHub.Application.Tests`)
- Localization: vi (default) / en via `.resx` files + DataAnnotations localization

Solution file: `FEEDBACK-HUB.sln`. 4 src projects + 2 test projects.

## Frontend (`frontend/`)

- Vue 3 (Composition API, `<script setup lang="ts">`) + Vite + TypeScript
- Pinia (setup-store style) for state
- naive-ui component library + Tailwind CSS
- vue-i18n (separate locale catalog from the backend's `.resx` files — see `.claude/rules/localization.md`)
- axios-based API client with 401 refresh-and-retry queuing (`frontend/src/api/client.ts`)
- Vitest + `@vue/test-utils` + msw (unit), Playwright (e2e)
- Package manager: `bun`. No ESLint configured — Prettier only.
- Serena's language server is configured for `csharp` only — it does not cover `frontend/` yet (see `.claude/rules/serena.md`).
