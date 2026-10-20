# Nhật ký lỗi agent lặp lại

Ghi lại các lỗi agent mắc phải khi làm việc trên repo này. Mục tiêu theo `agent-automation-playbook.md` (mục 4.4): sau 2–3 tuần, mỗi lỗi lặp lại phải được chuyển thành lint rule, test kiến trúc, test hồi quy, hoặc generator, không chỉ thêm một dòng vào prompt.

Mỗi mục có: ngày, việc đang làm, lỗi, nguyên nhân, và hành động đã làm hoặc cần làm.

Quy ước: thêm mục mới ở trên cùng. Khi một lỗi đã được tự động hóa, ghi "Đã chuyển thành: …" vào mục đó.

---

### 2026-10-19 — Đoán route và copy UI thay vì đọc router và locale

- **Việc đang làm:** chạy suite e2e cho luồng hồ sơ và phiên đăng nhập.
- **Lỗi:** viết `guards.spec.ts` theo route `/accounts` (lấy từ tài liệu cũ), và spec profile-sessions theo copy "Login sessions", "Sign out other devices". Route không tồn tại trong router, copy đã đổi trong `locales/en.ts`.
- **Nguyên nhân:** không kiểm tra router và locale trước khi viết selector và assertion.
- **Hành động:** đã xóa test `/accounts`, sửa copy theo locale. Cần: quy định trong `testing.md` là chọn selector theo `data-testid`, và đối chiếu route với `router/index.ts` trước khi viết test điều hướng.

### 2026-10-19 — Sửa race bằng một lệnh không làm gì

- **Việc đang làm:** sửa flaky của logout.
- **Lỗi:** thêm `waitForLoadState('load')` sau logout và báo là đã sửa, nhưng nó trả về ngay trên document cũ. Test vẫn flaky.
- **Nguyên nhân:** `reloadTo` đổi URL bằng `replaceState` trước khi `reload()`, nên URL đúng trước khi reload bắt đầu.
- **Hành động:** đã sửa bằng `waitForEvent('load')` trước khi bấm, và kiểm chứng bằng `--repeat-each=10 --retries=0` (10/10). Cần: mọi sửa flaky phải có bằng chứng chạy lặp không retry, không chỉ một lần pass.

### 2026-10-19 — Coi cookie là định danh phiên

- **Việc đang làm:** sửa "đăng xuất thiết bị khác".
- **Lỗi:** giả định phiên hiện tại được xác định bằng cookie `refresh_token`. Client chỉ có Bearer token bị thu hồi luôn phiên đang dùng, trái với mô tả endpoint.
- **Nguyên nhân:** không đọc hết các đường phát hành token (switch-organization, rotate refresh) trước khi thiết kế.
- **Hành động:** đã chuyển sang claim `session_id` trong access token, có regression test và bằng chứng Bearer-only. Cần: trước khi đổi một định danh, liệt kê mọi nơi tạo nó.

### 2026-10-19 — Ghi file tạm vào `/tmp`

- **Việc đang làm:** kiểm tra response đăng nhập của API.
- **Lỗi:** ghi response (có token) vào `/tmp/claude-login.json`, vi phạm quy tắc dùng thư mục scratchpad.
- **Nguyên nhân:** không chọn thư mục scratchpad cho file tạm.
- **Hành động:** đã xóa file. Cần: dùng thư mục scratchpad hoặc `verify-output/` (đã gitignore) cho mọi file tạm.
