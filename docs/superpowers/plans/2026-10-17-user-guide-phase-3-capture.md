# HDSD — Giai đoạn 3: Công cụ chụp ảnh — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Lệnh `bun run --cwd frontend guide:capture` chụp ảnh màn hình app thật (cả `vi` và `en`) vào `site/src/{base|app}/images/<locale>/<chương>/`. Ảnh có số chú thích và vùng che dữ liệu riêng tư. Công cụ không bao giờ ghi dữ liệu vào hệ thống đang chụp.

**Architecture:**
- **Runner:** Playwright test runner của frontend, config riêng `user-guide/tools/playwright.capture.config.ts`, mỗi "section" là một file `*.capture.ts`.
- **Logic thuần** nằm trong `tools/lib.ts`, test bằng vitest của frontend:
  - chính sách chặn ghi;
  - đọc `GUIDE_LOCALE`;
  - đường dẫn ảnh;
  - kiểm điều kiện dữ liệu.
- **Fixture `session` phạm vi worker:** một browser context cho cả lần chạy, nạp lại và lưu lại `storageState`, vì backend xoay vòng refresh token.

**Tech Stack:** `@playwright/test` 1.61.1 (đã có), vitest, TypeScript.

**Spec:** `docs/superpowers/specs/2026-10-15-user-guide-design.md` §10, §14 giai đoạn 3.

## Global Constraints

- Nhánh `feature/user-guide-capture` cắt từ `dev`. Không commit `docs/superpowers/**`. Commit theo Conventional Commits, stage theo đường dẫn, kèm hai dòng attribution như các giai đoạn trước.
- Biến môi trường:
  - `GUIDE_BASE_URL` (mặc định `http://localhost:5173`);
  - `GUIDE_API_URL` (mặc định `http://localhost:7000`, giống `VITE_API_BASE_URL` khi dev);
  - `GUIDE_USERNAME`, `GUIDE_PASSWORD`;
  - `GUIDE_LOCALE=vi|en|all` (mặc định `all`).

  Đọc từ `frontend/user-guide/tools/.env` (gitignored) qua `dotenv`, như `.env.e2e`.
- Config:
  - một project;
  - `workers: 1`, `fullyParallel: false`, `retries: 0`;
  - viewport 1440×900, `deviceScaleFactor: 1`;
  - không có `webServer`: app và API phải đang chạy.
- Request tới API phải có `X-TimeZone` (`UserTimeZoneMiddleware`).
- Ảnh: JPEG chất lượng 82, ghi vào `frontend/user-guide/site/src/<tree>/images/<locale>/<chapter>/<name>.jpg`.
- Chú thích: hình tròn violet-600 (`#7c3aed`), số trắng. Selector không thấy phần tử → test đỏ.
- Chặn ghi: mọi request `**/api/**` khác `GET` đều bị huỷ, trừ `POST /api/auth/login` và `POST /api/auth/refresh`. Danh sách request bị huỷ phải rỗng ở teardown.
- `frontend/user-guide/tools/.auth/` và `frontend/user-guide/tools/.env` vào `.gitignore`.

## Review Focus

1. **Một section bấm nhầm nút lưu hay xoá:** request bị huỷ, test đỏ, liệt kê method và URL. Đã thêm test cho `isAllowedApiRequest` (Task 1), và section mẫu của Task 3 khẳng định danh sách rỗng.
2. **Refresh token bị dùng lại giữa hai context:** chỉ một context cho cả lần chạy; `storageState` được lưu lại cả khi test đỏ (teardown của fixture worker).
3. **`GUIDE_LOCALE` gõ sai (`VI`, `fr`):** dừng ngay và báo giá trị hợp lệ, không chụp lặng lẽ một bộ rỗng. Đã thêm test vào Task 1.
4. **Thiếu dữ liệu điều kiện:** dừng trước khi chụp và in đủ mọi điều kiện còn thiếu, không chỉ điều kiện đầu tiên. Đã thêm test vào Task 1.
5. **Tên chương hoặc ảnh có `..` hay `/`:** ảnh không được ghi ra ngoài thư mục `images`. Đã thêm test vào Task 1.

---

### Task 1: Logic thuần (`tools/lib.ts`)

**Files:**
- Create: `frontend/user-guide/tools/lib.ts`
- Create: `frontend/tests/unit/user-guide-tools/lib.test.ts`

