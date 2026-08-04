# Phase 3 — Web Push Notifications (FCM)

## Context

`plans/v1/notification-module-architecture.md` lays out a 3-phase roadmap for the Notification module. Phase 1 (in-app + polling) and Phase 2 (Hangfire fan-out + email channel) are already merged. Phase 3 handles the case where the user isn't looking at the app: push notifications that wake the OS/browser even when the tab or app is closed, via **Firebase Cloud Messaging (FCM)**.

Scope is **Web Push only** — this repo has no native mobile codebase (`frontend/` is the only client), so there's no iOS/Android SDK work. On the backend, FCM is the single push provider, integrated via the official `FirebaseAdmin` NuGet package (confirmed via Context7 docs: `FirebaseMessaging.DefaultInstance.SendEachForMulticastAsync(...)`, per-token failure detail via `MessagingErrorCode.Unregistered`) — the SDK handles credential refresh/retries, so there's no hand-rolled OAuth2 HTTP client. On iOS, FCM itself relays through APNs under the hood; that's transparent to this backend since only web push is in scope here.

The `Notification` table stays the single source of truth (Phase 1/2 principle, unchanged) — push is purely a best-effort "hint" channel plugged into the existing `NotificationDispatcher` fan-out loop, exactly like `EmailNotificationChannel` already is. No changes to `NotificationDispatcher` itself are needed; adding `PushNotificationChannel` to DI is enough.

