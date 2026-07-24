# Changelog

All notable changes to this project will be documented in this file.

## [Unreleased]

### Added

- Refresh token values are now stored as SHA-256 hashes; plaintext tokens are never persisted.
- Session management API: `GET /api/auth/sessions`, `DELETE /api/auth/sessions/{id}`, `POST /api/auth/sessions/revoke-others` — each refresh token row maps to one session.
- `LoginAt` field on `RefreshToken` tracks the original sign-in time across token rotations, separate from `CreatedAt` (last rotation time).
- Session management UI in `ProfileDialog` (Sessions tab): lists active sessions with parsed device label, "This device" badge, "Remember login" badge, last-active and original sign-in timestamps, per-session and bulk revoke.
- `format-device-info` utility (backed by `ua-parser-js`) parses raw User-Agent strings to friendly labels ("Chrome on Linux", "Safari on iPhone") for the session list.
- Google social login button on `LoginView` and `RegisterView` (`GoogleLoginButton` component, `use-google-auth` composable).
- Profile management consolidated into `ProfileDialog` (sidebar avatar/name click) — `ProfileView` and its route removed.
- `hasPassword` flag in `ProfileDto` — hides the change-password tab for social-login-only accounts.
- `RefreshTokenCleanupService` — background service (runs every 24 h) bulk-deletes expired and revoked refresh tokens older than the configured retention window (default 7 days), keeping the `refresh_tokens` table bounded.
- `RefreshTokenCleanupSettings` — configurable via `appsettings.json` (`IntervalHours`, `RetentionDays`).

