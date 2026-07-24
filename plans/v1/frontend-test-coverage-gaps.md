# Frontend: lấp khoảng trống test (AccountsView, concurrent-401 refresh, router guard, coverage tooling)

## Context

Đánh giá test frontend (30 file, 209 test, tất cả pass, không `.only`/`.skip`) cho thấy chất lượng test hiện có tốt (dùng `deferred()` để test race condition, MSW để mock HTTP thật, branch theo `error.problem.code` thay vì generic message) nhưng còn 3 khoảng trống cụ thể và 1 việc hạ tầng:

1. **`AccountsView.vue`** (237 dòng) — CRUD account, debounce search 300ms, infinite scroll qua `useLazyList` — **không có test nào**, unit lẫn e2e.
2. **`client.ts`** — cơ chế `isRefreshing`/`failedQueue` xử lý nhiều request 401 đồng thời (dòng 18-19, 159-166) — chỉ được test với 1 request 401 tuần tự.
3. **`router/index.ts`** — `beforeEach` guard (dòng 76-86) chỉ được cover bởi `e2e/guards.spec.ts`, cần backend thật + `E2E_USER_USERNAME`/`E2E_USER_PASSWORD` — không có phản hồi nhanh/local.
4. Chưa cài `@vitest/coverage-v8` — hiện tại đánh giá test chỉ theo "có/không có file test", không đo được coverage dòng/nhánh thật.

Nguyên tắc áp dụng xuyên suốt: theo `frontend-conventions.md` (Vitest + `@vue/test-utils`, mock HTTP bằng `msw`, mock Pinia bằng `@pinia/testing`), tái dùng helper đã có (`tests/helpers/render.ts`, `tests/helpers/pinia.ts`, `tests/helpers/msw/server.ts`, `tests/helpers/with-setup.ts`). Thêm dependency mới (Phase 4) **cần xác nhận với user trước khi cài** — không tự ý `bun add`.

Thứ tự phase độc lập với nhau, có thể làm song song hoặc theo thứ tự ưu tiên rủi ro (1 → 2 → 3 → 4).

---

## Phase 1 — Test cho `AccountsView.vue` (ưu tiên cao nhất)

File mới: `frontend/tests/views/AccountsView.test.ts`

### Setup

- Mount qua `renderComponent(AccountsView)` (từ `tests/helpers/render.ts`) — component này dùng `useI18n`, `useRoute`/`useRouter` (qua `useQuerySync`), Pinia (gián tiếp qua `accountApi`/`apiClient`) — cả 3 đều được `renderComponent` cung cấp sẵn.
- Mock `accountApi` bằng MSW (`server.use(...)`), theo đúng shape `AccountPagedResult`/`Account` trong `frontend/src/api/types.ts`. Viết helper `makeAccount(overrides)` và `makePage(items, totalCount)` cùng file test (tương tự `makePage` trong `use-lazy-list.test.ts`).
- `AccountsView` gọi `onMounted(() => list.reset())` → mọi test cần `await flushPromises()` sau `renderComponent` để đợi lần load đầu tiên resolve trước khi assert.

### Case cần cover

**Load & hiển thị danh sách**
- Render danh sách account trả về từ `GET /api/accounts` (kiểm tra tên/username hiển thị trong `NVirtualList`).
- Empty state: `GET /api/accounts` trả `items: []` → hiển thị `t('accounts.empty')`.

**Search (debounce 300ms)**
- Gõ vào ô search → dùng `vi.useFakeTimers()` để verify `list.reset()` (tức 1 lần gọi `GET /api/accounts` mới) chỉ xảy ra **sau** 300ms và chỉ **1 lần** dù gõ nhiều ký tự liên tiếp (advance timer từng bước, gõ lại trước khi hết 300ms để chứng minh debounce reset đúng — pattern giống `onSearchInput` dùng `clearTimeout`).
- **Bẫy kỹ thuật:** `list.reset()` sau debounce là một fetch MSW bất đồng bộ (resolve qua microtask) — `vi.advanceTimersByTime()` thường KHÔNG flush được microtask đó, dẫn tới assert "reset chưa được gọi" sai (false negative). Dùng `await vi.advanceTimersByTimeAsync(300)` thay vì `advanceTimersByTime` + `flushPromises` tách rời, cho các case liên quan tới debounce → fetch.
- `clearSearch()` xoá `searchQuery` và gọi `list.reset()` ngay (không debounce).
- Query string đồng bộ qua `useQuerySync`/`stringQueryField` — có thể để `use-query-sync.test.ts` (đã có, cover composable riêng) chịu trách nhiệm phần đó; ở đây chỉ cần verify `searchQuery` truyền đúng vào `fetchPage` filter.

