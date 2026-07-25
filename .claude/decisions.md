# Decision Log

Expensive, non-obvious architectural decisions for StarterKit — real trade-offs, external constraints, rejected alternatives, or anything a future session could easily get wrong without the reasoning. Skip routine decisions (standard CRUD, straightforward bug fixes, following an existing pattern) — they add noise without future value.
Newest entry at top — prepend new entries directly below this line, do not append at the bottom.
Write only the **why** — the reasoning and rejected alternative. Never "what was done" or "how" (git log/diff already has that; do not restate file names, method names, or a narrative of the change). Keep each entry under ~80 words (excluding heading).

---

### Brand images converted PNG→WebP, except apple-touch-icon.png and favicon.ico

Converted the wordmark/mark/favicon-16/32/android-chrome files to lossless WebP. Two exceptions,
confirmed with the user rather than converting blindly: `favicon.ico` is a distinct container
format (multi-resolution ICO), not just re-encodable pixel content — must stay `.ico`, no browser
reads a `.webp` through an `x-icon` link. `apple-touch-icon.png` stays PNG — iOS's "Add to Home
Screen" icon has historically had unreliable/undocumented WebP support even where Safari supports
WebP for regular images, and this is the one spot Apple's own guidance still points at PNG
explicitly; not worth the risk of a broken home-screen icon on real iOS devices to save a few KB.

### Favicon/PWA icon set regenerated from the brand mark; caught a stale teal theme-color

Regenerated all 6 files under `public/icons/` (favicon.ico multi-size, 16/32px, apple-touch-icon,
android-chrome 192/512) from `weatherplus-mark.png` via Pillow resize — same filenames/paths kept
so `index.html`/`site.webmanifest` needed no reference changes. While touching these, found
`theme-color` (both `index.html`'s meta tag and the manifest's `theme_color`) still hardcoded to
the old teal (`#0f766e`) from before the purple rebrand — updated both to the new primary
(`#3d3071`), since shipping brand-new icons next to a stale teal browser-chrome tint would have
been an obvious, avoidable inconsistency sitting right next to what was actually asked for.

### Auth pages (login/register/verify-email) also switched to the wordmark

Follow-up to the sidebar-only wordmark decision below — extended the same swap to the auth pages'
icon slot. Distinguished two cases: Login/VerifyEmail showed `t('app.name')` as a heading (pure
brand-name restating what the wordmark image now already says) — dropped that heading entirely.
Register showed `t('auth.registerTitle')` ("Create an account"/"Tạo tài khoản") — a distinct
functional page title, not brand name — kept it below the wordmark. Extracted the light/dark image-
selection logic (previously inlined in AppSidebar.vue) into `useBrandWordmark()` so 4 call sites
don't duplicate the same computed/theme-check.

### Sidebar header shows the wordmark image directly, dropping the separate "App Starter" text

Every existing logo slot (sidebar, login/register/verify) was a small square icon next to
separately-rendered `t('app.name')` text — but 2 of the 3 supplied brand images are wide wordmarks
with the product name baked into the pixels (one for light backgrounds, one pre-built white-on-
violet for dark). Confirmed with the user: only the sidebar header (expanded desktop + mobile
drawer) swaps to the appropriate wordmark image outright, removing the redundant adjacent text —
collapsed sidebar and the auth pages keep the existing icon+separate-heading layout, using the
third (icon-only, circular) brand image in place of the generic placeholder mark. Two wordmark
files are necessary (not one recolorable asset) because the text color is baked into each image,
not CSS-styleable.

### Rejected swapping primary/background roles between the brand's purple and green

User asked to add more green, proposing purple-as-background-only and green-as-primary. Recommended
against it: in the actual logo, purple is the dominant wordmark color and green is a small leaf
accent — inverting that would make backgrounds (necessarily desaturated/low-visibility) carry the
brand's real signature color while a minor logo detail becomes the loudest, most-repeated UI color.
Green-as-primary also collides with the near-universal UI convention of green=success, making the
primary action button read as a "success" state everywhere. Kept purple as primary; added green as
a deliberate secondary accent instead (see below) — confirmed this direction with the user before
implementing.

### Green added as a deliberate accent, not a second primary

Added `--color-leaf-*` (from the logo's leaf glyph, #89b43f) as a separate static palette, used in
exactly two places: a purple→green gradient on the login card's top accent bar (echoing the logo's
own color pairing), and retuning Naive UI's `successColor` to this green instead of its default —
both reinforce brand recognition without touching any interactive/primary surface. Unlike the
purple primary, the dark-mode success steps needed *no* lightness-shift correction (leaf-400/300/200
already lands at ~8-13:1 contrast) — green sits favorably in WCAG's luminance weighting where blue-
violet doesn't, so the same correction wasn't just unnecessary but would have overshot.