- `.claude/rules/frontend-conventions.md` and `.claude/rules/api-contract.md` — agent rules covering the frontend stack and the backend↔frontend API contract, previously undocumented after the monorepo restructure.
- Frontend app (`frontend/`) — Vue 3 + Vite dashboard imported from the standalone `FEEDBACK-HUB-FE` starter template.
- MVP product architecture, backend architecture, and phased implementation plan under `documents/`.
- Decision log entries for Account↔Tenant membership, single Owner, `X-Tenant-Id`, UUID v7, rate limits, and repo scope (API + embed script).
- `POST /api/auth/register` for email/password self-registration.
- `IdGenerator.NewUuidV7()` convention helper for entity primary keys.
- `.claude/workflows/localization-sync-audit.js` — workflow script backing the `localization-sync-audit` skill, diffs vi/en key parity across backend resx/const messages and frontend locale files.
- Mandatory email verification: `POST /api/auth/verify-email` and `POST /api/auth/resend-verification`, backed by `EmailVerificationToken` (SHA-256 hashed) and real SMTP delivery (`MailKit`, `EmailSettings`, fail-fast validated at startup).
- Social login: `POST /api/auth/external/{provider}` via credential-forwarding (frontend obtains an id_token/code from the provider SDK and posts it to the backend for validation), backed by the `IExternalAuthProvider` abstraction and `ExternalLogin` entity; Google shipped first (`GoogleAuthProvider`, `Google.Apis.Auth`), rejects providers that don't confirm `email_verified`. Microsoft/GitHub deferred behind the same abstraction.
- Fixed-window rate limiting (`Microsoft.AspNetCore.RateLimiting`, per-IP) on `login`, `register`, `verify-email`, `resend-verification`, and `external/{provider}`.
- Unified error response shape across the whole API: business exceptions, model-validation failures, and rate-limit rejections all now go through a shared `ApiProblemDetailsFactory` and carry a machine-readable `code` field (the message's resx key) alongside the existing `status`/`title`/`detail`/`errors`, so clients can branch on error identity instead of parsing localized text.
- Frontend `RegisterView` and `VerifyEmailView` — the `/register` page (previously a stub redirect to `/login`) and a new `/verify-email` page that consumes the token from the backend's verification email link, auto-logs in on success, and offers a resend on an invalid/expired token.
- Frontend `ResendVerificationForm` component, reused from `LoginView` (on an `EmailNotConfirmed` login error), `RegisterView`'s post-registration success panel, and `VerifyEmailView`'s error state.
- Frontend `useApiAction` composable (`frontend/src/composables/use-api-action.ts`) — centralizes the try/catch/loading-flag/error-code-dispatch shape every form was reimplementing individually, mirroring the backend's `ApiProblemDetailsFactory` consolidation.
- Frontend deploy-time runtime config: an optional `public/config.json` (see `public/config.example.json`), fetched once at app bootstrap, can override the build-time `VITE_API_BASE_URL` so one build can target different backend environments without a rebuild.
- Frontend `RequiredMark` component — a red asterisk next to every genuinely-required field label (login, register, resend-verification, account create/edit, profile, change-password); optional fields (phone/position/address) are left unmarked.

### Changed

- `POST /api/auth/register` no longer auto-issues JWT/cookies — creates an unconfirmed account, sends a verification email, and returns `202 Accepted`; `POST /api/auth/login` now rejects accounts whose email isn't confirmed (`401 EmailNotConfirmed`).
- Rate-limit (429) and validation (400) error responses are now localized (vi/en) like every other error; previously both were hardcoded English regardless of `Accept-Language`.
- Frontend: removed the leftover `role`/`isAdmin` field and admin-only gating (backend no longer has a global `Account.Role`) — `/accounts` is now open to any authenticated user; removed dead code calling non-existent `bootstrap-admin`/session-management endpoints; removed the non-functional account list sort UI (backend pagination has no sort support).
- `CLAUDE.md`/`AGENTS.md` and `.claude/rules/` (`architecture.md`, `commands.md`, `localization.md`, `serena.md`) updated to reflect the monorepo layout (backend/frontend/shared), and resynced with each other after drifting apart; corrected a stale claim about a CI workflow that doesn't exist.
- Repository restructured into a monorepo: `backend/` (formerly root `src/`/`tests/`/`.sln`), `frontend/`, and `shared/` (`docs/api`, `docs/architecture` merged from root `docs/`+`documents/`, plus `openapi/`).
- README rewritten as a Feedback Hub product overview (replacing chat/RAG-oriented docs).
- README re-audited against actual repo state: split "Đã có" vs "chưa có" by what's actually implemented (invite/transfer ownership are done, embed widget/feedback/dashboard workflow are not), corrected storage (local-disk only, no Redis wired), and added a full backend/frontend/EF quickstart command section.
- `SystemSettingsController`/`SystemSettingsService` rebuilt as a generic key/value get-all/update-section API (previously 100% chat/RAG provider configuration).
- `ApiKeyAuthenticationHandler` and `CurrentUserService` no longer resolve or issue project-scoped claims.
- Auth cookie names de-chat-ified (`chat_bot_access_token` → `access_token`, `chat_bot_refresh_token` → `refresh_token`); OpenAPI description text reworded to generic terms.
- Former Admin-only controllers (`Accounts`, `ApiKeys`, `AuditLogs`, `SystemSettings`) now require any authenticated user (`[Authorize]`); tenant roles come in Phase 1.
- JWT and API-key authentication no longer emit role claims.

### Fixed

- `AppDbContextFactory` (design-time `dotnet ef` factory) now reads `ConnectionStrings:DefaultConnection` from `appsettings.json`/`appsettings.{ASPNETCORE_ENVIRONMENT}.json`, matching the runtime connection string, instead of falling back to a hardcoded connection string pointing at a non-existent database.

### Removed

- Global `Account.Role` / `AccountRole` and EF column `accounts.Role`.
- Bootstrap admin flow (`POST /api/auth/bootstrap-admin`, `AuthSettings.BootstrapAdminSecret`).
- Chat/RAG business logic and all supporting infrastructure: `Project`, `ChatSession`, `ChatMessage`, `AgentToolDefinition`, `QaTestRun`/`QaTestRunItem`, `TokenUsage` entities; Chat, ChatSessions, QaTesting, AgentTools, HydroSync, TokenQuota, and AdminDashboard services; the entire AI (LLM, embedding, rerank, retrieval, vector store) and Hydro external-connector infrastructure.
- Multi-tenant project scoping: `Account.ProjectId`, `X-Project-Id`/`X-Project-Code` headers, `ApiKeyClaims.ProjectId`.
- All 16 prior EF Core migrations, squashed into a single `InitialCreate` baseline.
- Dead `TooManyRequestsException` (never thrown — 429 responses were always written directly by the rate limiter's `OnRejected`, now via the shared factory instead). Frontend's `errors.tokenQuotaExhausted` 429 override, a leftover from the old chat/RAG token-quota feature that discarded the backend's real error detail.

The codebase now serves as a clean Clean-Architecture template retaining only generic infrastructure: accounts, authentication, API keys, file storage, audit logs, and system settings.