**Infinite scroll**
- Giả lập sự kiện `scroll` trên `NVirtualList` (`el.scrollTop + el.clientHeight >= el.scrollHeight - 200`) → gọi `list.loadMore()` → verify `GET /api/accounts` được gọi với `pageNumber` tăng dần.
- Không gọi thêm khi `list.hasMore` đã `false` (đã có test tương đương ở mức composable trong `use-lazy-list.test.ts` — ở đây chỉ cần 1 case tích hợp xác nhận `onScroll` thực sự nối tới `loadMore`).

**Create dialog** (`openCreateDialog`)
- Mock `useAppDialogNaive` giống pattern trong `use-app-dialog-naive.test.ts` (mock `naive-ui`'s `useDialog` → capture object truyền cho `dialog.create`), **hoặc** đơn giản hơn: `vi.mock('@/composables/use-app-dialog-naive')` để capture trực tiếp `open(component, options)` — ưu tiên cách này vì AccountsView không quan tâm nội bộ dialog, chỉ quan tâm `options.onConfirm` được gọi đúng.
- Click nút "Create" → verify `open` được gọi với `data.isEditing === false` và `data.state` là object rỗng đúng field mặc định (`status: true`, còn lại rỗng/`''`).
- Gọi `options.onConfirm(close)` thủ công trong test (mô phỏng user bấm Save trong dialog) → verify `POST /api/accounts` được gọi với payload đã `trim()`/chuyển `''` → `null` cho `phone`/`position`/`address` (đúng logic dòng 66-71), verify `showSuccessMessage` được gọi, và `list.reset()` được gọi lại (verify `GET /api/accounts` gọi thêm 1 lần sau khi confirm).

**Edit dialog** (`openEditDialog`)
- Verify `state` được seed đúng từ `account` truyền vào (bao gồm case `phone: null` → state field phải là `''` chứ không phải `null`, đúng dòng 82-84).
- Verify `onConfirm` gọi `PUT /api/accounts/:id` với `UpdateAccountRequest` (không có `password`).

**Delete** (`confirmDelete`)
- `AccountsView.vue` gọi `requestConfirmation`/`showSuccessMessage` không có hậu tố — hai hàm này export từ `@/lib/feedback` (không phải `feedback-naive.ts`, vốn export bản có hậu tố `*Naive` mà `feedback.ts` delegate tới). `ProfileDialog.test.ts` đã mock đúng module này (`vi.mock('@/lib/feedback')` / `import * as feedback from '@/lib/feedback'`) — copy pattern đó, không mock `feedback-naive.ts`.
- Mock `requestConfirmation` để capture `options.accept` rồi gọi trực tiếp (mô phỏng user bấm accept trên dialog confirm thật).
- Verify `DELETE /api/accounts/:id` được gọi, `showSuccessMessage` được gọi, `list.reset()` được gọi lại.

**Format ngày** (`formatDate`)
- `null`/`undefined` → `'-'`.
- Case có giá trị → không cần assert format cụ thể (phụ thuộc locale/Intl runtime), chỉ cần assert không phải `'-'` và không throw.

### Verification Phase 1

```bash
bun run --cwd frontend test:run -- AccountsView
```
Không cần test thủ công trên trình duyệt riêng cho phase này — nhưng nên chạy `bun run --cwd frontend dev` mở `/accounts` một lần để đối chiếu thủ công rằng debounce/infinite-scroll/dialog thật khớp với giả định trong test. `accountApi`, `useLazyList`, `useAppDialogNaive` đều auto-import từ `src/api/**`/`src/composables/**` (đã xác nhận qua `vite.config.ts`); `requestConfirmation`/`showSuccessMessage` auto-import từ `@/lib/feedback` (đã xác nhận ở mục Delete phía trên — không phải `feedback-naive.ts`).

---

## Phase 2 — Test concurrent-401 refresh queue trong `client.ts`

File sửa: `frontend/tests/unit/api/client.test.ts` (thêm case mới vào `describe('apiClient interceptors')` đã có).

### Case cần thêm

**2 request cùng nhận 401, chỉ 1 lần gọi `/api/auth/refresh`**
- Dùng `deferred()` (pattern giống `use-lazy-list.test.ts`) để giữ response `/api/auth/refresh` chưa resolve, cho phép bắn 2 request tới `/api/test-protected` gần như đồng thời trước khi refresh xong.
- MSW handler cho `/api/test-protected`: request đầu luôn 401 lần gọi đầu tiên của mỗi original request (dùng counter theo url/thứ tự), sau khi refresh xong thì trả `{ ok: true }`.
- Bắn `Promise.all([apiClient.get('/api/test-protected'), apiClient.get('/api/test-protected')])` (không `await` từng cái) → cả 2 phải cùng chờ, `refreshCalls` chỉ tăng lên 1 sau khi cả 2 resolve xong — verify qua counter đếm số lần `POST /api/auth/refresh` được handler nhận, không phải suy luận gián tiếp.
- Verify cả 2 response đều retry thành công (`response.data` đúng) sau khi refresh resolve.

**Request thứ 2 cũng bị reject khi refresh thất bại (qua `failedQueue`)**
- Tương tự nhưng handler `/api/auth/refresh` trả 401 → verify **cả 2** promise đều reject (không chỉ request khởi tạo refresh), và `auth.clearAuth()` chỉ chạy 1 lần (verify qua state cuối, không cần đếm lời gọi).

### Verification Phase 2

```bash
bun run --cwd frontend test:run -- client
```

---

## Phase 3 — Unit test cho router guard (`router/index.ts`)

File mới: `frontend/tests/unit/router/guards.test.ts`. `useHomeRoute()` (`src/composables/use-home-route.ts`) đã xác nhận trả về `{ name: 'home' }` — dùng giá trị này để assert case guest-redirect bên dưới.

### Cách tiếp cận — chọn Option A (khuyến nghị)

**Option A — extract guard thành hàm thuần, test trực tiếp không qua router thật:**

Sửa `frontend/src/router/index.ts`, tách nội dung `router.beforeEach` thành hàm export riêng:

```ts
export function resolveGuardRedirect(
  to: { meta: { requiresAuth?: boolean; guestOnly?: boolean } },
  auth: { isAuthenticated: boolean },
) {
  if (to.meta.requiresAuth && !auth.isAuthenticated) {
    return { name: 'login' }
  }
  if (to.meta.guestOnly && auth.isAuthenticated) {
    return useHomeRoute()
  }
  return undefined
}

router.beforeEach((to) => resolveGuardRedirect(to, useAuthStore()))
```

Test gọi thẳng `resolveGuardRedirect({ meta: {...} }, { isAuthenticated: true|false })` — không cần Pinia, không cần router thật, không có state chia sẻ giữa test nào để rò rỉ. Đây là thay đổi nhỏ ở source (tách hàm, không đổi hành vi) để đổi lấy test đơn giản, xác định — ưu tiên cách này trừ khi người triển khai có lý do cụ thể để không sửa source.

**Option B — test qua singleton router thật (chỉ dùng nếu không muốn sửa `router/index.ts`):**

`router` là singleton tạo bằng `createWebHashHistory()`, guard đọc `useAuthStore()` qua `getActivePinia()` tại thời điểm điều hướng — không cần mount component, chỉ cần `setupTestPinia()` rồi `await router.push({ name: '...' })` và assert `router.currentRoute.value.name`. Rủi ro: `router` là **singleton dùng chung giữa các test file trong cùng process** chạy hash history thật → cần `beforeEach: await router.push('/login')` để reset trạng thái sạch trước mỗi test, và **bắt buộc chạy lại toàn bộ suite** sau khi thêm file này để xác nhận không rò rỉ sang test khác (vd `LoginView.test.ts` dùng router riêng qua `renderComponent`, không đụng singleton này nên rủi ro thấp, nhưng cần xác nhận thực tế chứ không suy đoán).

### Case cần cover (áp dụng cho cả 2 option, đổi cách gọi tương ứng)

- Chưa đăng nhập (`auth.isAuthenticated === false`) → điều hướng tới route `requiresAuth: true` (`accounts`) → bị redirect tới `login`.
- Đã đăng nhập → điều hướng tới route `guestOnly: true` (`login`) → bị redirect tới `{ name: 'home' }` (khớp `useHomeRoute()`).
- Đã đăng nhập → điều hướng tới `accounts` (`requiresAuth`, không `guestOnly`) → **không** bị redirect (`resolveGuardRedirect` trả `undefined`, hoặc `currentRoute.name === 'accounts'` ở Option B).
- Chưa đăng nhập → điều hướng tới `login` (`guestOnly`, không `requiresAuth`) → **không** bị redirect.
- Route không có `meta.requiresAuth`/`meta.guestOnly` (`verify-email`) → luôn cho qua bất kể trạng thái auth.

### Verification Phase 3

```bash
bun run --cwd frontend test:run -- guards
bun run --cwd frontend test:run   # full suite — nếu chọn Option B, xác nhận không có rò rỉ router state ảnh hưởng file khác
bun run --cwd frontend build      # nếu chọn Option A, xác nhận tách hàm không phá type-check
```

---

## Phase 4 — Cài `@vitest/coverage-v8` để đo coverage thật

**Dừng lại xin xác nhận user trước khi chạy `bun add`** — đây là thêm dependency mới, theo `frontend-conventions.md` cần xác nhận trước.

### Nếu được đồng ý

- `bun add -D @vitest/coverage-v8 --cwd frontend`
- Thêm vào `frontend/vitest.config.ts`:
  ```ts
  test: {
    // ...giữ nguyên các field hiện có
    coverage: {
      provider: 'v8',
      reporter: ['text', 'html'],
      include: ['src/**/*.{ts,vue}'],
      exclude: ['src/typings/**', 'src/main.ts'],
    },
  },
  ```
- Thêm script vào `package.json`: `"test:coverage": "vitest run --coverage"`.
- Chạy `bun run --cwd frontend test:coverage` một lần để lấy baseline % thật — dùng baseline này để xác nhận lại (hoặc điều chỉnh) danh sách gap đã nêu ở Phase 1-3, và phát hiện thêm gap ở mức nhánh (branch) trong các file đã có test nhưng có thể chưa cover hết (vd `client.ts` các nhánh lỗi khác nhau trong `toProblemDetails`).
- Không set ngưỡng fail-under-% ở phase này (chưa có baseline, set ngưỡng tuỳ tiện sẽ gây noise) — để user quyết định ngưỡng sau khi thấy số thật.

### Verification Phase 4

```bash
bun run --cwd frontend test:coverage
```
Xem báo cáo HTML (`frontend/coverage/index.html`) để xác nhận `AccountsView.vue`, `router/index.ts`, và nhánh concurrent-401 trong `client.ts` (Phase 1-3) đã lên coverage cao; các file layout (`AppHeader`, `AppSidebar`,...) và `src/lib/validation.ts` sẽ hiện rõ là 0% — ghi nhận làm backlog riêng, không nằm trong scope plan này.

---

## Verification tổng thể (sau khi xong cả 4 phase)

```bash
bun run --cwd frontend build      # type-check sạch
bun run --cwd frontend test:run   # toàn bộ suite pass, không còn `.only`/`.skip`
```

Không cần chạy `dotnet test`/GitNexus impact — toàn bộ thay đổi nằm trong `frontend/tests/` (và `vitest.config.ts`/`package.json` ở Phase 4), không đụng `backend/`.

## Ghi chú phạm vi (không nằm trong plan này)

Từ đánh giá ban đầu, các gap mức độ thấp hơn **cố tình không đưa vào plan** (để giữ phạm vi tập trung vào rủi ro cao nhất):
- `src/lib/validation.ts` (`mapValidationErrors`) — hàm thuần nhỏ, dễ test nhưng rủi ro thấp.
- `src/layouts/*.vue` — chưa có test nào, nhưng phần lớn là trình bày (presentational), rủi ro logic thấp hơn `AccountsView`.
- `use-home-route.ts`, `use-smart-back.ts`, `use-theme-preference.ts` — composable nhỏ.

Nếu muốn, có thể mở plan `v2` riêng cho nhóm này sau khi Phase 4 (coverage baseline) cho số liệu cụ thể để ưu tiên đúng.