**Interfaces — Produces:**
```ts
export type Locale = 'vi' | 'en'
export type Tree = 'base' | 'app'
export function parseLocales(value: string | undefined): Locale[]          // undefined/'' / 'all' → ['vi','en']; 'vi' → ['vi']; else throw Error listing vi|en|all
export function isAllowedApiRequest(method: string, url: string): boolean   // GET/HEAD/OPTIONS → true; POST …/api/auth/login|refresh (path exact, query ignored) → true; else false
export function imagePath(o: { siteSrc: string; tree: Tree; locale: Locale; chapter: string; name: string }): string
    // `${siteSrc}/${tree}/images/${locale}/${chapter}/${name}.jpg`; chapter = 1–2 kebab-case segments joined by '/', name = kebab-case; else throw
export type PreconditionData = {
  organizations: { id: string; myPermissionCodes: string[] }[]
  membersByOrg: Record<string, number>       // org id → member count
  customRolesByOrg: Record<string, number>   // org id → roles with isSystem=false
  unreadNotifications: number
  sessions: number
}
export function missingPreconditions(d: PreconditionData): string[]        // [] when ok; one English line per unmet condition
```
Điều kiện (spec §10):
- có một tổ chức mà tài khoản có quyền `organizations.manage` (chủ tổ chức), và chính tổ chức đó có ≥2 thành viên và ≥1 vai trò tuỳ chỉnh;
- ≥1 thông báo chưa đọc;
- ≥2 phiên đăng nhập.

- [ ] **Step 1: Viết test (đỏ)**, `describe` theo từng hàm:
  - `parseLocales`:
    - `undefined`, `''`, `'all'` → `['vi','en']`;
    - `'en'` → `['en']`;
    - `'VI'` và `'fr'` ném lỗi chứa `vi|en|all`.
  - `isAllowedApiRequest`:
    - `GET http://localhost:7000/api/organizations` → true;
    - `POST …/api/auth/login` và `POST …/api/auth/refresh?x=1` → true;
    - `POST …/api/auth/logout`, `PATCH …/api/notifications/read-all`, `DELETE …/api/auth/sessions/5`, `POST …/api/auth/login/extra`, `POST …/api/auth/switch-organization` → false.
  - `imagePath`:
    - `organizations` + `add-member` → đúng đường dẫn;
    - `organizations/members` → hai cấp;
    - `../x`, `a/b/c`, `Org`, name `a/b`, name `..` → ném lỗi.
  - `missingPreconditions`:
    - dữ liệu đủ → `[]`;
    - dữ liệu rỗng → 4 dòng: chủ tổ chức, thành viên và vai trò của tổ chức đó, thông báo, phiên;
    - chủ tổ chức có 1 thành viên → chỉ dòng thành viên.
- [ ] **Step 2: Chạy** `bun run --cwd frontend test:run tests/unit/user-guide-tools/lib.test.ts` → FAIL (chưa có module).
- [ ] **Step 3: Implement** `lib.ts` (không import Playwright, để vitest nạp được).
- [ ] **Step 4: Chạy lại** → PASS.
- [ ] **Step 5: Commit** `feat(guide): add the capture tool's request, path and precondition rules`.

### Task 2: Config, fixture phiên, fixture chụp

**Files:**
- Create: `frontend/user-guide/tools/playwright.capture.config.ts`
- Create: `frontend/user-guide/tools/fixtures.ts`
- Create: `frontend/user-guide/tools/.env.example`
- Modify: `frontend/package.json` (script `guide:capture`)
- Modify: `frontend/tsconfig.e2e.json` (`include` thêm `user-guide/tools/**/*.ts`)
- Modify: `.gitignore`

**Interfaces:**
- Consumes: `lib.ts` (Task 1); `login()` của `frontend/e2e/fixtures.ts`.
- Produces: `fixtures.ts` export `test`, `expect`, `locales` (= `parseLocales(process.env.GUIDE_LOCALE)`). `test` có:
  - worker fixture `session: BrowserContext`:
    - mở một context duy nhất (nạp `tools/.auth/state.json` nếu có);
    - vào app; nếu sau 10s chưa thấy app shell (`aside`) thì `login()` bằng `GUIDE_USERNAME`/`GUIDE_PASSWORD`;
    - gọi `missingPreconditions` với dữ liệu lấy bằng `GET` qua `context.request` (kèm `X-TimeZone: UTC`); thiếu thì ném lỗi liệt kê đủ;
    - teardown ghi `context.storageState({ path })`.
  - test fixture `page`: trang mới trong `session`. `page.route('**/api/**')` huỷ request bị `isAllowedApiRequest` từ chối và ghi `METHOD URL` vào danh sách; teardown `expect(blocked).toEqual([])`, rồi đóng trang.
  - test fixture `capture`, gồm các hàm:
    - `useLocale(locale)`: đặt `localStorage['app-locale']` rồi `page.reload()` và chờ app shell;
    - `annotate(items: { selector: string; n: number }[])`: chèn overlay số tròn, `expect(locator).toBeVisible()` cho từng selector trước;
    - `blur(selectors: string[])`: `filter: blur(6px)` cho các phần tử; selector không có thì đỏ;
    - `shot(o: { tree?: Tree; locale: Locale; chapter: string; name: string; clip?: string })`: ghi JPEG q82 vào `imagePath(...)`, `clip` là selector của vùng cần chụp; gỡ overlay sau khi chụp.
