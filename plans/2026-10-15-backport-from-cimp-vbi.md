# Rà soát nâng cấp DAONT-BASE từ kinh nghiệm CIMP-VBI

- **Ngày rà soát:** 2026-10-15
- **Base:** `DAONT-BASE` @ `79a1cf1` (v1.5.2)
- **Dự án:** `CIMP-VBI` @ `fc5cb74a`
- **Baseline đã xác minh:** commit đầu tiên của CIMP-VBI (`60187272`) **trùng từng byte** với DAONT-BASE HEAD. Mọi so sánh tính từ commit đổi tên `283e62b2`.
- **Phạm vi:** khoảng 1.300 commit không phải merge. Đã loại toàn bộ code nghiệp vụ: hazard, alert, lookup, verification, asset, radar, lightning, dashboard.
- **Cách xác minh:** từng mục đã được đối chiếu với code của base (đổi `CimpVbi` thành `StarterKit`). "✔" nghĩa là base vẫn còn đúng đoạn code chưa sửa.
- **Quy ước:**
  - Ưu tiên: **P0** = bảo mật hoặc rò bí mật; **P1** = bug thật hoặc base đang sai; **P2** = cải tiến đáng có; **P3** = nice-to-have hoặc phòng ngừa.
  - Công sức: S < ½ ngày, M ½–2 ngày, L > 2 ngày.

## Thống kê

| Lĩnh vực | P0 | P1 | P2 | P3 | Tổng |
|---|---|---|---|---|---|
| Backend (src) | – | 4 | 6 | 12 | 22 |
| Frontend (src) | – | 4 | 6 | 7 | 17 |
| Hạ tầng test (BE + FE) | – | 6 | 6 | 9 | 21 |
| DevOps / Docker / CI / config | 1 | 2 | 6 | 4 | 13 |
| Tooling agent / tài liệu | – | 7 | 7 | 6 | 20 |
| **Tổng** (đã gộp trùng) | **1** | **23** | **31** | **38** | **93** |

Các mục mà nhiều nhánh cùng tìm ra chỉ được tính một lần, ở lĩnh vực chính của nó. Ví dụ: cookie, ArchitectureTests, FastPasswordHasher, runtime config, seam naive-ui.

---

## 🔴 P0 — làm ngay

| # | Mục | Mô tả / triệu chứng | File base | Xác minh | Công sức |
|---|---|---|---|---|---|
| 0.1 | **`.dockerignore` không chặn được `appsettings.json` thật** | Docker neo pattern vào gốc build context. Vì vậy `appsettings.json` chỉ khớp file ở gốc repo, còn `backend/src/StarterKit.API/appsettings.json` (chuỗi kết nối DB, JWT secret…) vẫn bị `COPY backend/src/` chép vào image. Web SDK publish file này ra `/app`, tức là **secret nằm trong image** mỗi khi build compose prod từ máy dev. `frontend/.env*` cũng lọt vào context, nhưng không làm mục này nặng thêm vì giá trị `VITE_*` vốn đã công khai phía client. Điều này phá vỡ quyết định "appsettings bind-mount, không bake" (2026-08-07). CIMP vẫn còn lỗi này; image CI an toàn chỉ vì CI build từ clone sạch. **Sửa:** `**/appsettings*.json` + `!**/appsettings*.Example.json`, `**/.env*` + các dòng `!**/.env*.example`, `**/coverage/`. | `.dockerignore` | ✔ Đã kiểm: csproj không loại appsettings khỏi publish (Web SDK mặc định publish `appsettings*.json`), Dockerfile không có bước `rm`, và stage final chép nguyên `/app/publish`. Chưa chạy `docker build`; xác nhận bằng `docker run --rm --entrypoint ls <img> /app`. | S |

---

## Backend (`backend/src`)

| # | Ưu tiên | Mục | Triệu chứng trong base | SHA CIMP | File base (dưới `backend/src/`) | Công sức |
|---|---|---|---|---|---|---|
| B1 | **P1** | **Cache: mục cũ sống sót sau invalidate; Redis sập kéo theo API sập** | (a) Trong `GetOrSetAsync`, generation được đọc **sau** factory. Nếu invalidate chen giữa, giá trị cũ bị ghi dưới generation mới, nên quyền vừa thu hồi vẫn còn hiệu lực đến hết TTL. (b) `INCR` mất key thì đếm lại từ 1, làm hồi sinh mục cũ. (c) `RedisCacheService` không có try/catch, Redis down thì mọi request có xác thực đều lỗi. **Sửa:** đọc generation trước factory; token `SET NX` TTL 24h; circuit 10s bỏ qua Redis; `expiration` bắt buộc; SystemSettings dùng `InvalidateScopeAsync`. *Cả hai repo vẫn còn:* `InvalidateMemberAsync` của PermissionResolver/TenantAccessService dùng `RemoveAsync`. | `6181b357` | `Infrastructure/Services/Caching/*`, `Application/Common/Interfaces/ICacheService.cs`, `CacheSettings.cs`, `SystemSettingsService.cs` | M |
| B2 | **P1** | **Email phân biệt hoa/thường** | Có thể cùng tồn tại `A@x.com` và `a@x.com`. Login, đăng ký, external login và mời thành viên đều so sánh phân biệt hoa/thường. **Sửa:** `Account.NormalizeEmail` dùng cho mọi đường ghi và tra; migration hạ chữ thường (bỏ qua dòng va chạm); Google thiếu claim email thì ném lỗi. | `7d9818d7` | `Domain/Entities/Account.cs`, `AccountService`, `AuthService`, `RegistrationService`, `OrganizationMembershipService`, `GoogleAuthProvider` | M |
| B3 | **P1** | **Cookie hardcode `Secure=true` + `SameSite=None`** | Khi trang phục vụ qua HTTP thuần từ origin khác `localhost` và phía trước không có TLS terminator (đúng cấu hình compose prod mặc định, chỉ mở :80), trình duyệt bỏ cookie `Secure`. Kết quả: login trả 200 nhưng các request sau đều 401. **Sửa:** `AuthCookieSettings` (mặc định vẫn an toàn); helper `AuthCookies.CreateOptions/Clear`; khởi động thì chặn tổ hợp `Secure=false` + `None`. Kèm README hướng dẫn chạy HTTP. | `47934078`, `9872de84` | `API/Controllers/AuthController.cs:248-285`, `Program.cs`, `appsettings.Example.json` | S–M |
| B4 | **P1** | **Phân trang không ổn định** | Audit dùng chung một `now` cho cả `SaveChanges`, nên ghi hàng loạt tạo nhiều dòng trùng `CreatedAt`. Một dòng có thể xuất hiện ở hai trang hoặc biến mất. **Sửa:** `ThenByDescending(Id)`. | `f4cae501` | `Infrastructure/Persistence/Repositories/GenericRepository.cs:69,94` | S |
| B5 | P2 | Trả 499 khi client hủy request | Mỗi lần F5 hay đổi filter lại sinh một log Error 500. Thêm `catch OperationCanceledException when RequestAborted` → log Debug, trả 499. | `73e6e2c4` | `API/Middleware/ExceptionHandlingMiddleware.cs` | S |
| B6 | P2 | Chuẩn hóa username về chữ thường | Login so sánh `==` phân biệt hoa/thường. Migration phải dừng nếu gặp trùng, không gộp ngầm. | `76afcbde` | `Account.cs`, `AuthService`, `RegistrationService`… | S–M |
| B7 | P2 | **ArchitectureTests + phá vòng namespace** | Vòng `IRealtimeNotifier`↔Notifications và `EntityMapper`↔Accounts/Notifications; controller phải import Infrastructure chỉ để lấy `AuthorizationPolicies`. **Sửa:** test reflection (hướng tầng, pattern entity, exception kế thừa `AppException`, ratchet vòng namespace); dời `AuthorizationPolicies` sang Application; mapper tách theo feature. | `c97ea316`, `6e5ff1e2` | `API/Controllers/{Files,Organizations}Controller.cs`, `Application/Common/Mappings/EntityMapper.cs`… | M |
| B8 | P2 | Khóa tài khoản tạm thời khi sai mật khẩu nhiều lần | Hiện chỉ có rate limit theo IP. Mặc định 5 lần trong 15 phút thì khóa 15 phút. Chỉ báo lý do khóa **sau khi** mật khẩu đúng. | `10361220`, `68326261` | `Account.cs`, `AuthService`, migration | M |
| B9 | P2 | Thông báo: `NotifyManyAsync`, `DedupeKey`, `ReadState` | Gửi cho N người tốn N lần `SaveChanges` và N job; không có idempotency; tham số `Search` nhận vào nhưng không dùng; `bool? unreadOnly` không lọc được "Đã đọc". *Đổi hợp đồng API, phải regen orval.* | `7071c869` (phần chung) | `Services/Notifications/*` | M |
| B10 | P2 | `appsettings.Example.json` thiếu khóa mà code đang bind | Thiếu `JwtSettings.RefreshTokenReuseGraceSeconds`, `RateLimiterSettings`, `PermissionResolverSettings`. | `73abbc98` | `API/appsettings.Example.json` | S |
| B11 | P3 | Gom `PasswordPolicy` một chỗ, bỏ `MinLength` ở Login/CurrentPassword | Nếu sau này đổi chính sách, tài khoản cũ sẽ không đăng nhập được. | `7a14c26a` (chỉ cơ chế) | `LoginRequest`, `ChangePasswordRequest` | S |
| B12 | P3 | Converter `IReadOnlySet` lồng trong record khi đọc Redis | Hiện tiềm ẩn. | `f1c25055` | `RedisCacheService.cs` | S |
| B13 | P3 | `AccountParams` bỏ `Status` | Bẫy mở lại tài khoản bị khóa. | `7d9818d7` | `Account.cs` | S |
| B14 | P3 | `IUnitOfWork.ExecuteInTransactionAsync` | | | `UnitOfWork.cs` | S |
| B15 | P3 | `ServiceUnavailableException` → 503 | | | `AppExceptionHttpMapper.cs` | S |
| B16 | P3 | Pattern lệnh CLI trong `Program.cs` | Dùng cho seed, import, tạo admin. | `dd99815a` | `Program.cs` | S |
| B17 | P3 | Nâng Npgsql 10.0.1 → 10.0.3 | | `5653a908` | csproj | S |
| B18 | P3 | Tìm kiếm không dấu tiếng Việt (`SearchText`) | | `5b68bacd` | mới | S |
| B19 | P3 | *(Module tùy chọn)* Hạ tầng PDF Razor + Gotenberg (cô lập mạng) | Phải bỏ brand WeatherPlus. | `07efed9c`, `a9327c78` | mới | L |
| B20 | P3 | *(Module tùy chọn)* Excel ClosedXML + hợp đồng endpoint trả file | Chỉ khai content-type xlsx cho response 200. | `8d3219c0`, `23ad524a` | mới | M |
| B21 | P3 | *(Tùy chọn)* System admin + bootstrap; quên mật khẩu bằng OTP | | `dd99815a`, `d38e9036` | mới | L |
| B22 | P3 | Ghi chú Hangfire | Hai entry point của cùng một job phải dùng chung `resource` của `[DisableConcurrentExecution]`; mọi queue được enqueue phải có trong `options.Queues`; seeder phải idempotent. | `b27ad551` | `rules/code-conventions.md` | S |

