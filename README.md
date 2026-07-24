# StarterKit

Bộ khởi tạo (app starter) cho ứng dụng web mới: .NET 10 Clean Architecture ở backend, Vue 3 + Vite ở frontend. Đã có sẵn phần nền tảng dùng chung cho hầu hết ứng dụng — auth, quản lý tài khoản, API key, audit log, file storage, system settings — để bạn bắt đầu code tính năng nghiệp vụ ngay, không phải dựng lại hạ tầng cơ bản từ đầu.

Đây **không phải** một sản phẩm hoàn chỉnh — không có tính năng nghiệp vụ nào được cài sẵn. Xoá/thay các phần không cần, thêm domain của bạn lên trên nền này.

## Đã có sẵn

- **Auth**: đăng ký/đăng nhập email + password (JWT access + refresh token, refresh token lưu dạng SHA-256 hash), xác thực email bắt buộc qua SMTP, đăng nhập Google (credential flow), quản lý session (list/revoke theo thiết bị).
- **Account**: CRUD tài khoản, đổi mật khẩu, profile.
- **ApiKey**: tạo/quản lý API key cho tài khoản.
- **AuditLog**: ghi log hành động.
- **Files**: upload/lưu file (local disk, provider pluggable qua `IStorageProvider`).
- **SystemSettings**: cấu hình hệ thống dạng key/value.

Không có multi-tenancy — mô hình single-user/single-account. Không có admin role/global role — mọi tài khoản đã đăng nhập đều truy cập ngang hàng các API trên; thêm phân quyền theo nhu cầu ứng dụng cụ thể của bạn.

## Kiến trúc

```text
backend/    .NET 10 API — Domain → Application → Infrastructure → API (src/, tests/, StarterKit.sln)
frontend/   Vue 3 + Vite dashboard (src/api, src/stores, src/views, ...)
shared/     docs/ + openapi/ (contract dùng chung, chưa wire codegen)
plans/      lịch sử plan file của các task đã triển khai (tham khảo pattern, không phải tài liệu sản phẩm)
```

Chi tiết layer/rule cho từng phần xem `CLAUDE.md` / `AGENTS.md` và `.claude/rules/`.

## Tech stack

| Thành phần | Công nghệ                                          |
| ---------- | --------------------------------------------------- |
| Backend    | .NET 10, ASP.NET Core, Clean Architecture, EF Core   |
| Database   | PostgreSQL                                           |
| Frontend   | Vue 3 + Vite, Pinia, naive-ui, vue-i18n, Tailwind    |
| Storage    | Local disk hiện tại; pluggable qua `IStorageProvider`|
| Email      | SMTP (MailKit) — bắt buộc, kể cả ở dev               |

## Bắt đầu

### 1. Hạ tầng local (Postgres + Mailpit)

```bash
docker compose up -d
```

Email xác thực tài khoản là bắt buộc ngay cả ở môi trường dev — không có seeder/bypass. Dùng Mailpit (`http://localhost:8025`) để xem email xác thực gửi ra khi test đăng ký local, thay vì cần SMTP thật.

### 2. Backend (`backend/`)

```bash
# Copy config mẫu
cp backend/src/StarterKit.API/appsettings.Example.json backend/src/StarterKit.API/appsettings.json

# Build — serialized, parallel build broken trong .NET 10 env này
dotnet build backend/StarterKit.sln --no-restore -m:1

# Áp dụng migration
dotnet ef database update --project backend/src/StarterKit.Infrastructure --startup-project backend/src/StarterKit.API

# Run API
dotnet run --project backend/src/StarterKit.API

# Test
dotnet test backend/StarterKit.sln --no-restore -m:1
```

Production: migration tự áp dụng lúc startup qua `Database.MigrateAsync`.

### 3. Frontend (`frontend/`)

Package manager: `bun`.

```bash
bun install --cwd frontend
bun run --cwd frontend dev            # dev server
bun run --cwd frontend build          # type-check + production build
bun run --cwd frontend test:run       # unit test (vitest)
bun run --cwd frontend test:e2e:install && bun run --cwd frontend test:e2e   # e2e (playwright)
bun run --cwd frontend format         # prettier — không có ESLint trong project này
```

### 4. Thử luồng đăng ký

`POST /api/auth/register` → mở Mailpit (`localhost:8025`) → click link xác thực → đăng nhập.

### CI

Chưa có CI workflow chạy build/test trên PR/push (`.github/workflows/` chỉ có `release.yml`, chạy khi tag `v*`). Chạy các lệnh trên local trước khi xin review.

Chi tiết đầy đủ xem `.claude/rules/commands.md`.

## Tài liệu

- Đóng góp / git workflow: [CONTRIBUTING.md](CONTRIBUTING.md)
- Lịch sử thay đổi: [CHANGELOG.md](CHANGELOG.md)
- Quyết định kiến trúc quan trọng: [.claude/decisions.md](.claude/decisions.md)
