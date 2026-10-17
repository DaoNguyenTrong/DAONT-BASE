# HDSD — Giai đoạn 2: Truy cập và triển khai — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Phục vụ site HDSD tại `/guide/` trong image `web`. Mặc định chỉ người đã đăng nhập xem được (nginx `auth_request`); `GUIDE_PUBLIC=true` mở công khai. App có lối vào HDSD và trạm `/guide-auth` đưa người dùng qua đăng nhập rồi quay lại đúng trang.

**Architecture:**
- **Backend:** thêm `GET /api/auth/check`, chỉ xác thực JWT (scheme Bearer/cookie, không nhận API key).
- **nginx:** gọi endpoint này qua subrequest. 401 thì chuyển về `/#/guide-auth?next=…`, API lỗi thì hiện trang lỗi tĩnh.
- **Chọn chế độ:** entrypoint của container chép snippet private hoặc public theo `GUIDE_PUBLIC`.
- **Site:** theme kiểm lại phiên trước mỗi lần chuyển trang phía client khi `/guide/_mode` là `private`.

**Tech Stack:** .NET 10 (xUnit, Testcontainers), Vue 3 + vitest + MSW, VitePress theme (bun test), nginx:alpine, Docker.

**Spec:** `docs/superpowers/specs/2026-10-15-user-guide-design.md` (§9.1–§9.4, §9.6, §11, §14 giai đoạn 2).

## Global Constraints

- Nhánh `feature/user-guide-access` cắt từ `dev` (đã có giai đoạn 1). Không commit `docs/superpowers/**`.
- Commit theo Conventional Commits, stage từng file theo đường dẫn. Mỗi message kết thúc bằng:
  ```
  Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
  Claude-Session: https://claude.ai/code/session_01NYrpuCpEweNouDcJhmpiAz
  ```
- C#: theo `code-conventions.md` (không `var`, CancellationToken). Test API theo `testing.md` (`Method_Scenario_Expected`, xUnit `Assert`).
- Frontend: auto-import, chuỗi người dùng nhìn thấy có đủ `vi`/`en` trong `src/locales/{vi,en}.ts`; test khẳng định chữ qua locale (`en.xxx`).
- Đường dẫn: HDSD ở `/guide/` (en: `/guide/en/`); trạm trung chuyển `/#/guide-auth?next=<đường dẫn thô>`; chế độ đọc ở `/guide/_mode` (`private` | `public`, text/plain).
- File mới của image đặt trong `frontend/docker/` (cạnh `40-write-runtime-config.sh`); entrypoint mới là `41-guide-mode.sh`.
- `GUIDE_PUBLIC`: `true` → public; `false` hoặc rỗng → private; giá trị khác → private kèm một dòng log cảnh báo.

## Review Focus

1. **Một người đang đọc HDSD thì phiên hết hạn (sau 15 phút)**: lần chuyển trang client tiếp theo phải đưa họ qua `/guide-auth` rồi quay lại đúng trang, không lặp vô hạn. Đã thêm test vào Task 5 (guard) và Task 3 (không chuyển tiếp khi kiểm thất bại).
2. **`next` độc hại** (`//evil`, `/guide/../x`, `https://…`, CRLF mã hoá `%0d%0a`): không được thoát khỏi `/guide/` và không được chèn header. Đã thêm test vào Task 2, và Task 6 kiểm header `Location` thô.
3. **API sập ở chế độ private**: trả trang lỗi tĩnh (5xx), không lộ nội dung HDSD. Đã thêm vào script kiểm của Task 6.
4. **`GUIDE_PUBLIC` viết sai** (`True`, `abc`): phải fail-closed về private và có log. Đã thêm vào script kiểm của Task 6.
5. **Request chỉ mang `X-Api-Key`**: endpoint trả 401 dù khoá hợp lệ. Đã thêm test vào Task 1.

---

### Task 1: `GET /api/auth/check` (chỉ phiên người dùng)