---

## Frontend (`frontend/src`)

| # | Ưu tiên | Mục | Triệu chứng trong base | SHA CIMP | File base (dưới `frontend/src/`) | Công sức |
|---|---|---|---|---|---|---|
| F1 | **P1** | **Reload trang mỗi khi phiên kết thúc** | Logout, đổi mật khẩu và hết phiên chỉ gọi `router.replace`. Pinia store vẫn sống, nên **tài khoản kế tiếp có thể thấy dữ liệu của tài khoản trước** (unreadCount, organizations, roles…). Đã xác minh ở mức code, chưa tái hiện trên base. **Sửa:** `lib/session-exit.ts` → `reloadToLogin()` theo nguyên tắc "lần gọi đầu thắng". *Gợi ý thêm:* `switchOrganization` cũng nên reload, CIMP chưa làm. | `2755b8cf` | `stores/auth.ts`, `App.vue:38-43`, `layouts/AppSidebar.vue:71-79`, `components/ProfileDialog.vue` | M |
| F2 | **P1** | **Poll thông báo tự sống lại sau khi rời app shell** | `connection.stop()` bắn `onclose`, `onclose` gọi `resume()` và dựng lại interval. Kết quả: ở trang login cứ 30 giây lại gọi API một lần, nhận 401, rồi hiện toast kỹ thuật "Refresh token là bắt buộc". **Sửa:** cờ `isActive`; thêm `fetchUnreadCountInBackground()` có `.catch`. | `126215b4` | `stores/notifications.ts` | S |
| F3 | **P1** | **URL SignalR hub bỏ qua `config.json`** | Hub dựng từ `import.meta.env` nên deploy bằng runtime config sẽ nối nhầm host. Nếu `apiBaseUrl` có `/` cuối thì sinh ra `//hubs/...`, một URL protocol-relative tới host tên `hubs`. | `913579d7`, `e9aba60c` | `stores/notifications.ts:66`, `lib/runtime-config.ts` | S |
| F4 | **P1** | **Mọi `VITE_*` đọc qua runtime `config.json`** | Base chỉ override được `apiBaseUrl`. Google/Microsoft client id và Firebase bị nướng vào bundle, nên **image frontend không bật được OAuth/push** khi deploy bằng compose. Có 13 chỗ đọc thẳng `import.meta.env`. | `e9aba60c`, `bbaef2ef` | `lib/runtime-config.ts`, `main.ts`, `api/client.ts`, `use-push-notifications.ts`, `*LoginButton.vue`, `public/config.example.json`, `Dockerfile`, compose prod | M |
| F5 | P2 | Query param mảng: khóa lặp lại thay vì `key[]` | Axios gửi `a[]=1`, ASP.NET không bind được, nên lọc bị **bỏ qua trong im lặng**. Thêm `paramsSerializer: { indexes: null }`. Hiện tiềm ẩn. | `2cd31596` | `api/client.ts` | S |
| F6 | P2 | Cả trang over-scroll ra ngoài app shell | Thêm `relative` cho shell và `<main>`. | `b7c9e98b` | `layouts/AppLayout.vue` | S |
| F7 | P2 | Cuộn về đầu khi chuyển màn | Nội dung cuộn trong `<main>` chứ không phải `window`, nên `scrollBehavior` không có tác dụng. Dùng `watch(route.path)` rồi `scrollTo(0)`. | `4d1dafd1` | `AppLayout.vue`, `router/index.ts` | S |
| F8 | P2 | **Cổng phân quyền cho sidebar và route** | Sidebar không lọc theo quyền; guard chỉ biết `requiresAuth`/`guestOnly`. Thêm `meta.sidebar.permission`, `meta.permission`, `requiresSystemAdmin`, `resolveLandingRoute()`. | `2c8a109f`, `9074e748`, `dc43ec25`, `487fba12` | `composables/use-sidebar-menu.ts`, `router/index.ts`, `use-home-route.ts` | M |
| F9 | P2 | Primitive responsive dùng chung | Ô nhập 16px trên iOS (chống auto-zoom), gutter cho modal, pagination wrap, `100dvh` thay `h-screen`, `lib/responsive.ts`, `use-media-query`. | `60edae90`, `07d28208` | `assets/styles/main.scss`, `AppLayout.vue` | S |
| F10 | P2 | Các component base vừa màn điện thoại | NotificationBell cố định 360px, OrganizationSwitcher mở `right-start` nên tràn drawer, hàng thành viên không wrap, vùng chạm khoảng 40px. | `bdac60f8` (một phần) | `NotificationBell`, `OrganizationSwitcher`, `OrganizationMembersDialog`, `OrganizationsView`, `AppControls` | M |
| F11 | P3 | `scrollbar-gutter: stable` trên `<main>` | | `df967061` | `AppLayout.vue` | S |
| F12 | P3 | Breadcrumb một dòng, cắt tên dài | | `f55fcace` | `layouts/AppHeader.vue` | S |
| F13 | P3 | Giới hạn chiều cao danh sách phiên đăng nhập | | `16b7bc20` | `ProfileDialog.vue` | S |
| F14 | P3 | `cursor: pointer` cho button (Tailwind v4 preflight) | | `3067f568` | `assets/styles/tailwind.css` | S |
| F15 | P3 | `AppSelect` (chọn lại thì bỏ chọn) + lọc không dấu (fuse.js) | | `b5dd9950` | `vite.config.ts` + file mới | S–M |
| F16 | P3 | *(Tùy chọn)* `--app-zoom` cho laptop màn chật | Là lựa chọn thiết kế; nếu đưa vào base thì phải kèm fix pointer của ECharts. | `f0927d59` | `main.scss` | M |
| F17 | P3 | Kho component để dành khi base có bảng hoặc biểu đồ | `ExportButton`, `file-download.ts`, `TableSkeleton`, `ColumnVisibilityMenu`, `use-row-activate`; fix ECharts pointer dưới CSS zoom và tooltip trễ. Đưa vào base dưới dạng "recipes", không bắt buộc. | nhiều | – | M |