- `guide:capture` = `playwright test -c user-guide/tools/playwright.capture.config.ts`. Config: `testDir: './sections'`, `testMatch: '**/*.capture.ts'`, `globalSetup` không dùng (điều kiện kiểm trong fixture `session`, xem ledger), `reporter: 'list'`, `use.baseURL = GUIDE_BASE_URL`.

- [ ] **Step 1: Viết config, fixture, `.env.example`, script, tsconfig, gitignore.**
- [ ] **Step 2: Kiểm kiểu** `bun run --cwd frontend type-check` → xanh. `bun run --cwd frontend guide:capture --list` → in ra danh sách rỗng (chưa có section), không lỗi config.
- [ ] **Step 3: Commit** `feat(guide): add the screenshot capture runner and fixtures`.

### Task 3: Section mẫu và chạy thật

**Files:**
- Create: `frontend/user-guide/tools/sections/base/getting-started.capture.ts`
- Modify: `frontend/user-guide/site/src/base/images/{vi,en}/getting-started/overview.webp` → thay bằng `overview.jpg` chụp thật; sửa hai trang `getting-started/index.md` (vi/en) trỏ sang `.jpg`.

**Interfaces — Consumes:** `test`, `locales`, `capture` (Task 2).

Section mẫu: với mỗi locale, `test(\`overview [${locale}]\`)`:
- `useLocale`;
- `annotate` sidebar (1), header (2), vùng nội dung (3);
- `blur` khối tài khoản ở chân sidebar (tên và email);
- `shot({ locale, chapter: 'getting-started', name: 'overview' })`.

Bảng chú thích `# / Thành phần` trong hai trang được cập nhật cho khớp ba số.

- [ ] **Step 1: Dựng môi trường local:**
  - `docker compose up -d postgres mailpit`;
  - chạy API (`dotnet run --project backend/src/StarterKit.API`) và app (`bun run --cwd frontend dev`);
  - tạo dữ liệu điều kiện bằng UI hoặc API (đăng ký và xác nhận qua Mailpit, tạo tổ chức, mời thành viên thứ hai, tạo vai trò tuỳ chỉnh, đăng nhập lần hai, có thông báo chưa đọc).

  Ghi lại các bước vào README (Task 4).
- [ ] **Step 2: Chạy với dữ liệu thiếu** (trước khi tạo đủ) → test đỏ, thông báo liệt kê đúng các điều kiện còn thiếu.
- [ ] **Step 3: Chạy đủ** `bun run --cwd frontend guide:capture` → 2 test PASS. Hai file `overview.jpg` tồn tại, mở ra thấy đúng giao diện vi/en, có số chú thích, khối tài khoản đã mờ.
- [ ] **Step 4: Chạy lần hai** → không đăng nhập lại: số phiên trong `GET /api/auth/sessions` không tăng.
- [ ] **Step 5: `bun run --cwd frontend/user-guide/site test && bun run --cwd frontend/user-guide/site build`** → xanh, không còn cảnh báo ảnh thừa.
- [ ] **Step 6: Commit** `feat(guide): capture the getting-started overview from the running app`.

### Task 4: Tài liệu

**Files:**
- Modify: `frontend/user-guide/README.md` (mục mới "Chụp ảnh")
- Modify: `.claude/rules/commands.md`

- [ ] **Step 1: Viết README:**
  - điều kiện chạy (app + API đang chạy, tài khoản, dữ liệu);
  - `.env`;
  - bốn dạng lệnh của spec §10;
  - quy ước section: hộp thoại chỉ mở để chụp, không bấm xác nhận; mọi ghi bị chặn;
  - `annotate`/`blur`/`shot`;
  - vì sao chỉ một context.

  `commands.md`: thêm lệnh `guide:capture`.
- [ ] **Step 2: Kiểm** `bun run --cwd frontend test:run` và `bun run --cwd frontend type-check` → xanh.
- [ ] **Step 3: Commit** `docs(guide): document the screenshot capture tool`.