**Files:**
- Modify: `backend/src/StarterKit.API/Controllers/AuthController.cs` (thêm action sau `Logout`)
- Modify: `backend/src/StarterKit.API/Middleware/UserTimeZoneMiddleware.cs:20-21`
- Modify: `backend/tests/StarterKit.API.Tests/Controllers/AuthControllerTests.cs` (mục `// Check` sau `// Logout`)
- Modify: `shared/openapi/openapi.json` (export lại)

**Interfaces:**
- Produces: `GET /api/auth/check` → `204` khi request có JWT hợp lệ (header `Authorization: Bearer` hoặc cookie `access_token`), `401` trong mọi trường hợp khác. Action: `public IActionResult Check() => NoContent();` với `[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]`, `[HttpGet("check")]`, `ProducesResponseType` 204 và `ProblemDetails` 401. XML doc nêu: đây là subrequest `auth_request` của nginx cho `/guide/`.
- `UserTimeZoneMiddleware` cho `/api/auth/check` đi qua khi thiếu `X-TimeZone`, giống `/api/health`.

- [ ] **Step 1: Viết test (đỏ)** trong `AuthControllerTests.cs`, mục `// Check (nginx auth_request for /guide/)`:
  - `Check_AccessTokenCookie_ReturnsNoContent`: seed tài khoản bằng `AuthTestHelper.SeedConfirmedAccountAsync`, header `Cookie: access_token=<MintAccessToken>` → 204.
  - `Check_BearerToken_ReturnsNoContent`: header `Authorization: Bearer <MintAccessToken>` → 204.
  - `Check_Unauthenticated_ReturnsUnauthorized`: không có gì → 401.
  - `Check_ApiKeyOnly_ReturnsUnauthorized`: dựng như `ApiKeyHeader_AuthenticatesAgainstCombinedPolicy` (seed organization và API key hợp lệ, header `X-Api-Key`) → 401.
  - `Check_WithoutTimeZoneHeader_StillAuthenticatesViaCookie`: `client.DefaultRequestHeaders.Remove("X-TimeZone")`. Có cookie hợp lệ → 204; bỏ cookie → 401.