---

## Hạ tầng test

| # | Ưu tiên | Mục | Ghi chú | SHA CIMP | File base | Công sức |
|---|---|---|---|---|---|---|
| T1 | **P1** | Nâng Testcontainers 4.13 → 4.15 | SSH.NET có 2 advisory mức high: GHSA-q939-rpr3-3284, GHSA-mggc-4xg6-vcxf. Chỉ là phụ thuộc của test. | `e6fc8a8e` | `tests/*.{Infrastructure,API}.Tests.csproj` | S |
| T2 | **P1** | Respawn loại schema `hangfire` | Không loại thì mỗi host phụ (`WithWebHostBuilder`) cài lại schema và **API.Tests treo vô hạn**. Lỗi đang tiềm ẩn. | `04aaded4` | `API.Tests/TestSupport/ApiFactoryFixture.cs` | S |
| T3 | **P1** | Ghim `CacheSettings__Provider=Memory` (và `RealtimeSettings__Backplane=None`) cho host test | Không ghim thì test ghi cache vào Redis của máy dev và lần sau đọc lại dữ liệu cũ. | `6ec95340` | `ApiFactoryFixture.cs` | S |
| T4 | **P1** | `enableAutoUnmount(afterEach)` toàn cục | Interval vẫn chạy sau teardown; ở CIMP từng làm CI đỏ. | `c30ab0df` | `frontend/tests/setup.ts` | S |
| T5 | **P1** | Test nút login: `vi.stubEnv('VITE_*_CLIENT_ID','')` | Máy dev có `.env` thật nên test này đỏ thường trực. | `bd85cbb0` | `tests/components/{Google,Microsoft}LoginButton.test.ts` | S |
| T6 | **P1** | `tsconfig.e2e.json` thêm `"node"` vào `types`; `type-check` phải phủ cả tsconfig vitest và e2e | Thiếu `node` sinh khoảng 100 lỗi type ở e2e. Script `type-check` hiện không kiểm code test. | `1ceb02ff` | `frontend/tsconfig.e2e.json`, `package.json` | S |
| T7 | P2 | `FastPasswordHasher` (BCrypt cost 4) cho API.Tests | Cost 12 tốn khoảng 0,3s mỗi lần hash, chiếm khoảng ⅓ thời gian API.Tests của CIMP. Có kèm test ghim production vẫn cost 12. | `2c8435b5`, `6038eca1` | `ApiFactoryFixture.cs`, `PasswordHasher.cs` | S |
| T8 | P2 | Respawn dùng `DELETE` thay `TRUNCATE` | Nhanh hơn khoảng 60ms mỗi lần reset. | `2c8435b5` | `ApiFactoryFixture.cs` | S |
| T9 | P2 | `setup.ts` thôi import naive-ui (tách seam `confirmation-override`) | Suite FE của CIMP giảm 18% (92s → 75s). | `7ec0b96d` | `tests/setup.ts:4`, `src/lib/feedback-naive.ts` | S |
| T10 | P2 | Một container Postgres, mỗi collection clone DB từ template đã migrate | Infra tests của CIMP: khoảng 80s → 60s. Template phải migrate với `Pooling=false`; dọn dẹp nhờ Ryuk. Base hiện còn nhỏ, chủ yếu để fork dùng sau. | `972b60e4`, `b0d4f795` | `Infrastructure.Tests/TestSupport/PostgresContainerFixture.cs` | M |
| T11 | P2 | `storeIn(pinia, useStore)` + `renderComponent` trả về `pinia` | Pinia testing bỏ qua tham số và có thể trả store của test trước. | `c30ab0df`, `79021356` | `tests/helpers/{pinia,render}.ts` | S |
| T12 | P2 | Stub view lazy trong test router | Nạp chunk thật mất khoảng 2s, có lúc vượt timeout. | `303b681b` | `tests/router/guard-integration.test.ts` | S |
| T13 | P3 | Quy ước "host test không gọi ra ngoài, không phụ thuộc config local" | Stub ở mép HTTP, `BaseUrl` trỏ host `.invalid`, ghim mọi tham số bằng env. | `885f2330`, `caf6b334` | `rules/commands.md` | S |
| T14 | P3 | `test.sh` mặc định lọc bỏ `Category=RealData` | | `2c8435b5` | `backend/scripts/test.sh` | S |
| T15 | P3 | Helper `WithServices` cho host phụ | Tránh lỗi "service provider thứ 21" của EF. | `2658ed62` | | S |
| T16 | P3 | `SentEmailCollector` thay no-op | Để assert được nội dung email. | `d38e9036` | | S |
| T17 | P3 | Assert theo khóa locale thay vì câu chữ | Base có khoảng 32 assert dùng literal. | `30e029f7` | | M |
| T18 | P3 | Gộp test chỉ khác dữ liệu thành `it.each` / `[Theory]` (giữ tên ca) | | `07bac283`, `ddc6ee90` | | S |
| T19 | P3 | Factory `makeOrganization` / `makeRole` dùng chung | | `0fa37da3` | | S |
| T20 | P3 | Fixture thời gian dựng từ instant UTC (CI chạy UTC) | | `4ce50f93` | | S |
| T21 | P3 | MSW trả phản hồi trung tính cho các endpoint gọi ngầm | | `a5a4409a` | | S |

---

## DevOps / Docker / CI / config