### Rebrand: dark-mode primary uses lighter steps (200/100/50) than the old teal did (400/300/200)

Derived the new indigo/violet primary+surface palette by sampling the logo's wordmark color
(#4e3d90) and reusing the *existing* teal ramp's exact lightness steps per swatch (proven,
already contrast-tuned) — only hue/saturation changed. That mostly worked, except dark-mode
primaryColor: copying teal's 400/300/200 steps verbatim gave only ~2.8:1 contrast against the
dark body (should be 4.5+), because WCAG luminance weights green ~10x more than blue, so a
blue-violet hue needs meaningfully more lightness than teal to read equally bright. Shifted the
whole dark-mode trio two steps lighter (200/100/50) to land back around 8-16:1, verified in code
before touching any files, not just by eyeballing hex values. Also retinted the neutral `surface`
scale (low-saturation slice of the same brand hue) instead of leaving it hue-neutral slate, so
borders/backgrounds/text-muted read as one family with the primary color, not a generic gray.

### Broadened MicrosoftAuthProvider's catch to include SecurityTokenArgumentException

Found while investigating a fresh "Không thể đăng nhập" report (turned out unrelated — see below):
a malformed/non-JWT-shaped `credential` throws `SecurityTokenMalformedException`, which derives from
`SecurityTokenArgumentException`/`ArgumentException`, NOT `SecurityTokenException` — it fell through
the existing `catch (SecurityTokenException)` and surfaced as an unhandled 500 instead of a 401.
Verified via reflection that the actual validation-failure exceptions (invalid issuer/audience/
signature/expiry) all still derive from `SecurityTokenException` as expected, so this only affects
garbage/non-JWT input (a misbehaving or malicious client bypassing the frontend) — genuine MSAL-
issued tokens were never affected by this specific gap. Broadened the catch with an `is X or Y`
pattern rather than two catch blocks with identical bodies.

### Microsoft login always failed with 401: `Regex.Escape` doesn't escape the closing `}`

Root cause of every Microsoft sign-in returning `InvalidExternalCredential` (401), surfaced only by
live testing — unit tests all mock `IMicrosoftJwtValidator`, so the real regex-building line was
never exercised. `Regex.Escape("{tenantid}")` escapes the opening brace only (`\{tenantid}`, no
backslash before `}`), so `Regex.Escape(config.Issuer).Replace("\\{tenantid\\}", "[^/]+")` searched
for a substring that never occurred — the placeholder was silently never replaced, leaving the
literal text `{tenantid}` in the final pattern, which then matched no real issuer, ever. Fixed by
splitting the issuer template on the raw `{tenantid}` placeholder *before* escaping each segment,
sidestepping any dependence on `Regex.Escape`'s exact brace-handling. Extracted the regex-building
into `MicrosoftJwtValidator.BuildIssuerPattern` (previously inline, untestable without a network
call) and added direct unit tests for it — a mocked-validator test alone cannot catch a bug that
lives inside the validator itself.

### `ExternalLogin_UnsupportedProvider_ReturnsBadRequest` depended on local appsettings.json state

The API test fixture (`ApiFactoryFixture`) boots the real `Program` and only overrides a few
settings via env var (connection string, rate limits, storage path) — `ExternalAuthSettings` was
never one of them, so the test's "no provider registered" assumption silently depended on the
developer's local `appsettings.json` having blank `Google`/`Microsoft` ClientIds. Filling in real
ClientIds there for manual OAuth testing (this session) broke it — `google` became a registered,
real (network-calling) provider, turning the expected 400 into a 401. Added explicit env var
overrides forcing both ClientIds empty for the test run, matching the existing pattern for the
other settings — test correctness should never depend on what a developer happens to have in their
local, gitignored config.

### Social login buttons: Google converted to a full-width text+logo button, not Microsoft to icon-only

Google's button was icon-only (40x40 circle). Asked to sync the two, and confirmed with the user:
made Google match Microsoft's shape (full-width, logo + visible "Sign in with Google" text, square
corners, 40px height) rather than shrinking Microsoft to icon-only — Microsoft's own guidelines
mandate the logo always pair with visible text (see the guideline-conflict entry above), while
Google's guidelines are flexible enough to allow a text button. Both buttons now share identical
structural CSS (height, padding, gap, font-size/weight, border width, corner radius, hover/pressed/
disabled transitions); only brand-mandated colors, font-family and logo differ between them.

### Microsoft popup login silently hung: hash-mode router *and* msal-browser 5.x's bridge model

Two compounding issues, found across two rounds of live Playwright testing (first the popup
returned `#/code=...` with a spurious `/`; after the first fix it stopped mangling the hash but
never closed the popup — a real end-to-end MS sign-in reproduced with a cached SSO session,
since no test credentials were available to type manually):