**Scope decisions locked in for this phase** (see architecture doc's own Open Questions §9 — these are explicitly deferred, not overlooked):
- No `NotificationPreference` per-type/per-channel opt-out system — the only on/off signal is "does this account have an active `PushSubscription` row." Toggling on registers a subscription; toggling off deletes it.
- No time-based cleanup job — invalid tokens are deleted **reactively**, inline, right after FCM reports `MessagingErrorCode.Unregistered` for a token. This is materially free (the multicast response already tells you) and keeps this phase from growing a whole new recurring-job surface.
- No per-recipient localization for push content — `NotificationEmailTemplates` already hardcodes Vietnamese with no locale lookup; `NotificationPushTemplates` follows the identical, already-accepted limitation rather than inventing new i18n infra for a background job with no request context.

---

## Backend

### Why `IPushSender` exists

`EmailNotificationChannel` depends on `IEmailSender` (an Application interface) instead of calling MailKit directly — not because there's a second email provider today, but because it's the only way to unit-test the channel without hitting a real SMTP server, and `EmailNotificationChannelTests` (`backend/tests/StarterKit.Infrastructure.Tests/Services/Notifications/`) proves this pattern is exercised. `PushNotificationChannel` must follow the same reasoning: `IPushSender` in Application, implemented by `FirebasePushSender` in Infrastructure (the only class that touches `FirebaseAdmin`/`FirebaseMessaging`). The channel itself must never call `FirebaseMessaging.DefaultInstance` directly.

### New files

**Domain**
- `Entities/PushSubscription.cs` — `BaseEntity<Guid>`, private ctor, `AccountId (Guid)`, `Token (string)`, `Platform (string)`. `static Create(PushSubscriptionParams)` factory (mirrors `Notification.Create`/`RefreshToken` pattern). One mutator: `ReassignTo(Guid accountId)` — used when the same browser re-subscribes under a different logged-in account. No generic `Update`.
- `Entities/PushSubscriptionParams.cs` — `record PushSubscriptionParams(Guid AccountId, string Token, string Platform)`, placed alongside `NotificationParams` relative to `Notification.cs`.

**Application**
- `Common/Interfaces/IPushSender.cs` — `Task<PushSendResult> SendAsync(IReadOnlyList<string> tokens, PushMessage message, CancellationToken ct)`. No `FirebaseAdmin` types leak into the signature.
- `Common/Models/PushMessage.cs` — `record PushMessage(string Title, string Body, IReadOnlyDictionary<string,string>? Data = null)`.
- `Common/Models/PushSendResult.cs` — `record PushSendResult(IReadOnlyList<string> InvalidTokens, int SuccessCount, int FailureCount)` — the SDK→domain translation boundary.
- `Common/Settings/FcmSettings.cs` — `{ ProjectId, ServiceAccountJson } { get; init; }`, plain POCO, mirrors `EmailSettings.cs`.
- `Services/Notifications/IPushSubscriptionService.cs` + `PushSubscriptionService.cs` — `RegisterAsync(string token, string platform, ct)`, `RemoveAsync(string token, ct)`, `HasActiveSubscriptionAsync(ct)` (backs the toggle's initial state). Ownership resolved via `ICurrentUserService.UserId`, same as `NotificationService`. Lookup-by-token uses `IRepository<PushSubscription,Guid>.FirstOrDefaultAsync(s => s.Token == token, ct)` — **confirmed this already exists** on `IRepository<T,TId>` (`backend/src/StarterKit.Domain/Interfaces/IRepository.cs:12`), no interface change needed. `RegisterAsync`: not found → `Create`+`AddAsync`; found, different account → `ReassignTo`; found, same account → no-op.
- `Services/Notifications/NotificationPushTemplates.cs` — static `TryRender(string type, string? dataJson) -> (string Title, string Body)?`, structurally identical to `NotificationEmailTemplates.TryRender`.
- `Services/Notifications/RegisterPushSubscriptionRequest.cs` — `sealed record` with `[Required]` `Token`/`Platform`, following the `RegisterRequest.cs`/`CreateOrganizationRequest.cs` convention (request DTOs live beside their feature's service, not in a shared DTOs folder).
- `Services/Notifications/PushSubscriptionStatusResponse.cs` — `record PushSubscriptionStatusResponse(bool IsActive)`.

**Infrastructure**
- `Services/Notifications/PushNotificationChannel.cs` — `internal sealed class PushNotificationChannel(IPushSender pushSender, IUnitOfWork unitOfWork, ILogger<PushNotificationChannel> logger) : INotificationChannel`. `Name => "Push"`. `SendAsync`: `NotificationPushTemplates.TryRender` (null → skip) → `unitOfWork.Repository<PushSubscription,Guid>().ListAsync(s => s.AccountId == notification.AccountId, ct)` (empty → skip, **same repo-predicate pattern `EmailNotificationChannel` already uses for `Account`**, no need to inject `AppDbContext` directly) → `pushSender.SendAsync(tokens, message, ct)` → delete rows whose `Token` is in `result.InvalidTokens`, `SaveChangesAsync`.
- `Services/Notifications/FirebasePushSender.cs` — `internal sealed class FirebasePushSender(ILogger<FirebasePushSender> logger) : IPushSender`. Calls `FirebaseMessaging.DefaultInstance.SendEachForMulticastAsync(new MulticastMessage { Tokens = tokens, Notification = new Notification { Title, Body }, Data = ... })`; maps each response's `MessagingErrorCode.Unregistered` to `PushSendResult.InvalidTokens`; try/catch around the whole call (total failure → empty result, logged, doesn't throw — matches the dispatcher's per-channel catch-and-continue).
- `Services/Notifications/PushExtensions.cs` — `AddPush(this IServiceCollection, IConfiguration)` mirrors `EmailExtensions.AddEmail`: read `FcmSettings` section, throw `InvalidOperationException` if `ProjectId`/`ServiceAccountJson` missing, `FirebaseApp.Create(new AppOptions { Credential = GoogleCredential.FromJson(settings.ServiceAccountJson) })` once at startup (guard `FirebaseApp.DefaultInstance == null` first), `services.Configure<FcmSettings>(...)`, register `IPushSender → FirebasePushSender` and `INotificationChannel → PushNotificationChannel`.
- `Persistence/Configurations/PushSubscriptionConfiguration.cs` — `IEntityTypeConfiguration<PushSubscription>`, `ToTable("push_subscriptions")`, **unique index on `Token`**, index on `AccountId`, FK cascade delete to `Account`. Auto-discovered, no manual registration.

**API**
- `Controllers/PushSubscriptionsController.cs` — `[ApiController][Authorize][Route("api/push-subscriptions")]`. `POST` (body `RegisterPushSubscriptionRequest`) → 204; `DELETE` (query `token`) → 204; `GET status` → `PushSubscriptionStatusResponse` (drives the toggle's initial state on page load).

### Existing files — small edits
- `Infrastructure/Persistence/AppDbContext.cs` — add `DbSet<PushSubscription> PushSubscriptions`.
- `Infrastructure/DependencyInjection.cs` — add `services.AddPush(configuration);` next to the existing `AddBackgroundJobs`/`AddNotificationChannels` calls (~line 26).
- Wherever `INotificationService → NotificationService` is registered — add `services.AddScoped<IPushSubscriptionService, PushSubscriptionService>();`.
- `Infrastructure/StarterKit.Infrastructure.csproj` — add `<PackageReference Include="FirebaseAdmin" .../>`.
- `API/appsettings.json` (+ `appsettings.Development.json`) — add `"FcmSettings": { "ProjectId": "", "ServiceAccountJson": "" }`; real values go in the gitignored `appsettings.Development.json`/deploy-time secret, same handling as other credentials in this repo (`serena.md` already flags `appsettings*.json` as secret-bearing).
- New EF migration (`dotnet ef migrations add AddPushSubscriptions ...`) — generated, not hand-written.

**Deliberately not touched**: `NotificationDispatcher` (already loops all `INotificationChannel`s), `NotificationQueues`/`HangfireExtensions` (push reuses the existing `notifications` queue/dispatch job, no new queue), no new recurring job registration.

### Backend tests (new files)
- `StarterKit.Infrastructure.Tests/Services/Notifications/PushNotificationChannelTests.cs` (mirrors `EmailNotificationChannelTests`): known-type-with-subscriptions-sends; no-subscriptions-skips-without-calling-sender; unknown-type-skips-without-touching-repo-or-sender; invalid-token-in-response-deletes-subscription (no email analog).
- `StarterKit.Application.Tests/.../NotificationPushTemplatesTests.cs` (mirrors `NotificationEmailTemplatesTests`): known-type-renders; unknown-type-returns-null.
- `StarterKit.Application.Tests/.../PushSubscriptionServiceTests.cs`: register-new-token-creates; register-existing-token-different-account-reassigns; register-existing-token-same-account-is-idempotent; remove-deletes-owned-subscription; remove-not-owned-is-no-op; has-active-subscription true/false.

`FirebasePushSender` itself stays untested against the real SDK, same posture as the MailKit-based email sender — it's a thin adapter, covered indirectly via the channel tests' mocked `IPushSender`.

---

## Frontend

### Config-injection approach for the service worker (one choice, not three)

Firebase's web config (`apiKey`, `authDomain`, `projectId`, `messagingSenderId`, `appId`) is public/non-secret — the same category `VITE_GOOGLE_CLIENT_ID`/`VITE_MICROSOFT_CLIENT_ID` already are in this codebase. The static `firebase-messaging-sw.js` (served untouched from `public/`, no Vite processing) can't read `import.meta.env` at runtime, and this repo has zero PWA build tooling (`vite-plugin-pwa` etc.) to template it. Simplest fit: register the worker with the config as a query string —
`navigator.serviceWorker.register('/firebase-messaging-sw.js?apiKey=...&projectId=...')` — and parse `self.location.search` inside the static SW file. No new build step, stays a plain static asset like everything else in `public/` today.

### New files
- `frontend/public/firebase-messaging-sw.js` — static SW: parses `self.location.search`, `importScripts('firebase-app-compat.js', 'firebase-messaging-compat.js')`, `firebase.initializeApp({...})`, `firebase.messaging().onBackgroundMessage(payload => self.registration.showNotification(...))`.
- `frontend/src/composables/use-push-notifications.ts` — mirrors `use-health-status.ts` (composable-owned, no Pinia store — single consumer, no cross-cutting state). Exposes `isSupported`, `permission`, `isSubscribed` refs; `subscribe()` (`Notification.requestPermission()` → register the SW → `getToken(messaging, { vapidKey, serviceWorkerRegistration })` → POST to `getPushSubscriptions()`'s register call, wrapped via `use-api-action.ts`); `unsubscribe()` (`deleteToken` + DELETE call). Reads `isSubscribed` initial state from the new `GET status` endpoint on mount.

### Edits
- `frontend/src/components/ProfileDialog.vue` — new switch (first `n-switch` usage in this codebase — noted, not a blocker) bound to `isSubscribed`, disabled with a tooltip when `Notification.permission === 'denied'` (per the architecture doc's UX note: never prompt on load, only on explicit opt-in).
- `frontend/src/locales/en.ts` / `vi.ts` — new `pushNotifications` namespace in **both** `LocaleSchema` and the literal object (`title`, `description`, `enable`, `disable`, `permissionDenied`, `notSupported`), lowerCamelCase, matching the existing `notifications` namespace's non-type-key convention.
- `.env.example` / `.env.development` — add `VITE_FIREBASE_API_KEY`, `VITE_FIREBASE_AUTH_DOMAIN`, `VITE_FIREBASE_PROJECT_ID`, `VITE_FIREBASE_MESSAGING_SENDER_ID`, `VITE_FIREBASE_APP_ID`, `VITE_FIREBASE_VAPID_KEY` — build-time, mirrors the `VITE_GOOGLE_CLIENT_ID` precedent (not `config.json`, which exists specifically for values that must change post-build; Firebase config doesn't need that).
- `frontend/src/api/types.ts` — re-export the new generated DTOs (`RegisterPushSubscriptionRequest`, `PushSubscriptionStatusResponse`) under stable names, no hand-written wrapper file.
- `frontend/package.json` — add **`firebase`** (client JS SDK) only. `firebase-admin` is Node-only server-side and does not belong here — the .NET equivalent (`FirebaseAdmin` NuGet) is already covered above.

---

## Sequencing

1. **External prerequisite** (parallel to early backend work, blocks any end-to-end check): create/select a Firebase project → Web app config → generate a VAPID key pair (Cloud Messaging settings) → download a service-account JSON for the Admin SDK.
2. Backend Domain → Application (`IPushSender`, `PushSubscriptionService`, templates, settings) → Infrastructure (EF config + migration, `FirebasePushSender`, `PushNotificationChannel`, `PushExtensions`, DI, `.csproj`, `appsettings.json`) → API controller/DTOs. Tests written alongside, `dotnet test backend/StarterKit.sln --no-restore -m:1` passing.
3. OpenAPI re-export (`dotnet build backend/src/StarterKit.API/StarterKit.API.csproj --no-restore -m:1 -p:OpenApiGenerateDocumentsOnBuild=true`) — needs the controller/DTOs from step 2 to exist and compile.
4. `bun run --cwd frontend codegen` — needs step 3's fresh `shared/openapi/openapi.json`.
5. Frontend: `package.json`, `VITE_FIREBASE_*` (needs real values from step 1), SW file, composable, i18n, `ProfileDialog.vue` toggle — needs step 4's generated types.
6. Manual end-to-end pass against the real Firebase project.

---

## Verification

`dotnet test backend/StarterKit.sln --no-restore -m:1` is a valid gate for the backend **logic**: channel send/skip/invalid-token-delete, template render/skip, subscription register/reassign/remove — all mocked, no real SDK calls.

**What it does not prove, and this should not be reported as passing/working without the manual pass**: that `FirebaseAdmin` actually authenticates and delivers against real FCM (`FirebasePushSender` is an intentionally untested thin adapter, same posture as the MailKit email sender); that `Notification.requestPermission()` → `getToken()` → register flow works in a real browser; that `firebase-messaging-sw.js` registers and handles a background push correctly, including that the query-string config-injection parses as expected; that a push actually arrives on a device. These need a real Firebase project, an HTTPS/localhost secure context, and a real browser — Chrome/Firefox/Edge are fairly uniform, Safari's Web Push has known quirks worth a dedicated manual check. No Playwright test is added for this flow: headless browsers handle the permission prompt inconsistently, and FCM tokens are per-installation, so CI can't meaningfully simulate the grant→token→delivery chain. `bun run --cwd frontend build` (typecheck) and `bun run --cwd frontend test:run` should still pass for the composable/component changes, but that is separate from proving delivery works.