| # | Ưu tiên | Mục | Ghi chú | SHA CIMP | File base | Công sức |
|---|---|---|---|---|---|---|
| D1 | **P0** | *(xem 0.1)* `.dockerignore` | | | | |
| D2 | **P1** | **`RENAMING.md` còn lỗ hổng** (nguyên nhân của `d8e401d1`) | Lệnh `grep --include=*.*` bỏ sót `backend/Dockerfile`. Thiếu các dạng `starterkit` (package, volume, `POSTGRES_USER`, Prometheus, Grafana) và `starterKit` (orval). Thiếu cảnh báo: đổi tên volume sẽ tạo volume rỗng mới; đổi tên job Prometheus làm đứt chuỗi metric. Thiếu bước kiểm cuối `git grep -nIi 'starter.\?kit'` phải trả rỗng. Bước 7 vẫn nhắc gitnexus đã lỗi thời. **Đề xuất:** `scripts/rename.sh` + fork checklist. | `d8e401d1`, `283e62b2` | `RENAMING.md` | M |
| D3 | **P1** | **`bun.lock` bị gitignore nhưng Dockerfile và CI chạy `--frozen-lockfile`** | Lockfile không được track (CIMP cũng vậy, chỉ track `frontend/user-guide/site/bun.lock`), nên `--frozen-lockfile` không ghim được gì: CI resolve lại dependency mỗi lần, còn build local thì chép `bun.lock` chưa track của máy dev. Hai môi trường có thể ra hai tập phiên bản khác nhau. Cần quyết: track lockfile ngay trong base. | – (cả hai repo vẫn lỗi) | `.gitignore:12` | S |
| D4 | **P1** | *(xem F4)* Image frontend không bật được OAuth/Firebase | | | | |
| D5 | P2 | nginx: `gzip` (+`gzip_proxied any`), cache immutable cho `/assets/`, `no-cache` cho `index.html`, `absolute_redirect off` | | `df8187d6`, `dbbfb04a` | `frontend/nginx.conf` | S |
| D6 | P2 | Footer image frontend hiện "unknown" | Dockerfile frontend không có git. | `344fab45` | `frontend/Dockerfile` | S |
| D7 | P2 | **CI không phụ thuộc nhà cung cấp; build image prod từ pipeline của tag** | Build từ `main` thì MinVer ra `X.Y.(Z+1)-preview`. Tag `v*` phải được bảo vệ; dùng `fetch-depth: 0`. Giữ GitHub làm mặc định, thêm mẫu GitLab. Fork checklist khi đổi host gồm: `prCli`, `ciTriggerNote`, branch-guard của lefthook, CONTRIBUTING. | `c54b4bb0`, `87e36b19` | `.github/workflows/release.yml`, `.claude/release-kit.json`, `lefthook.yml` | M |
| D8 | P2 | Compose staging (tùy chọn) | Subnet và cổng riêng, Mailpit, `-p`, `.env.staging.example`. | `db84c525` | mới | M |
| D9 | P2 | README phần production | Giữ secret ngoài checkout; để trống `VITE_API_BASE_URL` khi chạy same-origin; chạy HTTP thì đặt `Secure:false`, `SameSite:Lax`. | `436876bf` | `README.md` | S |
| D10 | P2 | `.gitignore` bổ sung | `backend/src/*.API/uploads/*`, `appsettings.Staging.json`, `.worktrees/`. | `0a8b9b4e` | `.gitignore` | S |
| D11 | P3 | Healthcheck cho `api` và `web` trong compose prod | Base đã có `/api/health`. | – | `docker-compose.prod.yml` | S |
| D12 | P3 | Ghim tag cho các image `:latest` | | – | `docker-compose.yml` | S |
| D13 | P3 | Deploy qua SSH: nếu đưa mẫu GitLab về base thì dùng SSH key + `known_hosts` | Không chép `sshpass` + `StrictHostKeyChecking=no` của CIMP. | – | | S |
| D14 | P3 | Gotenberg sidecar cô lập mạng | Chỉ đi kèm nếu backport B19. | | | – |

---

## Tooling agent và tài liệu (`.claude/`, `CLAUDE.md`, `.serena/`)

| # | Ưu tiên | Mục | Ghi chú | Nguồn | File base | Công sức |
|---|---|---|---|---|---|---|
| A1 | **P1** | **`.serena/project.yml` không vào repo; `serena.md` mô tả cấu hình không tồn tại** | `.gitignore` chặn cả `.serena/`. Base chỉ khai `languages: [csharp]` nên không có LSP TS/Vue. Rule ghi `ignore_all_files_in_gitignore: false`, nhưng thực tế là `true`. **Sửa:** dùng `.serena/*` + `!.serena/project.yml`; `languages: [csharp, vue, typescript]`; thử `find_declaration` với `auto-imports.d.ts` rồi chọn một trong hai: chỉnh config cho khớp rule, hoặc viết lại rule. | `CIMP-VBI/.serena/project.yml` | `.gitignore:16`, `.serena/project.yml`, `.claude/rules/serena.md` | S–M |
| A2 | **P1** | `CLAUDE.md` ghi "Codegen isn't wired up yet" | Câu này đã lỗi thời: orval và `postinstall` đã có sẵn. | `ec156e6c` | `CLAUDE.md` | S |
| A3 | **P1** | Đường dẫn trong `localization.md` sai | Đường dẫn đúng: `Application/Resources/Messages*.resx`, `Domain/Exceptions/DomainMessages.cs`. | `fa6d3404` | `.claude/rules/localization.md` | S |
| A4 | P2 | Export OpenAPI có thể **cần DB** | CIMP gặp: nếu thiếu `ASPNETCORE_ENVIRONMENT=Development`, host chạy nhánh `MigrateAsync` và nối tới `Host=postgres`, sinh hàng loạt lỗi trông như lỗi hợp đồng. Base có cùng nhánh này (`Program.cs:154-159`), nên khả năng cao cũng gặp, nhưng **chưa chạy thử trên base**. Lỗi DNS EAGAIN với `localhost` là do cấu hình máy, chỉ nên ghi thành mẹo troubleshooting. | `f8cb6a4e`, memory | `rules/commands.md`, `rules/api-contract.md`, `README.md` | S |
| A5 | **P1** | Hook và bước Verify trong `CLAUDE.md` vẫn gọi `dotnet test` chạy tuần tự | Nên đổi sang `backend/scripts/test.sh`. | `ec156e6c` | `.claude/hooks/pre-task-reminder.sh`, `CLAUDE.md` | S |
| A6 | **P1** | `.gitignore` thiếu các file do agent sinh ra | `.claude/worktrees/`, `.claude/scheduled_tasks.lock`, `.claude/settings.local.json`, `.worktrees/`, `__pycache__/`. | | `.gitignore` | S |
| A7 | **P1** | Mục **Superpowers Integration** trong CLAUDE.md | Superpowers nhường CLAUDE.md. Spec và plan nằm trong `docs/superpowers/`. Khoảng trống nghiệp vụ thì dừng lại hỏi. Không dùng worktree. Mỗi task commit cục bộ trên `feature/*` và stage theo đường dẫn. Các thao tác có hậu quả ra ngoài vẫn cần user. | `ec156e6c` | `CLAUDE.md`, `docs/superpowers/{specs,plans}/` | S |
| A8 | **P1** | Quy tắc an toàn commit | Câu hỏi không phải lệnh commit. Chạy `git branch --show-current` trước mỗi commit. Subagent chạy song song thì commit theo pathspec. | memory | `CLAUDE.md` | S |
| A9 | P2 | Mẫu **PO Decision Log** tách khỏi decision kỹ thuật | Viết bằng ngôn ngữ PO đọc được; không cần cổng xác nhận. | `6d5c84bc` | `CLAUDE.md` | S |
| A10 | P2 | Rule `task-tracking.md` dạng template | Dùng placeholder cho project id và tracker. | `f4e5daf8`, `337883db` | mới | S–M |
| A11 | P2 | Khôi phục `product-workflow.md` và skill `product-module-start` dạng template | Nội dung: ID spine, chia item cha/con ≤5 điểm, đối chiếu AC, chọn cycle theo ngày. Lấy từ `git show 344fab45^:...`. | `f93810f2`, `ec5a1149` | mới | M |
| A12 | P2 | Mapper theo feature + gỡ skill `crud-entity` | **Chỉ làm cùng B7.** Nếu gỡ riêng lẻ thì skill vẫn đúng với `EntityMapper` của base. | `6e5ff1e2`, `e79d0b9e` | `rules/code-conventions.md`, `skills/crud-entity` | M |
| A13 | P2 | Bài học về worktree | Worktree thiếu các file gitignored (appsettings, generated client, `.env`); phải xoá sau khi dùng. `isolation:"worktree"` tách từ `origin/main` nên không thấy commit chưa push. | memory | `CLAUDE.md` | S |
| A14 | P2 | Ghi các kỹ thuật test vào `rules/commands.md` | Gồm T4, T7, T9, T10, T11, T12. | | `rules/commands.md` | S |
| A15 | P2 | README prod và fork checklist | Gộp với D2 và D9. | | | – |
| A16 | P3 | Rule theo đường dẫn (frontmatter `paths:`) để tiết kiệm context | | `47e33924` | `CLAUDE.md` (Rule Index) | S |
| A17 | P3 | Agent `po` dạng template (chỉ đọc) | | `1f2b3221` | `.claude/agents/po.md` | S |
| A18 | P3 | Rule kiểm thử chức năng ISTQB + `sync_sheets.py` dạng template | | `47e33924`… | | M |
| A19 | P3 | Skill `changelog-xlsx` | Có thể hợp với plugin release-kit hơn. | `6a87a147` | | S–M |
| A20 | P3 | Bộ công cụ hướng dẫn sử dụng (Playwright capture, docx builder, VitePress) | Cần tham số hóa thương hiệu. | `frontend/user-guide/` | | L |
| A21 | P3 | Phụ lục Plane | REST fallback khi MCP timeout; `project_estimate` trả 404. | memory | `rules/task-tracking.md` | S |

