# Feedback Hub

Nền tảng tập trung thu thập và quản lý feedback từ nhiều sản phẩm qua một widget JavaScript nhúng (`<script>`).

Mỗi sản phẩm gắn widget → người dùng gửi feedback (kèm ảnh nếu cần) → team xử lý trên dashboard theo workflow trạng thái. Multi-tenant: feedback được quản lý theo Tenant / Project.

> **Trạng thái hiện tại:** repo đang ở giai đoạn nền tảng — auth, multi-tenant (Tenant/Membership), API key, file storage, audit log, system settings đã có. Phần sản phẩm cốt lõi (embed widget, gửi feedback, dashboard xử lý workflow) **chưa được implement**, xem tiến độ theo phase tại [shared/docs/architecture/FeedbackHub-MVP-Implementation-Plan.md](shared/docs/architecture/FeedbackHub-MVP-Implementation-Plan.md).

## Kiến trúc tổng quan

```text
Product A / B / C
        │
        ▼
 Feedback Widget (JS SDK)         ← chưa implement
        │
      HTTPS
        │
        ▼
  Feedback API (.NET, Clean Architecture)
        │
        ├── PostgreSQL
        ├── File Storage (Local disk hiện tại; provider pluggable qua IStorageProvider)
        └── Redis (chưa wire, cân nhắc sau nếu cần)

 Dashboard (Vue 3 + Vite)
        │
     REST API
```

## Phạm vi MVP

**Đã có:** đăng ký/đăng nhập email + password (JWT), Tenant + Membership (Owner/Member) qua header `X-Tenant-Id`, invite member, transfer ownership, API key, file storage (local disk), audit log, system settings, dashboard shell (login, accounts, profile).

**Đang làm / chưa có:** trash Tenant + purge sau 30 ngày, Project + ApiKey + generate embed script, rate limit theo ApiKey/IP, public API nhận feedback + đính kèm ảnh, dashboard quản lý feedback (list/filter, đổi status, comment nội bộ), object storage provider ngoài local disk (S3/R2/MinIO).

**Ngoài phạm vi MVP:** social login (Google/GitHub/Microsoft), AI phân loại, tích hợp Jira/GitHub, notification, analytics nâng cao, voting.

## Workflow (feedback, khi implement xong)

```text
NEW → TRIAGED → IN_PROGRESS → RESOLVED → CLOSED
 NEW → REJECTED
```

## Tech stack

| Thành phần   | Công nghệ                        |
| ------------ | --------------------------------- |
| Backend      | .NET 10, ASP.NET Core, Clean Architecture |
| Database     | PostgreSQL (EF Core)              |
| Frontend     | Vue 3 + Vite, Pinia, naive-ui, vue-i18n |
| Storage      | Local disk hiện tại; S3/R2/MinIO planned (pluggable provider) |
| Cache        | Redis — chưa wire, cân nhắc sau nếu cần |

## Cấu trúc repo

```text
backend/    .NET 10 API — Domain → Application → Infrastructure → API (src/, tests/, FEEDBACK-HUB.sln)
frontend/   Vue 3 + Vite dashboard (src/api, src/stores, src/views, ...)
shared/     docs/ (api, architecture) + openapi/ (contract dùng chung, chưa wire codegen)
plans/      plan file cho các task đang triển khai
```

Chi tiết layer/rule cho từng phần xem `CLAUDE.md` / `AGENTS.md` và `.claude/rules/`.

## Bắt đầu

### Backend (`backend/`)

```bash
# Build — serialized, parallel build broken trong .NET 10 env này
dotnet build backend/FEEDBACK-HUB.sln --no-restore -m:1

# Run API
dotnet run --project backend/src/FeedbackHub.API

# Test
dotnet test backend/FEEDBACK-HUB.sln --no-restore -m:1

# EF Core migrations — apply
dotnet ef database update --project backend/src/FeedbackHub.Infrastructure --startup-project backend/src/FeedbackHub.API

# EF Core migrations — thêm mới
dotnet ef migrations add <MigrationName> --project backend/src/FeedbackHub.Infrastructure --startup-project backend/src/FeedbackHub.API
```

Production: migration tự áp dụng lúc startup qua `Database.MigrateAsync`.

### Frontend (`frontend/`)

Package manager: `bun`.

```bash
# Install deps
bun install --cwd frontend

# Dev server
bun run --cwd frontend dev

# Type-check + production build
bun run --cwd frontend build

# Unit test (vitest)
bun run --cwd frontend test:run

# E2E test (playwright — cài browser 1 lần)
bun run --cwd frontend test:e2e:install
bun run --cwd frontend test:e2e

# Format (prettier — không có ESLint trong project này)
bun run --cwd frontend format
```

### CI

Chưa có CI workflow chạy build/test trên PR/push (`.github/workflows/` chỉ có `release.yml`). Chạy các lệnh trên local trước khi xin review.

Chi tiết đầy đủ xem `.claude/rules/commands.md`.

## Tài liệu

- Kiến trúc sản phẩm MVP: [shared/docs/architecture/FeedbackHub-MVP-Architecture.md](shared/docs/architecture/FeedbackHub-MVP-Architecture.md)
- Kiến trúc backend: [shared/docs/architecture/FeedbackHub-Backend-Architecture.md](shared/docs/architecture/FeedbackHub-Backend-Architecture.md)
- Kế hoạch triển khai theo phase: [shared/docs/architecture/FeedbackHub-MVP-Implementation-Plan.md](shared/docs/architecture/FeedbackHub-MVP-Implementation-Plan.md)
- API contract hiện có (Tenants): [shared/docs/api/api-tenants.md](shared/docs/api/api-tenants.md), [shared/docs/api/integration-tenants.md](shared/docs/api/integration-tenants.md)
- Đóng góp / git workflow: [CONTRIBUTING.md](CONTRIBUTING.md)
- Lịch sử thay đổi: [CHANGELOG.md](CHANGELOG.md)
