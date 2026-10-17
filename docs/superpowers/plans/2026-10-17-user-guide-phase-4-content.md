# HDSD — Giai đoạn 4: Nội dung — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** HDSD có đủ nội dung ban đầu của spec §8 bằng cả `vi` và `en`, kèm ảnh chụp thật:
- các trang tác vụ;
- FAQ;
- Thuật ngữ;
- trang chủ có lưới 6 tính năng và dải "Bắt đầu nhanh".

**Architecture:**
- Mỗi chương có một file section `tools/sections/base/<chương>.capture.ts` để chụp ảnh từ sandbox đã seed (xem ledger giai đoạn 3).
- Trang theo template §7.1 và style guide §7.2. Nhãn giao diện in đậm, khớp đúng `frontend/src/locales/{vi,en}.ts`.
- Trang chủ có thêm component `HomeQuickStart.vue`, đọc `frontmatter.quickStart`.

**Spec:** `docs/superpowers/specs/2026-10-15-user-guide-design.md` §5.2, §7, §8, §14 giai đoạn 4.

## Global Constraints

- Nhánh `feature/user-guide-content`. Không commit `docs/superpowers/**`.
- **Slug:** tiếng Anh, giống nhau ở hai ngôn ngữ.
- **Chương:**
  - `getting-started`, `faq`, `glossary` không ẩn được;
  - `account`, `organizations`, `notifications` có `hideable: true`;
  - không link chéo vào chương `hideable` (test `content-integrity`).
- **Nội dung đúng với app hiện tại, không bịa tính năng.**
  - Spec §8 ghi "Sửa thông tin tổ chức (`edit`)", nhưng app chỉ có **Ngưng hoạt động tổ chức** (chủ tổ chức, `organizations.manage`). Trang là `organizations/deactivate`.
  - Spec ghi "Tạm khoá hoặc xoá thành viên", nhưng app chỉ đổi vai trò hoặc xoá. Trang `manage-members` viết theo đúng hai việc đó.
  - Quyền "Tạo tổ chức": mọi người đã đăng nhập (`POST /api/organizations` chỉ có `[Authorize]`).
- **Ảnh:**
  - `.jpg` chụp bằng `guide:capture` từ dữ liệu mẫu;
  - mọi email, tên tài khoản đang đăng nhập, IP và thiết bị đều được làm mờ;
  - hộp thoại chỉ mở để chụp.
- **Trang đăng nhập và đăng ký** chụp bằng một context khách riêng (không nạp phiên): fixture `guest` mới trong `fixtures.ts`, vẫn có chặn ghi.

## Review Focus

1. **Nhãn trong trang lệch với chữ trên giao diện:** đối chiếu từng nhãn in đậm với `locales/{vi,en}.ts`.
2. **Bản `en` thiếu ý so với bản `vi`:** mỗi trang có cùng các mục và cùng số bước.
3. **Ảnh lộ dữ liệu riêng tư:** email, IP, thiết bị không được đọc rõ.
4. **Bảng `# / Thành phần` không khớp số trên ảnh.**
5. **Trang mô tả hành vi app không có:** đối chiếu với component và endpoint.

---

### Task 1: Fixture khách và chương "Bắt đầu"

- `fixtures.ts`: fixture `guest: { page: Page; capture: Capture }`:
  - context mới, không có `storageState`, `serviceWorkers: 'block'`;
  - cùng guard chặn ghi và cùng CSS chụp.
- Trang `getting-started/{register,verify-email,sign-in,sign-out,interface}` (vi/en), mục lục thêm 5 mục.
- Section `getting-started.capture.ts`:
  - `overview` (đã có);
  - `sign-in` (khách, form đăng nhập có nút Google/Microsoft);
  - `register` (khách);
  - `interface-controls` (cụm nút giao diện, cỡ chữ, ngôn ngữ trên header).
- Kiểm: `guide:capture -- sections/base/getting-started` xanh, site `test` + `build` xanh.

### Task 2: Chương "Tài khoản"

- Trang `account/{profile,change-password,sessions,push-notifications}`, chương `hideable`.
- Section `account.capture.ts`: hộp thoại Hồ sơ cá nhân ở từng tab:
  - Thông tin cá nhân (mờ email);
  - Đổi mật khẩu;
  - Phiên đăng nhập (mờ thiết bị/IP);
  - Thông báo đẩy.

### Task 3: Chương "Tổ chức"

- Trang `organizations/{switch,create,deactivate,add-member,manage-members,roles}`:
  - frontmatter `permission` theo §8 và Global Constraints;
  - trang `roles` ghi rõ vai trò hệ thống không sửa hay xoá được, chủ tổ chức luôn có mọi quyền.
- Section `organizations.capture.ts`:
  - menu chuyển tổ chức;
  - danh sách tổ chức;
  - hộp thoại tạo tổ chức;
  - hộp thoại thành viên (mờ email);
  - hộp thoại vai trò;
  - form tạo vai trò.

### Task 4: Chương "Thông báo", FAQ, Thuật ngữ

- `notifications/index` kèm ảnh bảng thông báo.
- `glossary.md` (vi/en):
  - bảng song ngữ §7.3;
  - thuật ngữ khớp locale: chủ tổ chức là vai trò hệ thống "Owner", vai trò hiển thị đúng tên trong app.
- FAQ thêm các câu hỏi thực tế, chỉ link trong chương không ẩn được:
  - quên mật khẩu;
  - không nhận được email xác thực;
  - bị khoá tạm thời;
  - không thấy nút Google;
  - mất phiên sau 15 phút không dùng HDSD.

### Task 5: Trang chủ

- `index.md`/`en/index.md`:
  - `tagline`;
  - action "Mở ứng dụng" (đã có qua slot);
  - `features` 6 thẻ (icon SVG inline, `link`, `linkText`);
  - `quickStart` 3 bước (Đăng nhập → Chọn tổ chức → Cập nhật hồ sơ), mỗi bước có ảnh thu nhỏ lấy lại từ ảnh của trang.
- `theme/components/HomeQuickStart.vue`, chèn qua slot `home-features-after`, chỉ đọc frontmatter.
- `custom.css`:
  - thẻ viền violet-100, hover nâng nhẹ và viền violet-300;
  - lưới 3 cột trên desktop, 1 cột trên điện thoại.
- Test bun: `HomeQuickStart` không giữ chuỗi (đọc frontmatter), và frontmatter `quickStart` của vi/en có đủ 3 bước với link và ảnh tồn tại.

### Task 6: Kiểm và tài liệu

- `guide:capture` toàn bộ: xanh.
- site `test`/`build`/`format:check`: xanh.
- frontend `test:run`/`type-check`: xanh.
- Kiểm bằng mắt từng ảnh và trang chủ (desktop, điện thoại, sáng/tối) qua `vitepress preview`.