1. `createWebHashHistory()` normalizes `location.hash` as a side effect of `createRouter()` at
   `router/index.ts`'s module top level, which runs at *import time* — before any guard in
   `main.ts`'s own body, since ES module imports evaluate before the importing file's body. A
   static `import router from './router'` at the top of `main.ts` rewrote the unrecognized
   `#code=...` into `#/code=...` on every load, including the popup's redirect-back load. Fixed by
   making the router import dynamic (`await import('./router')`) inside `bootstrap()`, gated behind
   `isOAuthPopupRedirect()` (hash contains `code=`/`error=` and doesn't start with `#/` — a real
   route always does).
2. Leaving the hash untouched wasn't sufficient on its own: `@azure/msal-browser` 5.x no longer
   polls `popupWindow.location.href` from the opener (the old mechanism) — the popup page itself
   must call `broadcastResponseToMainFrame()` (from the `@azure/msal-browser/redirect-bridge`
   subpath export) to parse the response and post it to the opener over a `BroadcastChannel`, then
   close itself. Skipping the app mount entirely (as fix #1 did in isolation) meant nothing ever
   called that bridge — the popup sat on the callback URL forever until manually closed. Fixed by
   branching on `isOAuthPopupRedirect()`: call the bridge instead of mounting, rather than doing
   nothing.

Rejected a dedicated static HTML redirect page (Microsoft's own recommended pattern for issue #1
alone) — Vite's dev server routes nested `public/*.html` requests through its SPA-fallback/HTML-
transform pipeline instead of serving them raw, silently returning `index.html` instead (confirmed
via `curl`), so it would have "worked" only in production static hosting and broken in local dev.
Google's login is unaffected by either issue — GIS delivers the credential via an in-page JS
callback, never a URL/hash round-trip.

### Microsoft login button kept text+logo (not icon-only like Google's circular button)

User asked to visually sync the Microsoft button with Google's icon-only circular button, but
Microsoft's own branding guidelines (learn.microsoft.com/entra/identity-platform/howto-add-branding-in-apps)
explicitly state the logo must always appear paired with visible "Sign in with Microsoft"/"Sign in"
text — an icon-only treatment breaks that rule. Kept the rectangular text+logo button (square
corners per MS's redlines, not rounded), matched only height/font/hover-state feel to Google's
button instead of shape, after confirming this trade-off with the user.

### Microsoft external login: multi-tenant issuer validated by regex, no email_verified check

`common` tenant's OIDC discovery document reports `issuer` as a literal `{tenantid}` placeholder
(no single fixed issuer exists across tenants), so `ValidIssuer` couldn't be set directly — used a
custom `IssuerValidator` that turns the placeholder into a regex instead. Also skipped Google's
`email_verified` check: Microsoft ID tokens carry no such claim on either work/school or personal
accounts, since Microsoft itself guarantees the email at the tenant/MSA level. Rejected an extra
Microsoft Graph `/me` call to double-check — adds a network round-trip and a `User.Read` scope for
a guarantee the token issuer already provides.

### Social login buttons: divider extracted out of GoogleLoginButton into a shared component

`GoogleLoginButton.vue` used to render its own "Or continue with" divider inline. Adding
`MicrosoftLoginButton.vue` alongside it would have stacked two dividers. Extracted the divider into
`SocialLoginDivider.vue` (shown once if any provider's client ID is configured), used by both
`LoginView.vue` and `RegisterView.vue` ahead of the provider buttons — kept each provider button
component only responsible for its own button/error/resend state.

### Refresh-token flow: interceptor delegates to the store instead of duplicating the call

`client.ts`'s 401-retry interceptor had its own inline `refreshClient.post('/api/auth/refresh', {})`
+ `setAuth(...)`, duplicating `stores/auth.ts`'s `refreshToken` action almost verbatim (pre-existing,
not introduced by the codegen migration — just carried over). Changed the interceptor to call
`auth.refreshToken()` directly — same `refreshClient` instance either way (no `isRefreshing`/
`failedQueue` interaction inside the action, so no behavior change), one implementation instead of
two that could silently drift. Also found the interceptor's refresh-failure path cleared auth but
not the `keep-login` localStorage flag (unlike `logout()`/`restoreSession`'s failure path) — harmless
(next boot just retries and fails once more) but inconsistent; added the same `clearKeepLoginPreference()`
call there. Covered both by a new test in `client.test.ts`.

### Removed the `{resource}-api.ts` wrapper layer; orchestration moved into Pinia stores

Per explicit user request, deleted `account-api.ts`/`auth-api.ts`/`health-api.ts`/`profile-api.ts`
— call sites now import generated clients directly. The side effects those wrappers used to own
(`setAuth()` after login/register/verifyEmail/externalLogin, the `refreshClient`-not-`apiClient`
special case for token refresh, session-id `number|string` normalization) had to move somewhere:
put auth orchestration into `stores/auth.ts` as actions (idiomatic Pinia, keeps it centralized
instead of scattered `useAuthStore().setAuth(...)` calls across 5+ views) rather than inlining at
every call site. Profile/health/accounts have no such side effects, so those call the generated
`get*()` factories directly with no intermediate layer. `PagedResult<T>` normalization (still
needed — see below) extracted to a pure `lib/paged-result.ts` helper, not a per-resource wrapper.

### OpenAPI codegen: orval + generated-but-gitignored, mirrors src/typings/

Frontend uses `orval` (full client, per user choice over types-only `openapi-typescript`) generating
into `frontend/src/api/generated/**`, gitignored and regenerated via a `postinstall` script — same
convention as the existing gitignored `src/typings/` (auto-import .d.ts files), avoids committed-
generated-code drifting from `shared/openapi/openapi.json`. Hand-written `{resource}-api.ts` stay as
a thin layer over generated calls (side effects like `setAuth()` after login aren't spec-expressible).
`authApi.refreshToken` stays fully hand-written — must use the separate `refreshClient` (no 401-retry
interceptor) to avoid recursing into `apiClient`'s own refresh logic; routing it through the shared
mutator would have silently broken that isolation.

### Backend OpenAPI doc generation: off by default on `dotnet build`

`Microsoft.Extensions.ApiDescription.Server`'s `OpenApiGenerateDocumentsOnBuild` defaults to `true`
(generates on every build), costing ~5-9s via a design-time host on every single `dotnet build` —
unacceptable given `dotnet build backend/StarterKit.sln` is the standard command used everywhere
(commands.md, CLAUDE.md's Verify step). Set to `false`; regeneration is an explicit opt-in command
(`-p:OpenApiGenerateDocumentsOnBuild=true`), same spirit as EF migrations being explicit, not automatic.

### Controller routes normalized to lowercase (not a codegen workaround)

4 of 8 controllers used `[Route("api/[controller]")]`, yielding PascalCase paths (`/api/Auth/login`)
in the OpenAPI spec — ASP.NET Core routing is case-insensitive so this never broke real traffic, but
it broke MSW test mocks and orval-generated client paths once codegen started reading the spec
literally, and was inconsistent with the other 4 controllers' explicit lowercase routes. Fixed by
declaring explicit lowercase routes on all 8 (root-cause fix, not a per-consumer workaround) — added
`QueryParameterCasingTransformer` for the same reason on DTO-bound query params (PageNumber etc.).

### Generated int64 fields (`number | string`) normalized at the api-layer boundary, not propagated

.NET's OpenAPI generator widens `long`/`int` response fields to a `number | string` union (JS-safe-
integer interop) — e.g. `PagedResultOf*Dto` fields, `SessionDto.id`. Rather than let that union type
leak into views/stores (pagination arithmetic, session-id comparisons), `account-api.ts`/`auth-api.ts`
coerce to plain `number` before returning, keeping `PagedResult<T>`/`SessionDto` as stable hand-written
frontend-only shapes. Everything else in `types.ts` is now a direct re-export of the generated model
type under its existing name — kept the migration transparent to every existing call site.

Rate limiting keys on `Connection.RemoteIpAddress`, which is the proxy's own IP for every request
once the app sits behind any reverse proxy/LB — collapsing all real clients into one rate-limit
bucket (and breaking IP-based audit/abuse tracking). Added `UseForwardedHeaders()` bound from a
new `ForwardedHeadersSettings:KnownProxies`/`KnownNetworks` config (both default empty — ASP.NET
Core's own default of loopback-only trust applies, so forwarded headers are silently ignored,
i.e. no-op, until an operator explicitly lists their actual proxy/LB IP or CIDR). Rejected
clearing `KnownProxies`/`KnownNetworks` to trust any forwarder unconditionally — that lets any
client spoof `X-Forwarded-For` to bypass the rate limiter entirely if the app is ever reachable
directly (no proxy in front, e.g. misconfigured deployment or direct port exposure). This is a
starter kit with no fixed deployment topology, so the trusted-proxy set has to be a per-deployment
config knob, not a hardcoded assumption.

### CORS: explicit allowlist instead of reflect-any-origin

`Program.cs` used `SetIsOriginAllowed(_ => true)` + `AllowCredentials()` — reflects any Origin
back with credentials enabled, which defeats the browser's same-origin protections entirely
(any site can call the API using the victim's cookies). Replaced with `WithOrigins()` bound from
a new `CorsSettings:AllowedOrigins` config array (same required-config pattern as `JwtSettings` —
throws at startup if missing/empty, rather than silently falling back to permissive). Default
dev value is `http://localhost:5173` (frontend's Vite port).