---

## Không đưa về base (nghiệp vụ CIMP — backport sẽ là thoái lui)

- **Có thể chuyển thành cờ cấu hình:**
  - Gỡ external login (Google/Microsoft).
  - Bỏ luồng tự đăng ký (RegisterView).
  - Ghi nhớ username.
- **Chỉ CIMP cần:**
  - Mật khẩu tối thiểu 6 ký tự (BR-24).
  - Cửa sổ phiên 7 ngày.
  - Đổi mật khẩu chỉ thu hồi phiên hiện tại (AC-36).
  - Regex số điện thoại VN.
- **Thiết kế riêng của CIMP:**
  - Branch, data scope, admin account, `DataScopeMiddleware`, asset masking.
  - Sidebar, header, breadcrumb, ProfileView thiết kế lại theo CIMP; bỏ HomeView.
- **Nghiệp vụ hoặc hạ tầng riêng:**
  - Toàn bộ hazard, alerts, lookup, verification, assets, radar, lightning, terrain, dashboard.
  - PostGIS, NIMBUS, WeatherPlus.
- **Cấu hình kém an toàn hơn base:**
  - `POSTGRES_PASSWORD=CimpVbi_pass`.
  - Mở cổng Postgres 5432 ra host.
  - Deploy bằng `sshpass` + `StrictHostKeyChecking=no`.

---

## Đánh giá lại (2026-10-15)

Đã đối chiếu khoảng 20 nhận định với base @ `79a1cf1`, tất cả đều đúng: 0.1, B1, B3, B4, B10, F2–F5, T1, T2, T4, T6, A1, A2, A3, A5.

**Bổ sung so với báo cáo gốc:**

- **B1(a) cũng xảy ra ở `MemoryCacheService`**, tức provider mặc định. Ở cả hai provider, `SetAsync` tính lại physical key *sau* factory, nên giá trị cũ bị ghi dưới generation mới.
- **A1 nặng hơn mô tả.** `serena.md` và các dòng frontend trong bảng Tool Selection của `CLAUDE.md` đang mô tả LSP TS/Vue và `ignore_all_files_in_gitignore: false`. Thực tế `project.yml` chỉ có `csharp`, giá trị là `true` và `ignored_paths: []`.
  - ⚠️ Đổi sang `false` mà không chép đủ `ignored_paths` thì Serena đọc được `appsettings.json` chứa secret thật.
- **P0 còn thiếu ba việc:**
  - `frontend/Dockerfile` chép nguyên `frontend/`, nên `.env*` lọt vào build context. Mức độ thấp vì `VITE_*` vốn công khai; sửa cùng lượt. Các pattern `*.md` và `tests/` cũng neo gốc nhưng vô hại.
  - Nếu image build từ máy dev từng được push hoặc chia sẻ thì phải **đổi mật khẩu DB và JWT secret**.
  - Sửa luôn ở CIMP.
  - Coi là xong khi `docker run --rm --entrypoint ls <img> /app` không còn `appsettings.json`.
- **Ước lượng hotfix gốc (½–1 ngày) không khả thi** vì đã có B1 (M), B3 (S–M) và A1 (S–M).
- **F8 bị xếp hai lần**, ở cả v1.6.0 và v1.7.0. Nay chỉ giữ ở v1.7.0.
- **Hạ ưu tiên:** A6, A7, A8 xuống P2, vì đó là quy trình hoặc vệ sinh repo, không phải lỗi. T2 hiện chỉ tiềm ẩn. T5 chỉ đỏ trên máy có `.env` thật.
  - T1: xác minh bằng `dotnet list backend/StarterKit.sln package --vulnerable --include-transitive`.
- **Cách port:** CIMP-VBI có sẵn trên máy, các SHA đều tra được, nhưng nhiều commit gộp lẫn phần riêng của CIMP (ví dụ `7d9818d7` chứa cả B2 và B13). Phải port bằng tay, không cherry-pick.
  - Các mục ghi "cả hai repo vẫn còn" là việc thiết kế mới, phải ước lượng cao hơn chi phí port: `InvalidateMemberAsync`, reload khi `switchOrganization`, `.dockerignore`, `bun.lock`.

## Lộ trình

### Bước 0 — Quyết định (mỗi mục cần một entry `decisions.md`, có cổng xác nhận)

| # | Quyết định | Hướng |
|---|---|---|
| D3 | `bun.lock`. Comment ở `.gitignore:7-8` cho thấy việc không track là cố ý. | (a) Track lockfile, khuyến nghị. (b) Giữ như cũ, bỏ `--frozen-lockfile` khỏi Dockerfile và CI. |
| B3 | Đổi tư thế bảo mật cookie | Thêm `AuthCookieSettings`, mặc định vẫn `Secure` + `None`, khởi động thì chặn tổ hợp nguy hiểm |
| B1 b/c | Thay cơ chế generation counter (2026-08-25) | `SET NX` + circuit breaker, hoặc chỉ try/catch rồi fallback sang factory |
| F4 | Mở rộng thiết kế same-origin (2026-08-07) | Mọi `VITE_*` đọc qua `config.json` |
| A1 | Serena | (a) Bật TS/Vue, track `project.yml`, giữ `ignore_all_files_in_gitignore: true`, sửa rule cho khớp. Khuyến nghị. (b) Viết lại rule theo cấu hình chỉ có csharp. |

