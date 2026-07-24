# Frontend: đồng bộ với Auth backend mới (email verification + social login + unified errors)

## Context

Backend đã merge 2 PR vào `dev`:

- **#13** — `POST /api/auth/register` không còn auto-login: tạo account chưa `EmailConfirmed`, gửi email verify (SMTP thật), trả `202 Accepted` + `RegisterResult`. `POST /api/auth/login` giờ 401 nếu email chưa verify. Thêm `POST /api/auth/verify-email`, `POST /api/auth/resend-verification`, `POST /api/auth/external/{provider}` (social login Google, credential-forwarding).
- **#14** — Mọi lỗi API (exception nghiệp vụ, validation 400, rate-limit 429) giờ trả cùng 1 shape `{status, title, detail, code, errors?}` — `code` là key ổn định (vd `EmailNotConfirmed`, `ValidationFailed`, `TooManyRequests`) để client branch theo, không cần parse chuỗi đã localize.

Frontend hiện **chưa đồng bộ** với 2 PR trên: `authApi.register()` vẫn theo contract cũ (tự set token), 4 endpoint mới chưa có hàm gọi, 2 view (Register, VerifyEmail) chưa tồn tại, `LoginView.vue` không phân biệt được lỗi sai mật khẩu với email chưa xác thực (cả hai đều 401), và social login Google chưa có UI.

Nguyên tắc áp dụng xuyên suốt: theo `frontend-conventions.md` (setup-store Pinia, `<script setup lang="ts">`, api module dạng object export, `@/*` alias), `localization.md` (thêm đủ key vi/en, không render chuỗi BE trực tiếp — map theo `code`), `api-contract.md` (khi đổi field/response BE phải đồng bộ `types.ts` theo đúng camelCase wire shape).