- [ ] **Step 2: Chạy, xác nhận đỏ.** `dotnet build backend/StarterKit.sln --no-restore -m:1 && dotnet test backend/tests/StarterKit.API.Tests --no-restore -m:1 --filter "FullyQualifiedName~AuthControllerTests.Check_"` → 5 FAIL (404, riêng ca timezone là 400/404).
- [ ] **Step 3: Implement action và miễn trừ timezone.**
- [ ] **Step 4: Chạy lại filter** → 5 PASS.
- [ ] **Step 5: Export OpenAPI rồi chạy toàn bộ backend.** `dotnet build backend/src/StarterKit.API/StarterKit.API.csproj --no-restore -m:1 -p:OpenApiGenerateDocumentsOnBuild=true`; `git diff --stat shared/openapi/openapi.json` có thay đổi (path `/api/auth/check`). `bun install --cwd frontend` để sinh lại client. `backend/scripts/test.sh` → toàn bộ PASS.
- [ ] **Step 6: Commit** `feat(auth): add a session-only check endpoint for the user guide gate` (gồm 3 file C# và `shared/openapi/openapi.json`).

### Task 2: `safeGuideTarget`

**Files:**
- Modify: `frontend/src/lib/safe-redirect.ts`
- Modify: `frontend/tests/unit/lib/safe-redirect.test.ts`

**Interfaces:**
- Produces: `export const GUIDE_HOME = '/guide/'` và `export function safeGuideTarget(value: unknown): string`. Trả về `value` khi giá trị là chuỗi, bằng `/guide` hoặc bắt đầu bằng `/guide/`, không chứa `//`, `\`, `..`, ký tự điều khiển `[\u0000-\u001f\u007f]` hay dạng mã hoá `%0d`/`%0a`/`%00` (không phân biệt hoa thường). Mọi trường hợp khác trả về `GUIDE_HOME`.

- [ ] **Step 1: Viết test (đỏ)**: `describe('safeGuideTarget')`:
  - `'returns guide paths unchanged'`: `/guide`, `/guide/`, `/guide/organizations/add-member`, `/guide/en/faq`.
  - `it.each`, mỗi ca trả về `'/guide/'`:
    - giá trị không phải chuỗi (`undefined`, `null`, `['/guide/x']`, `42`);
    - đường dẫn ngoài guide (`/organizations`, `/guidex`, `/`);
    - URL tuyệt đối (`https://evil.example/guide/`);
    - protocol-relative (`//evil.example`, `/guide//evil.example`);
    - backslash (`/guide/\\evil`);
    - traversal (`/guide/../organizations`);
    - ký tự điều khiển (`/guide/a\r\nSet-Cookie: x`);
    - CRLF đã mã hoá (`/guide/%0d%0aSet-Cookie:x`, `/guide/%0A`).
- [ ] **Step 2: Chạy** `bun run --cwd frontend test:run tests/unit/lib/safe-redirect.test.ts` → FAIL (không có export).
- [ ] **Step 3: Implement** trong `safe-redirect.ts`, kèm comment nêu nginx chuyển về đâu và vì sao chặn từng dạng.
- [ ] **Step 4: Chạy lại** → PASS.
- [ ] **Step 5: Commit** `feat(guide): validate the user guide return target`.

### Task 3: Route `/guide-auth` và `GuideAuthView`

**Files:**
- Create: `frontend/src/views/GuideAuthView.vue`
- Modify: `frontend/src/router/index.ts` (route trước `not-found`)
- Modify: `frontend/src/locales/en.ts`, `frontend/src/locales/vi.ts` (khối mới `guideAuth`)
- Create: `frontend/tests/views/GuideAuthView.test.ts`
- Modify: `frontend/tests/unit/router/guards.test.ts`

**Interfaces:**
- Consumes: `safeGuideTarget` (Task 2), `GET /api/auth/check` (Task 1).
- Produces:
  - Route `{ path: '/guide-auth', name: 'guide-auth', component: () => import('@/views/GuideAuthView.vue'), meta: { requiresAuth: true } }`, không có `sidebar`/`breadcrumbKey`.
  - Locale `guideAuth: { opening: string; failed: string }`:
    - vi: "Đang mở hướng dẫn sử dụng…" / "Không mở được hướng dẫn sử dụng. Hãy thử lại sau.";
    - en: "Opening the user guide…" / "Couldn't open the user guide. Please try again later.".
  - `GuideAuthView` khi mount gọi `fetch('/api/auth/check', { credentials: 'same-origin', cache: 'no-store' })`, đường dẫn tương đối, không dùng `apiBaseUrl`: nginx kiểm cookie của chính origin này. 204 → `window.location.replace(safeGuideTarget(route.query.next))`. Kết quả khác hoặc lỗi mạng → hiện `guideAuth.failed` và nút `common.goHome` (`router.push(useHomeRoute())`). Bố cục giống `NotFoundView` (khung giữa màn hình, `AppControls` góc phải); khi đang kiểm thì hiện `n-spin` và `guideAuth.opening`.

- [ ] **Step 1: Viết test (đỏ).** `GuideAuthView.test.ts`: dùng `renderComponent(GuideAuthView, { initialRoute: '/guide-auth?next=…' })`, MSW `http.get('*/api/auth/check', …)`. Mock `window.location` bằng `Object.defineProperty`, như `tests/unit/lib/session-exit.test.ts`, với `replace: vi.fn()`.
  - `it('opens the requested guide page once the session check passes')`: 204, `next=/guide/organizations/add-member` → `replace` được gọi với `'/guide/organizations/add-member'`.
  - `it('falls back to the guide home for an unsafe next')`: 204, `next=//evil.example` → `replace('/guide/')`.
  - `it('shows an error instead of redirecting when the check fails')`: 401 → `wrapper.text()` chứa `en.guideAuth.failed`, `replace` không được gọi.
  - `it('shows an error when the API is unreachable')`: `HttpResponse.error()` → như trên.

  `guards.test.ts`: `it('requires a session for the guide hand-off route')` → `router.resolve('/guide-auth').meta.requiresAuth` là `true` (import router như các test router hiện có; nếu việc import router kéo theo view, dùng `stubView` theo `commands.md`).
- [ ] **Step 2: Chạy** `bun run --cwd frontend test:run tests/views/GuideAuthView.test.ts tests/unit/router/guards.test.ts` → FAIL.
- [ ] **Step 3: Implement view, route, locale.**
- [ ] **Step 4: Chạy lại** → PASS, rồi `bun run --cwd frontend test:run` → toàn bộ PASS.
- [ ] **Step 5: Commit** `feat(guide): add the sign-in hand-off route for the user guide`.

### Task 4: Lối vào HDSD trên header và proxy dev

**Files:**
- Modify: `frontend/src/layouts/AppHeader.vue`
- Modify: `frontend/src/locales/en.ts`, `frontend/src/locales/vi.ts` (`nav.userGuide`)
- Modify: `frontend/tests/layouts/AppHeader.test.ts`
- Modify: `frontend/vite.config.ts` (`server.proxy`)

**Interfaces:**
- Produces:
  - `nav.userGuide`: vi "Hướng dẫn sử dụng", en "User guide".
  - Header có nút icon `Help` (`@vicons/tabler`) trong `n-tooltip`, đặt trước `NotificationBell`, render là thẻ `<a>` (`n-button tag="a"`) với `href` = `/guide/` (vi) hoặc `/guide/en/` (en) theo `useLocaleStore().currentLocale`, `target="_blank"`, `rel="noopener"`, `aria-label` = `t('nav.userGuide')`, `data-testid="app-header-user-guide"`.
  - `vite.config.ts`: `server.proxy = { '/guide': { target: 'http://localhost:5174' } }`. Chạy `bun run --cwd frontend/user-guide/site dev` song song để xem HDSD qua app khi dev; dev không có bước kiểm phiên.

- [ ] **Step 1: Viết test (đỏ)** trong `AppHeader.test.ts`:
  - `it('links to the user guide in a new tab')`: tìm `[data-testid="app-header-user-guide"]`, kiểm `href` là `'/guide/'`, `target` là `'_blank'`, `rel` chứa `'noopener'`, `aria-label` bằng `en.nav.userGuide`.
  - `it('opens the English guide when the app is in English')`: đặt locale `en` (store `useLocaleStore().setLocale('en')` qua pinia test, hoặc i18n global locale), kiểm `href` là `'/guide/en/'`.

  Nếu test đang dùng stub `NotificationBell`, giữ như cũ.
- [ ] **Step 2: Chạy** `bun run --cwd frontend test:run tests/layouts/AppHeader.test.ts` → FAIL.
- [ ] **Step 3: Implement nút, locale và proxy.**
- [ ] **Step 4: Chạy lại** → PASS. `bun run --cwd frontend test:run` toàn bộ PASS. `bun run --cwd frontend build-only && bun run --cwd frontend type-check` → xanh.
- [ ] **Step 5: Commit** `feat(guide): link the user guide from the app header`.

### Task 5: Guard phía client của site

**Files:**
- Create: `frontend/user-guide/site/.vitepress/theme/auth-guard.ts`
- Create: `frontend/user-guide/site/tests/auth-guard.test.ts`
- Modify: `frontend/user-guide/site/.vitepress/theme/index.ts` (`enhanceApp`)

**Interfaces:**
- Produces:
  ```ts
  export type GuardDeps = {
    mode: () => Promise<string>          // text of /guide/_mode, fetched once
    check: () => Promise<number>         // status of GET /api/auth/check
    navigate: (to: string) => void       // full page load
  }
  export function createAuthGuard(deps: GuardDeps): (to: string) => Promise<boolean | void>
  ```
  Hàm trả về được gán cho `router.onBeforeRouteChange`:
  - lần gọi đầu bỏ qua: VitePress gọi `router.go()` cho chính trang vừa tải, mà trang đó nginx đã kiểm;
  - `mode` khác `'private'` → cho qua;
  - `check()` trả 401 → `navigate(to)` và trả `false`;
  - lỗi (`mode` hoặc `check` reject) → cho qua, vì lượt tải trang kế tiếp vẫn qua nginx.

  `mode()` chỉ gọi một lần, kết quả được dùng lại. `enhanceApp`: bỏ qua khi `import.meta.env.SSR || import.meta.env.DEV`. Nếu không, cài guard với `mode: () => fetch(withBase('/_mode'), { cache: 'no-store' }).then(r => r.ok ? r.text() : '')`, `check: () => fetch('/api/auth/check', { credentials: 'same-origin', cache: 'no-store' }).then(r => r.status)`, `navigate: (to) => { window.location.href = to }`.

- [ ] **Step 1: Viết test (đỏ)**, `bun test`:
  - `'skips the first navigation (the page nginx just served)'`: `check` không được gọi ở lần đầu.
  - `'lets navigation through in public mode without checking'`.
  - `'reloads the target through nginx when the session has lapsed'`: lần 2, mode `private`, check 401 → `navigate('/guide/faq')`, kết quả `false`.
  - `'lets navigation through when the session is valid'`: 204 → không gọi `navigate`, kết quả khác `false`.
  - `'lets navigation through when the network fails'`: `check` reject → không gọi `navigate`.
  - `'fetches the mode only once'`: ba lần chuyển trang, `mode` được gọi đúng 1 lần.
- [ ] **Step 2: Chạy** `bun run --cwd frontend/user-guide/site test tests/auth-guard.test.ts` → FAIL.
- [ ] **Step 3: Implement và nối vào `enhanceApp`.**
- [ ] **Step 4: Chạy lại** → PASS. `bun run --cwd frontend/user-guide/site test && bun run --cwd frontend/user-guide/site build` → xanh.
- [ ] **Step 5: Commit** `feat(guide): re-check the session on client-side page changes`.

### Task 6: nginx, image và compose

**Files:**
- Modify: `frontend/nginx.conf` (`map` ở đầu file; `include` trong `server`)
- Create: `frontend/docker/guide-private.conf`, `frontend/docker/guide-public.conf`, `frontend/docker/guide-unavailable.html`, `frontend/docker/41-guide-mode.sh`
- Create: `frontend/docker/verify-guide-access.sh` (kiểm tích hợp, chạy local, không chạy trong CI)
- Modify: `frontend/Dockerfile` (stage `guide`, stage `final`)
- Modify: `.dockerignore`
- Modify: `docker-compose.prod.yml` (service `web`), `.env.prod.example`

**Interfaces:**
- Consumes: `/api/auth/check` (Task 1); `dist/` của site.
- Produces (nội dung theo §9.3 của spec):
  - **Snippet private:**
    - `location = /guide` → 301;
    - `location = /guide/_mode` (`default_type text/plain`, `Cache-Control: no-store`, `return 200 'private'`);
    - `location /guide/`:
      - `auth_request /_guide_auth`;
      - `error_page 401 = @guide_login`;
      - `error_page 500 502 503 504 /_guide_unavailable.html`;
      - `error_page 404 /guide/404.html`;
      - `add_header Cache-Control "private, no-cache" always`;
      - `try_files $uri $uri.html $uri/ =404`;
      - lồng `location /guide/assets/` (`private, max-age=31536000, immutable`).
    - `location = /_guide_auth` (internal; `proxy_pass http://api:8080/api/auth/check`; bỏ body; `Content-Length ""`; `Cookie $http_cookie`; `X-Api-Key ""`; Host, X-Forwarded-Proto);
    - `location = /_guide_unavailable.html` (internal, `no-store`);
    - `location @guide_login` → `return 302 /#/guide-auth?next=$guide_next`.
  - **Snippet public:** `/guide` → 301; `_mode` trả `public`; `/guide/` dùng `try_files` và `error_page 404`, HTML `no-cache`, assets `public, max-age=31536000, immutable`.
  - **`41-guide-mode.sh`:** `GUIDE_PUBLIC=true` → chép snippet public đè `/etc/nginx/snippets/guide.conf`. `false`/rỗng → giữ private. Giá trị khác → `echo "41-guide-mode.sh: GUIDE_PUBLIC=<v> is not true/false — serving the user guide privately" >&2` và giữ private.
  - **Dockerfile:**
    - `FROM build AS guide`, `WORKDIR /build/frontend/user-guide/site`, `RUN bun install --frozen-lockfile && bun run build`;
    - `final` chép `dist/` vào `/usr/share/nginx/html/guide`, hai snippet vào `/etc/nginx/snippets-available/`, snippet private vào `/etc/nginx/snippets/guide.conf`, trang lỗi vào `/usr/share/nginx/html/_guide_unavailable.html`, script entrypoint với `--chmod=755`.
  - **`.dockerignore`:** `!frontend/user-guide/site/src/**/*.md` ngay sau `**/*.md`, cộng `frontend/user-guide/site/.vitepress/cache/`, `frontend/user-guide/site/src/public/brand/`, `**/brand-tokens.generated.css`.
  - **compose `web.environment`:** `GUIDE_PUBLIC: ${GUIDE_PUBLIC:-false}`. `.env.prod.example`: biến `GUIDE_PUBLIC=` kèm comment (private cần API cùng origin; giới hạn 15 phút của cookie).
  - **`verify-guide-access.sh`:** dựng image `web` từ repo root, tạo network tạm, chạy một container stub tên `api` (nginx:alpine; `location = /api/auth/check` trả 204 khi cookie chứa `access_token=good`, ngược lại 401), rồi kiểm bằng `curl`. Script in `PASS`/`FAIL` cho từng ca, exit ≠ 0 nếu có ca hỏng, và dọn container/network khi thoát (`trap`).

- [ ] **Step 1: Viết `verify-guide-access.sh` với các ca sau (đỏ trước khi có snippet):**
  1. private, `/guide/faq` không cookie → `302`, `Location: /#/guide-auth?next=/guide/faq`.
  2. private, cookie `access_token=good` → `200`, body chứa `Câu hỏi thường gặp`.
  3. private, `/guide` → `301` tới `/guide/`.
  4. private, `/guide/_mode` → `private`.
  5. private, `/guide/%0d%0aSet-Cookie:x` → `302` và header `Location` không có dòng `Set-Cookie`.
  6. private, dừng container `api` → `/guide/faq` trả `500`, body là trang lỗi, không chứa `Câu hỏi thường gặp`.
  7. `GUIDE_PUBLIC=true` → `/guide/faq` không cookie trả `200`; `_mode` trả `public`.
  8. `GUIDE_PUBLIC=abc` → `/guide/_mode` trả `private` và log container có dòng cảnh báo.
  9. `/` vẫn trả app (`200`, có `<div id="app"`).
- [ ] **Step 2: Chạy** `frontend/docker/verify-guide-access.sh` → FAIL (image hiện tại chưa phục vụ `/guide/`).
- [ ] **Step 3: Implement nginx, snippet, script, Dockerfile, `.dockerignore`, compose, env.**
- [ ] **Step 4: Chạy lại script** → 9/9 PASS. `docker compose -f docker-compose.prod.yml config >/dev/null` → hợp lệ.
- [ ] **Step 5: Commit** `feat(guide): serve the user guide from the web image behind the session gate`.

### Task 7: Tài liệu và gate đầy đủ

**Files:**
- Modify: `frontend/user-guide/README.md` (mục mới "Triển khai và quyền truy cập")
- Modify: `README.md` (mục Production: `GUIDE_PUBLIC`)
- Modify: `.claude/rules/commands.md` (mục User guide: xem qua app khi dev, `verify-guide-access.sh`)

- [ ] **Step 1: Viết tài liệu.**
  - README của guide:
    - hai chế độ và giá trị `GUIDE_PUBLIC`;
    - ràng buộc cùng origin;
    - luồng `/guide-auth` và giới hạn 15 phút của cookie;
    - trang lỗi khi API sập;
    - dev qua proxy `/guide` của app;
    - checklist tích hợp, tức các ca của `verify-guide-access.sh` cộng hai ca tay (đăng nhập thật rồi quay lại đúng trang; chuyển `vi`/`en` qua nút "?").
  - `README.md`: một dòng về `GUIDE_PUBLIC` trong Production.
  - `commands.md`: lệnh dev song song và lệnh kiểm.
- [ ] **Step 2: Chạy gate.** `scripts/test-gate.sh` → exit 0.
- [ ] **Step 3: Commit** `docs(guide): document user guide deployment and access modes`.