**Đã chốt (2026-10-15):**
- **A1:** (a) bật TS/Vue, track `.serena/project.yml`, giữ `ignore_all_files_in_gitignore: true`.
- **D3:** (a) track `bun.lock`.
- **B3:** `AuthCookieSettings`, mặc định vẫn an toàn.
- **B1 b/c:** try/catch rồi fallback sang factory, giữ generation `INCR` và thêm TTL cho generation key. Không làm circuit breaker, không bắt buộc `expiration`.
- **F4:** mọi `VITE_*` đọc qua runtime `config.json`. Biến build-time vẫn dùng làm fallback khi chạy dev (chốt ngày 2026-10-16).

Mỗi mục sẽ có entry `decisions.md` riêng, chỉ ghi sau khi user xác nhận bản nháp, và ghi lúc triển khai.

### v1.5.3 Patch (khoảng 1–1,5 ngày, chế độ feature)

Tiêu chí: công sức S, không đổi interface hay hợp đồng API, không lật quyết định.

- [x] **D3**: track `bun.lock`.
- [x] **0.1**: sửa toàn bộ pattern sang dạng `**/…` (`appsettings*.json` trừ `*.Example.json`, `.env*` trừ `*.example`, `coverage/`). Kiểm bằng `docker build` rồi `ls /app`.
- [x] **B1(a)**: tính physical key một lần, trước factory, cho cả Memory lẫn Redis. Interface giữ nguyên.
- [x] **B4**: thêm `ThenByDescending(Id)`. Sửa luôn `AuditLogRepository` (sắp theo `Timestamp`), đây mới là chỗ B4 thật sự gây lỗi.
- [x] **B10**: bổ sung key còn thiếu vào `appsettings.Example.json`.
- [x] **D10 + A6**: bổ sung `.gitignore`.
- [x] **F2**: thêm cờ `isActive`, poll không tự sống lại sau khi rời app shell.
- [x] **F3**: URL hub lấy qua runtime config và cắt `/` ở cuối.
- [x] **T1** (sau khi xác minh advisory), **T2, T3, T4, T6**.
- [x] **A1, A2, A3, A5**. Sau khi restart Serena, cần kiểm lại `find_declaration` trên `.vue`.
- [x] **S1 (mới, phát hiện ngày 2026-10-16; đã ghim Pkcs/Xml 10.0.13 và MessagePack 2.5.303 trong Infrastructure, scan sạch):** dependency bắc cầu có lỗ hổng nằm trong **code production**, không chỉ trong test.
  - `System.Security.Cryptography.Pkcs` 10.0.0 do MimeKit 4.17 kéo vào. Có advisory High GHSA-846f-f764-76mx và Moderate GHSA-2fmv-rv2c-59x7.
  - `MessagePack` 2.5.302 do `SignalR.StackExchangeRedis` kéo vào. Có advisory Moderate GHSA-qhrr-8q5h-9q3h.
  - Các project test còn dính thêm `System.Security.Cryptography.Xml` 10.0.10 (High).
  - **Cách sửa:** ghim version đã vá bằng `PackageReference` trực tiếp, hoặc nâng MailKit/MimeKit. Kiểm lại bằng `dotnet list backend/StarterKit.sln package --vulnerable --include-transitive`.
- [ ] Ngoài base: sửa `.dockerignore` ở CIMP; đổi secret nếu cần.

### v1.6.0 Hardening (khoảng 5–7 ngày)