Reuse chính: pattern `guestOnly`/`requiresAuth` đã có ở `/login` (`frontend/src/router/index.ts`), pattern bắt lỗi `error instanceof ApiError` + `error.problem.detail ?? t(...)` đã dùng ở `ProfileView.vue`/`LoginView.vue`, `ApiError`/`ProblemDetails`/`toProblemDetails()` đã có sẵn field `code` (merge từ PR #14).

---

## Phase 1 — Types & API layer (nền tảng, làm trước mọi thứ khác)

### `frontend/src/api/types.ts`

- `Account` và `ProfileDto`: thêm `emailConfirmed: boolean` (khớp `AccountDto.EmailConfirmed`/`ProfileDto.EmailConfirmed` phía BE).
- Thêm mới:
  ```ts
  export interface RegisterResult {
    accountId: string
    email: string
  }

  export interface VerifyEmailRequest {
    token: string
  }

  export interface ResendVerificationRequest {
    email: string
  }

  export interface ExternalLoginRequest {
    credential: string
  }
  ```
- `RegisterRequest` giữ nguyên (không đổi shape phía BE).
- `ProblemDetails.code?: string` — **đã có sẵn**, không cần sửa (merge từ PR #14).

### `frontend/src/api/auth-api.ts`

- Sửa `register()`: đổi kiểu trả về từ `AuthResponse` sang `RegisterResult`, **bỏ** lệnh gọi `useAuthStore().setAuth(...)` (BE không còn trả token ở bước này).
  ```ts
  const register = async (data: RegisterRequest): Promise<RegisterResult> => {
    const response = await apiClient.post<RegisterResult>('/api/auth/register', data)
    return response.data
  }
  ```
- Thêm 3 hàm mới, theo đúng pattern `login`/`refreshToken` (gọi `setAuth` sau khi có token):
  ```ts
  const verifyEmail = async (data: VerifyEmailRequest): Promise<AuthResponse> => {
    const response = await apiClient.post<AuthResponse>('/api/auth/verify-email', data)
    useAuthStore().setAuth(response.data)
    return response.data
  }

  const resendVerification = async (data: ResendVerificationRequest): Promise<void> => {
    await apiClient.post('/api/auth/resend-verification', data)
  }

  const externalLogin = async (
    provider: string,
    data: ExternalLoginRequest,
  ): Promise<AuthResponse> => {
    const response = await apiClient.post<AuthResponse>(`/api/auth/external/${provider}`, data)
    useAuthStore().setAuth(response.data)
    return response.data
  }
  ```
- Export cả 3 hàm mới trong default export object.

### Verification Phase 1

- `bun run --cwd frontend build` (type-check) phải sạch — chưa có UI dùng các hàm mới nên chỉ cần compile qua được.
- Chưa cần test thủ công trên trình duyệt ở phase này (chưa có UI gọi tới).

---

## Phase 2 — Login UX dùng `code` (nhanh, độc lập với Phase 3/4)

### `frontend/src/views/LoginView.vue`

Hiện tại (dòng ~49-56): mọi lỗi 401 gộp chung 1 message `t('auth.invalidCredentials')`. Sửa thành branch theo `error.problem.code`:

```ts
if (error instanceof ApiError) {
  if (error.problem.code === 'EmailNotConfirmed') {
    submitError.value = t('auth.emailNotConfirmed')
    showResendVerification.value = true // state mới, hiện link/nút resend
  } else if (error.status === 401) {
    submitError.value = t('auth.invalidCredentials')
  } else {
    submitError.value = error.problem.detail ?? t('auth.loginFailed')
  }
} else {
  submitError.value = t('auth.loginFailed')
}
```

- Thêm state `showResendVerification` + 1 nút/link nhỏ dưới form gọi `authApi.resendVerification({ email: ... })` — cần ô nhập email riêng vì `LoginView` hiện chỉ có `username`, không chắc là email (username và email là 2 field khác nhau ở BE — `RegisterRequest` có cả hai). Đơn giản nhất: khi hiện nhánh này, cho user nhập lại email trong 1 input nhỏ hoặc dialog, không cố suy ra từ `username`.
- Component dùng chung: cân nhắc tách 1 `ResendVerificationForm.vue` nhỏ (dùng lại được ở cả `LoginView` và `VerifyEmailView` — xem Phase 3) thay vì viết 2 lần.

### i18n (`frontend/src/locales/{vi,en}.ts`)

Thêm dưới namespace `auth`: `emailNotConfirmed`, `resendVerification`, `resendVerificationSuccess`, `resendVerificationEmailLabel`.

### Verification Phase 2

- `bun run --cwd frontend test:run` — thêm/sửa test cho `LoginView.vue` (mock 401 với `code: 'EmailNotConfirmed'` qua msw, assert hiện đúng nhánh UI).
- Test thủ công trên trình duyệt: đăng nhập bằng 1 account chưa verify (tạo qua API trực tiếp hoặc chờ Phase 3 có RegisterView) → xác nhận thấy đúng message + nút resend, KHÔNG bị nhầm với sai mật khẩu.

---

## Phase 3 — Register + Verify Email UI (view mới)

### `frontend/src/views/RegisterView.vue` (mới)

- Route `/register`, `meta: { guestOnly: true }` (giống `/login`).
- Form: `name`, `username`, `email`, `password`, `phone?`, `position?`, `address?` — khớp `RegisterRequest`.
- Validation phía client tối thiểu (required, email format, password >= 8 ký tự) — validation đầy đủ vẫn do BE trả qua `errors` dict (400 `ValidationFailed`), hiện inline theo field như `AccountForm.vue` đã làm.
- Submit thành công (202) → **không** redirect vào dashboard — chuyển sang trạng thái "đã gửi email xác thực, kiểm tra hộp thư" (có thể là 1 state trong cùng view, không cần route riêng).
- Lỗi 409 (`AccountUsernameAlreadyExists` / `AccountEmailAlreadyExists`, phân biệt qua `error.problem.code`) → hiện đúng message theo từng field.
- Link "Đã có tài khoản? Đăng nhập" trỏ `/login`.

### `frontend/src/views/VerifyEmailView.vue` (mới)

- Route `/verify-email`, đọc query `?token=` (khớp URL BE build: `{FrontendBaseUrl}/verify-email?token=...`).
- **Không** gắn `guestOnly: true` — user bấm link từ email có thể ở bất kỳ trạng thái auth nào.
- `onMounted`: nếu có `token` → gọi `authApi.verifyEmail({ token })`.
  - Thành công → `setAuth` đã tự chạy trong `verifyEmail()`, redirect vào `useHomeRoute()` (dùng lại composable đã có trong `LoginView.vue`).
  - Lỗi (401 `EmailVerificationTokenInvalidOrExpired`) → hiện message + form resend verification (dùng lại `ResendVerificationForm.vue` từ Phase 2 nếu đã tách).
- Nếu không có `token` trong query → hiện trạng thái lỗi chung ("liên kết không hợp lệ").

### Router (`frontend/src/router/index.ts`)

Thêm 2 route entry theo đúng pattern hiện có (`/login` ở dòng ~65-68):
```ts
{
  path: '/register',
  name: 'register',
  component: () => import('@/views/RegisterView.vue'),
  meta: { guestOnly: true },
},
{
  path: '/verify-email',
  name: 'verify-email',
  component: () => import('@/views/VerifyEmailView.vue'),
},
```

### i18n

Thêm namespace `auth.register.*` (label form, success state) và `auth.verifyEmail.*` (đang xác thực, thành công, thất bại).

### Verification Phase 3

- `bun run --cwd frontend test:run` — unit test mới cho `RegisterView.vue`, `VerifyEmailView.vue` (mock 202/401 qua msw, theo pattern các view test hiện có).
- Test thủ công qua trình duyệt (bắt buộc theo quy ước UI của repo): `bun run --cwd frontend dev`, đăng ký account mới → kiểm tra email thật (Mailpit hoặc SMTP đã cấu hình ở BE) → bấm link → xác nhận login thành công vào dashboard. Test thêm case token hết hạn/đã dùng.

---

## Phase 4 — Social login (Google)

### Quyết định cần chốt trước khi code

Cách lấy `id_token` phía client — 2 hướng chính, cần user chọn trước khi implement:

1. **Google Identity Services (GSI) script thuần** (`https://accounts.google.com/gsi/client`) — load qua thẻ `<script>`/dynamic import, gọi `google.accounts.id.initialize({ client_id, callback })` + render nút hoặc One Tap. Không thêm dependency vào `package.json`, nhưng phải tự viết wrapper TypeScript (không có type chính thức từ Google, cần khai báo type thủ công hoặc dùng `@types/google.accounts` cộng đồng).
2. **Thư viện Vue wrapper** (vd `vue3-google-login`) — API gọn hơn, có sẵn component `<GoogleLogin>`, nhưng thêm 1 dependency mới cần review (license, kích thước bundle, có được maintain không).

Khuyến nghị: hướng 1 (GSI thuần) — ít phụ thuộc hơn, khớp tinh thần "credential-forwarding" đã chọn ở backend (chỉ cần lấy đúng `id_token`, không cần thêm abstraction UI phức tạp).

### Việc cần làm (sau khi chốt hướng)

- Thêm `VITE_GOOGLE_CLIENT_ID` vào `frontend/.env.example` (và `.env.development` cục bộ, không commit).
- Tạo 1 composable `useGoogleLogin()` hoặc component `GoogleLoginButton.vue` — nhận `id_token` từ callback GSI, gọi `authApi.externalLogin('google', { credential: idToken })`, `setAuth` đã tự chạy trong hàm đó → redirect vào dashboard.
- Thêm nút "Đăng nhập với Google" vào cả `LoginView.vue` và `RegisterView.vue` (đăng nhập lần đầu qua Google = tự tạo account, theo logic `ExternalLoginAsync` phía BE).
- Lỗi cần xử lý riêng: 409 `ExternalLoginEmailNotConfirmed` (đã có account local cùng email nhưng chưa verify — BE từ chối auto-link) → message hướng dẫn verify email thường trước.

### i18n

`auth.continueWithGoogle`, message cho case `ExternalLoginEmailNotConfirmed`.

### Verification Phase 4

- `bun run --cwd frontend test:run` — mock GSI callback, mock `externalLogin` qua msw.
- Test thủ công: cần Google OAuth Client ID thật (tạo trên Google Cloud Console, loại "Web application", Authorized JavaScript origin = `http://localhost:5173`) — **cần user cung cấp hoặc tự tạo**, không thể giả lập đầy đủ bằng dữ liệu giả như BE đã làm ở PR #13.

---

## Thứ tự triển khai đề xuất

Phase 1 → Phase 2 → Phase 3 → Phase 4. Phase 1 là nền bắt buộc cho mọi phase sau. Phase 2 độc lập, có thể làm sau Phase 1 mà không cần chờ Phase 3. Phase 4 phụ thuộc quyết định thư viện (mục trên) và cần Google Client ID thật để test đầy đủ — có thể lùi lại làm sau cùng nếu chưa có.

## Verification chung (mọi phase)

- `bun run --cwd frontend test:run` phải pass trước khi coi 1 phase là xong.
- `bun run --cwd frontend build` (type-check + production build) phải sạch.
- Theo quy ước repo cho thay đổi UI: khởi động `bun run --cwd frontend dev`, test thủ công qua trình duyệt golden path + edge case trước khi báo hoàn tất — không chỉ dựa vào test suite.
