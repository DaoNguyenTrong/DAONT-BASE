# Feature map

Bản đồ tính năng theo góc nhìn người dùng: vào bằng đâu, code nằm ở đâu, kiểm tra thế nào. Mỗi tính năng là một mục `##`. Thêm tính năng mới theo cùng cấu trúc.

---

## Hồ sơ và phiên đăng nhập

- **Vào bằng:** avatar ở góc trên bên phải (`app-header-user-menu`) → **Profile** (`user-menu-profile`) → mở dialog `Profile` → tab **Login sessions**. Vai trò: mọi người dùng đã đăng nhập (đã kiểm tra với tài khoản role `User`).
- **Frontend:**
  - Menu: `frontend/src/layouts/UserMenu.vue`
  - Dialog và tab sessions: `frontend/src/components/ProfileDialog.vue`
  - Testid: `profile-sessions-list` (danh sách phiên), `app-header-user-menu`, `user-menu-profile`, `user-menu-logout`
  - Nút quan trọng: `Sign out other devices` (chỉ hiện khi có phiên khác), xác nhận bằng `Confirm` trong dialog cảnh báo của Naive UI
- **Backend:**
  - `GET /api/profile`, `PUT /api/profile`, `PUT /api/profile/password` → `ProfileController` → `IAccountService` (`Application/Services/Accounts/AccountService.cs`)
  - `GET /api/auth/sessions`, `DELETE /api/auth/sessions/{id}`, `POST /api/auth/sessions/revoke-others` → `AuthController`
- **Test đã có:**
  - Backend: `backend/tests/StarterKit.API.Tests/Controllers/ProfileControllerTests.cs`, `AuthControllerTests.cs` (`GetSessions_ReturnsOnlyCallersOwnSessions`, `RevokeSession_NotOwned_ReturnsNotFound`, `RevokeOtherSessions_RevokesAllButCurrent`), `backend/tests/StarterKit.Application.Tests/Services/Accounts/AccountServiceTests.cs`
  - E2E: `frontend/e2e/profile-sessions.spec.ts`
  - Frontend unit: **chưa có** test cho `ProfileDialog.vue` (`loadSessions`, `confirmRevokeOtherSessions` không có test bao phủ)
- **Dữ liệu test:**
  - Tài khoản role `User`: `E2E_USER_USERNAME` / `E2E_USER_PASSWORD` trong `frontend/.env.e2e` (mẫu ở `frontend/.env.e2e.example`)
  - Spec tự tạo một phiên "thiết bị khác" bằng cách đăng nhập rồi đóng context trong `beforeAll`, nên không cần seed phiên riêng
  - Chưa có tài liệu seed riêng cho tài khoản này (cần bổ sung khi dựng `dev-up.sh`)
- **Cách kiểm tra "đã chạy đúng":**
  1. Đăng nhập, mở Profile → Login sessions: thấy mục `This device` và nút `Sign out other devices` khi có phiên khác.
  2. Bấm `Sign out other devices` → `Confirm`: hiện thông báo `Other devices signed out`, chỉ còn `This device`, nút biến mất.
  3. Không có lỗi trong console; `GET /api/auth/sessions` trả về đúng số phiên của chính người dùng.
- **Rủi ro hay gặp:**
  - Dialog cảnh báo của Naive UI cũng có `role="dialog"`, nên locator phải lấy `.last()` khi có hai dialog mở cùng lúc.
  - Chỉ phiên của chính người dùng được liệt kê và thu hồi (`RevokeSession_NotOwned_ReturnsNotFound`); thử nghiệm cần đúng tài khoản.
  - Đổi mật khẩu có khóa riêng sau số lần sai (`ChangePasswordAsync`), tránh khóa nhầm khi thử nhiều lần.
  - Chuỗi giao diện đang kiểm tra ở locale `en` (fixture đặt `app-locale`); đổi locale làm hỏng selector theo text.