1. [x] **B3** cookie + **D9** README chạy HTTP. `ParseSameSite` chặn thêm giá trị dạng số mà `Enum.TryParse` vẫn nhận, CIMP chưa chặn trường hợp này.
2. [x] Phần còn lại của **B1**: (b), (c), `InvalidateMemberAsync`.
   - Kết quả: `RemoveAsync` dùng generation theo từng key, sửa được race cho mọi nơi gọi.
   - Redis lỗi thì ghi log rồi chạy thẳng factory; không làm circuit breaker.
   - (b): ban đầu **không** thêm TTL cho generation key, vì counter bị mất sẽ đếm lại từ 0 và làm sống lại dữ liệu cũ. Sau review (phát hiện #3: mỗi `RemoveAsync` để lại một counter vĩnh viễn), user chốt **thêm TTL** (2026-10-16):
     - TTL của counter = `CacheSettings:MaxExpirationMinutes` (mặc định 1440) + 1 phút, đặt sau mỗi INCR. Mỗi lần ghi entry đều gia hạn **cả hai** counter, kể cả counter đọc ra là 0.
     - TTL entry vượt Max thì bị kẹp (clamp), không ném lỗi. Khởi động sẽ fail nếu không thoả 0 < Default ≤ Max.
     - Nếu counter đọc ra > 0 mà tới lúc ghi đã biến mất, bỏ qua lần ghi.
     - Redis **bắt buộc** `maxmemory-policy noeviction`; `volatile-*` không còn an toàn.
   - Phát hiện #2 (invalidate nuốt lỗi Redis): giữ nguyên theo B1, user đồng ý.
   - Code review ngày 2026-10-16 xác nhận: `RemoveAsync` chạy trong lúc factory đang tính vẫn bị ghi đè bằng giá trị cũ, vì nó xoá key rồi factory ghi lại vào đúng key đó. Lỗi này ảnh hưởng PermissionResolver, TenantAccessService và SystemSettings.
   - Cách sửa: dùng version theo từng key, hoặc chuyển các chỗ invalidate một phần tử sang cơ chế generation. Kèm test cho trường hợp `RemoveAsync`.
3. [x] **F1** reload khi phiên kết thúc, kèm reload khi đổi tổ chức. Đổi mật khẩu không cần reload, vì base không thu hồi phiên khi đổi mật khẩu. Đã phát hiện và sửa race: logout có thể thua watcher phiên hết hạn, dẫn tới redirect vào trang của tài khoản trước.
4. [x] **F4 + D4 + D6**: runtime config cho toàn bộ frontend. Container sinh `config.json` từ biến môi trường; nginx trả 404 và `no-store` cho file này; footer hiện version git.
5. [x] **B2 + B6 + B13**: một migration chuẩn hóa email và username, dừng khi gặp dòng trùng (user đã duyệt SQL ngày 2026-10-16). Chỉ trim + lower, không cấm khoảng trắng bên trong username. `Status` chỉ đổi qua `Deactivate()`/`Activate()`.
6. [x] **B5**, **F5–F7**, **F9**. Request bị client huỷ: log Debug, trả 499. Thêm `relative` + `h-dvh` cho layout. Chỉ cuộn `main` về đầu khi `path` đổi. CSS chống iOS tự zoom, chừa lề cho modal, cho phân trang xuống dòng. Chưa port `MobileFilterCollapse`/`responsive.ts`, để dành cho F10.
7. [x] **T5** (đã xong, làm cùng W4), **T7–T9**, **T11–T12** + **A14**.
   - API.Tests giảm 21s → 12s; suite FE giảm 12,2s → 9,8s.
   - **T10 hoãn, chuyển vào Backlog**: Infrastructure.Tests của base chỉ có 6 class dùng Postgres, chạy 7s và phần lớn là thời gian khởi động container. Template DB chỉ có lợi khi đã tách thành nhiều collection chạy song song. Nên làm khi một fork có Infra tests vượt khoảng 30s.
   - `type-check` kiểm `src` hai lần là **cố ý**, giữ nguyên: pass vitest có thêm type `node`/`vitest/globals` nên che mất lỗi; chỉ pass app mới bắt được code app dựa vào các type đó. Hai pass tốn khoảng 13s.
8. [x] **D3** (đã làm ở Bước 0), **D5**. Đã build image và kiểm bằng curl: `/` và route fallback trả `no-cache`; `/assets/*` trả immutable và gzip, kể cả khi request có header `Via`; asset không tồn tại trả 404; redirect thư mục dùng Location tương đối.
9. [x] **A4** (chạy thử trước), **A13**.
   - Chạy thử cho thấy base **nặng hơn CIMP**: kể cả khi có `Development`, export vẫn fail khi không có DB, vì `UseBackgroundJobs` đăng ký recurring job Hangfire. CIMP chỉ sửa tài liệu thành "cần DB".
   - Base sửa bằng code: thêm cờ `isOpenApiExport` (entry assembly là `GetDocument.Insider`) để bỏ qua migrate, Hangfire và bucket. Câu "no DB required" giờ đúng thật.
   - Kèm theo: phát hiện `openapi.json` đã trôi từ `ee452ba` (Files chuyển sang scope theo tổ chức); đã regen. Frontend không dùng các field bị bỏ.

### v1.7.0 Architecture & DX (khoảng 1,5–2 tuần)

- [x] **B7 + A12**: ArchitectureTests và mapper theo feature.
  - Base chỉ có đúng một vòng (`Common.Interfaces`/`Common.Mappings` ↔ `Accounts`/`Notifications`), nên đi thẳng tới test cấm vòng tuyệt đối như bản cuối của CIMP, bỏ qua giai đoạn ratchet.
  - `EntityMapper` tách thành `AccountMapper` + `NotificationMapper`; `IRealtimeNotifier` dời về `Services.Notifications`; `AuthorizationPolicies` dời sang `Application.Common.Authorization`, controller hết import Infrastructure.
  - Gỡ skill `crud-entity`. `openapi.json` không đổi.
- [x] **B8**: khóa tài khoản tạm thời. Migration `20261016100000_AddAccountLoginLockout` (3 cột).
  - User chốt (2026-10-16): giữ lượt **trước** BCrypt bằng một `UPDATE … RETURNING`, nên đoán song song không vượt được 5 lần. Login ngoài được cho qua và xoá bộ đếm. Tài khoản deactivate vẫn trả lỗi chung.
  - Khác CIMP: CIMP load account, kiểm BCrypt rồi `Update` cả dòng, nên có thể ghi đè một lần đổi mật khẩu/deactivate xảy ra giữa chừng. Base chỉ ghi đúng 3 cột lockout, và EF không bao giờ ghi các cột này khi update thường.
  - Báo khoá **trước** khi kiểm mật khẩu: nếu chỉ báo khi mật khẩu đúng thì khoá thành máy dò mật khẩu. Việc này không lộ thêm gì, vì `/register` đã cho biết username tồn tại.
- [x] **B9**: thông báo; đổi hợp đồng API, phải regen orval. Migration `20261016110000_AddNotificationDedupeKey`.
  - User chốt (2026-10-16): `readState=All|Unread|Read` thay `unreadOnly`, bỏ `search` (**breaking** cho client ngoài, ghi release note); `NotifyAsync` đi chung đường khử trùng với `NotifyManyAsync`.
  - `NotifyManyAsync`: một lần lưu, job dispatch chia lô 500 id. `DispatchAsync(Guid)` giữ lại cho job cũ còn trong hàng đợi.
  - Khác CIMP: lọc khoá đã gửi theo cả account, kiểm độ dài `DedupeKey` ở domain; race giữa hai lần chạy đồng thời để index unique chặn, caller retry.
- [x] **F8** cổng phân quyền: một trường `meta.permission` vừa ẩn mục sidebar vừa chặn route (chuyển về `home`). Không port `requiresSystemAdmin` (base không có admin toàn cục) và `resolveLandingRoute` (base vẫn có trang chủ). Base chưa có route nào dùng; đây là cơ chế cho fork.
- [x] **F10** mobile. Kiểm bằng Playwright ở 375px (API giả lập) và 1280px: trang không cuộn ngang, panel thông báo vừa màn hình, menu tổ chức mở lên trên trong drawer, nút điều khiển 40px trên màn chạm.
  - Phát hiện thêm và sửa: footer drawer của Naive là flex hàng ngang canh phải, nên switcher tổ chức trên mobile bị đẩy hẳn ra ngoài mép trái (x = −287). Lỗi có từ trước.
  - Trên điện thoại chuông chuyển sang mép phải (chỉ dưới `sm`), để panel neo theo chuông có đủ bề rộng. Desktop giữ nguyên.
  - Chưa port `MobileFilterCollapse`: base chưa có màn danh sách nào có bộ lọc.
- [x] **D2** `rename.sh` + fork checklist, **D7** CI không phụ thuộc nhà cung cấp + build từ tag.
- [x] **A7–A11**: tùy chọn quy trình.
- [x] **T10**: một container Postgres, mỗi collection clone DB từ template đã migrate. Hoãn từ W7, xem lý do ở mục 7 của v1.6.0.

### Backlog / recipes

Các mục P3 còn lại; module B19–B21, F17, D8, A18–A20 (chỉ khi có dự án thứ hai cần).

**Đã làm (2026-10-16, nhánh `feature/backport-cimp-vbi`):**

- Nhóm 1:
  - B17 `f06de9c`, D12 `9c45281`, D11 `5d0a536`, T14 + T20 `a7059bd`, B22 `b068610`, B11 `e8c8402`, B15 `1b2152b`, F11–F14 `522d0d6`.
  - Thêm ngoài danh sách: fan-out realtime chạy song song `dd8bafa`, dedupe đọc projection `09cbb18`.
  - B13 vốn đã có sẵn trong base: `AccountParams` không chứa `Status`.
- Nhóm 2:
  - B14 `a544040`: `ExecuteInTransactionAsync`.
  - B16 `3a708a1`: `ICliCommand` + lệnh `migrate`. Migration đi qua port `IDatabaseMigrator` để giữ luật Infrastructure-chỉ-trong-`Program.cs`.
  - F15 `12f482e`: `AppSelect` + bộ lọc không dấu theo từ.
  - F15 (bổ sung): fuse.js chỉ là phương án dự phòng khi **không** option nào khớp theo từ, một index cho cả danh sách, từ dưới 4 ký tự phải khớp nguyên văn, ngưỡng 0.35. Bản CIMP (fuse cho mọi phím gõ, mỗi option một index) kéo về gần nửa danh sách với các âm tiết ngắn như `hai`/`hoa`; ngưỡng 0.4 cũng vậy.
  - T13 `f034c8c`: host test ghim toàn bộ `appsettings.Example.json` bằng env, kèm override cách ly và test canh.
  - T16 `8f49b08`: `SentEmailCollector`, test trọn chuỗi register → link trong email → verify.
  - T17 `b4e1297`: 48 assert dùng khoá locale.
  - T18 `c39ed9b` (frontend) và `c8d8458` (backend).
  - T19 `0e1ac68`.
- Không làm:
  - **T15**: base không có interceptor EF. Đã dựng thử 25 host phụ, không có lỗi "service provider thứ 21", nên helper sẽ không có ai gọi.
  - **B18**: không đưa vào base (user chọn, 2026-10-16). Base tìm kiếm phân trang dưới SQL (`ILIKE`), còn CIMP so trong bộ nhớ. Base mới có một ô tìm kiếm phía server là audit log, nơi người ta thường tìm theo username/email. Recipe cho fork cần tìm không dấu ở SQL (đã chạy thử trên Postgres 18):

    ```sql
    -- Trusted extension từ PG13: user sở hữu DB tạo được, không cần superuser. đ/Đ ra d/D.
    CREATE EXTENSION IF NOT EXISTS unaccent;
    -- Index không nhận unaccent() trực tiếp ("must be marked IMMUTABLE"), nên cần hàm bọc.
    CREATE FUNCTION immutable_unaccent(text) RETURNS text
      LANGUAGE sql IMMUTABLE PARALLEL SAFE STRICT
      RETURN public.unaccent('public.unaccent'::regdictionary, $1);
    CREATE INDEX ... ON <bảng> (lower(immutable_unaccent(<cột>)));
    ```

    Viết lệnh này trong migration bằng `migrationBuilder.Sql`, map hàm vào EF bằng `DbFunction`, rồi so `lower(immutable_unaccent(cột)) LIKE lower(immutable_unaccent(@term))`. Lưu ý cách này vẫn so một chuỗi con liền mạch, nên khác quy tắc "mọi từ, thứ tự bất kỳ" của `AppSelect`. Muốn khớp giống hệt frontend thì thêm cột tìm kiếm do app chuẩn hoá lúc lưu.

---

## Workflow triển khai

### 1. Mô hình nhánh

Hiện tại `main` và `dev` trùng nhau (cả hai @ `79a1cf1`, không có commit nào chưa release).

- **Toàn bộ lộ trình nằm trên một nhánh `feature/backport-cimp-vbi` cắt từ `dev`, và chỉ mở một MR vào `dev`.**
  - Mỗi WP là một hoặc vài commit cục bộ, theo conventional commit, stage theo đường dẫn.
  - Chưa push và chưa mở MR cho đến khi user yêu cầu.
  - Phiên bản do user quyết định lúc release. Các nhãn v1.5.3, v1.6.0, v1.7.0 trong file này chỉ dùng để chia đợt và thứ tự ưu tiên.
  - Skill không merge ngược `main` về `dev`; user tự làm sau mỗi release.
- **Repo không có CI chạy cho PR.** CI chỉ chạy sau khi đẩy tag (xem `release-kit.json`). Gate local là chốt chặn duy nhất trước khi merge, nên DoD bên dưới là bắt buộc.

### 2. Chu trình cho mỗi WP

1. **Chuẩn bị**
   - Đọc `decisions.md` và mục "Đã chốt" ở trên. Chạy `git status`.
   - Chạy `git -C ../CIMP-VBI show <sha>` để xem bản sửa gốc. Port bằng tay, đổi `CimpVbi` thành `StarterKit`, không cherry-pick.
2. **Thiết kế:** chỉ làm với WP có ký hiệu ◆. Dùng `superpowers:brainstorming` ra spec ngắn, sau đó `superpowers:writing-plans`. Spec và plan lưu trong `plans/`.
3. **Impact check:** dùng `codegraph_explore` trên mọi symbol sắp sửa. Với C# thì thêm Serena `find_referencing_symbols`.
4. **TDD:** viết test tái hiện lỗi, xác nhận test đỏ, sửa code, rồi xác nhận test xanh.
5. **DoD:** tất cả các bước dưới phải xanh.
   - `backend/scripts/test.sh` (khi WP chạm backend).
   - `bun run --cwd frontend test:run`, `type-check`, `lint` (khi WP chạm frontend).
   - `docker build` cả hai image, rồi `ls /app` (khi WP chạm Dockerfile, `.dockerignore`, nginx hoặc runtime config).
   - `bun run --cwd frontend codegen`, rồi `type-check` (khi WP chạm `shared/openapi/openapi.json`).
6. **Review:** chạy `/code-review medium`, sửa các finding đã xác nhận.
7. **Cổng user:**
   - Duyệt nháp entry `decisions.md`, nếu WP có.
   - Duyệt SQL migration, nếu WP có.
   - Commit cục bộ khi WP đạt DoD. Chỉ mở MR khi user yêu cầu (`release-kit:git-mr`).
8. **Đóng WP:** tick checkbox trong file này.

### 3. Work package và thứ tự

**v1.5.3:** mỗi WP là một nhóm commit trên nhánh chung. Làm H1 → H2 trước, sau đó H3–H5 theo thứ tự bất kỳ, cuối cùng H6.

| WP | Mục | Ghi chú |
|---|---|---|
| H1 build-security | 0.1, D3, D10 + A6 | Có `docker build` trong DoD |
| H2 test-infra | T1, T2, T3, T4, T6 | Phải xong **trước** mọi việc khác. T3 tách test khỏi Redis của máy dev. |
| H3 cache-race | B1(a) | Test tái hiện race với invalidate, chạy cho cả hai provider |
| H4 backend-small | B4, B10 | |
| H5 fe-notifications | F2, F3 | |
| H6 agent-docs | A1, A2, A3, A5 | A5 sửa cả `release-kit.json` (`gate.phase1`, `gate.mandatory`) và hook. A1 kiểm lại bằng `find_declaration` trên `.vue`. |

**v1.6.0:** chạy hai lane song song (backend và frontend). Lane infra/docs cắm vào bất cứ lúc nào.

| Lane | Thứ tự | Lý do xếp như vậy |
|---|---|---|
| BE | W1 cookie ◆ (B3, D9) → W5 identity ◆ (B2, B6, B13) → W2 cache ◆ (B1 b/c, `InvalidateMemberAsync`) | W1 và W5 cùng sửa `AuthService`. W5 có migration. |
| FE | W3 session-exit ◆ (F1 + switch org) → W4 runtime-config ◆ (F4, D4, D6) → W6 polish (B5, F5–F7, F9) → W7 test-speed (T5, T7–T12, A14) | W4 và W6 cùng sửa `api/client.ts`. T5 phụ thuộc cách nút login đọc config sau W4. |
| Infra/docs | W8 (D5), W9 (A4 chạy thử, A13) | Độc lập |

**v1.7.0:**
- **X1 (B7 + A12) làm đầu tiên và làm riêng**, sau khi mọi nhánh của 1.6 đã merge, vì X1 dời namespace.
- Sau X1:
  - Lane BE: X2 lockout ◆ (B8, có migration) → X3 notifications ◆ (B9, đổi OpenAPI).
  - Lane FE: X4 permission gate ◆ (F8) → X5 mobile (F10).
  - Độc lập: X6 fork/CI ◆ (D2, D7), X7 process templates (A7–A11).

### 4. Luật tuần tự hóa

- **Mỗi lúc chỉ một WP có EF migration** (W5, X2). Hai migration song song sẽ xung đột ở `*ModelSnapshot.cs`.
- **Mỗi lúc chỉ một WP sửa `shared/openapi/openapi.json`** (X3; kiểm thêm B5 nếu nó đổi response được khai báo).
- **Mặc định làm tuần tự trong một working tree.** Chỉ chạy song song lane BE và FE khi file không chồng nhau, và worktree phải được tạo tay từ `dev`.
  - Không dùng `isolation: "worktree"`, vì cách đó cắt từ `origin/main`.
  - Setup cho worktree: chép `appsettings.json` và `frontend/.env*`, chạy `bun install` (sinh client orval và typings). Xóa worktree sau khi dùng xong.
- **H2/T3 phải merge trước khi có bất kỳ lần chạy test song song nào.**

### 5. Việc của user, ngoài base

- **Ngay khi H1 xong:** đổi mật khẩu DB và JWT secret nếu image build từ máy dev từng được push hoặc chia sẻ.
- **Sau mỗi release:** đưa ngược sang CIMP-VBI các mục mà CIMP cũng còn lỗi.
  - Ở v1.5.3: `.dockerignore`, `bun.lock`.
  - Ở v1.6.0: `InvalidateMemberAsync`, reload khi `switchOrganization`.
- **Sau mỗi release:** merge `main` về `dev`.
