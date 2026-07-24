# Feedback Hub — Backend Architecture

Tài liệu thiết kế tổng quan backend cho MVP. Bám [FeedbackHub-MVP-Architecture.md](./FeedbackHub-MVP-Architecture.md) và Clean Architecture hiện có trong repo.

**Phạm vi repo này:** Backend API + embed script (generate theo Tenant/Project). Dashboard Vue ở repo riêng.

---

## 1. Mục tiêu

Xây dựng một ASP.NET Core API duy nhất (monolith) để:

- Nhận feedback từ embed script qua ApiKey (Project-scoped)
- Dashboard (repo riêng) gọi API qua JWT + header `X-Tenant-Id`
- Auth MVP: register/login email + password; **không** bootstrap admin; **không** social login trong MVP
- Multi-tenant: Account → tạo Tenant (1 Owner) → Project → invite Member
- Workflow trạng thái + comment nội bộ + đính kèm ảnh (giới hạn size)
- Rate limit theo ApiKey (Owner cấu hình) + theo IP; Id = UUID v7
- Tenant trash → purge toàn bộ dữ liệu sau 30 ngày

Không tách microservices ở giai MVP.

---

## 2. Nguyên tắc kiến trúc

Giữ 4 layer hiện tại:

| Layer | Project | Trách nhiệm |
|-------|---------|-------------|
| Domain | `FeedbackHub.Domain` | Entities, enums, domain rules, repository interfaces |
| Application | `FeedbackHub.Application` | Services, DTOs, settings, Mapperly |
| Infrastructure | `FeedbackHub.Infrastructure` | EF Core (PostgreSQL), storage, JWT, rate limit, repositories |
| API | `FeedbackHub.API` | Controllers, middleware (`X-Tenant-Id`), OpenAPI/Scalar |

```text
API  →  Application  →  Domain
              ↑
        Infrastructure
```

**Entity pattern:** private constructor + `static Create(XxxParams p)` + `Update(XxxParams p)`. Validation trong Domain.

**Id:** UUID v7 cho mọi entity (không tuần tự, khó đoán).

---

## 3. Sơ đồ tổng quan

```text
┌──────────────────────┐     ┌──────────────────────┐
│ Embed script         │     │ Dashboard (repo riêng)│
│ (generate T/P)       │     │ JWT + X-Tenant-Id     │
│ ApiKey               │     │                       │
└──────────┬───────────┘     └──────────┬────────────┘
           │                            │
           ▼                            ▼
┌────────────────────────────────────────────────────┐
│              FeedbackHub.API                       │
│  PublicFeedback │ Feedback │ Tenants │ Projects …  │
│  + rate limit (ApiKey / IP)                        │
└────────────────────────────┬───────────────────────┘
                             │
┌────────────────────────────▼───────────────────────┐
│           FeedbackHub.Application                  │
│  Tenant / Membership / Project / Feedback / …      │
└────────────────────────────┬───────────────────────┘
                             │
┌──────────────┐   ┌─────────▼───────────────────────┐
│   Domain     │◄──┤     Infrastructure              │
│  Entities    │   │  EF / PostgreSQL (UUID v7)      │
│  Enums       │   │  S3 / R2 / MinIO                │
│  Rules       │   │  Rate limiter                   │
└──────────────┘   └─────────────────────────────────┘
```

| Client | Auth | Context |
|--------|------|---------|
| Embed script | ApiKey (Project) | Project từ ApiKey |
| Dashboard | JWT + **`X-Tenant-Id`** | Membership check; thiếu Tenant → data trống |

---

## 4. Mô hình tổ chức (đã chốt)

```text
Account  (không Role toàn cục)
  └── TenantMembership (Owner | Member | … sau)
        └── Tenant  (đúng 1 Owner; soft-delete / trash)
              └── Project (ApiKey + embed script config)
                    └── Feedback
                          ├── Attachment*
                          └── Comment*
```

### Luồng người dùng

```text
Register email + password → Account (JWT)
  → tạo Tenant (creator = Owner duy nhất)
    → tạo Project + ApiKey
      → generate embed script cho Project
      → invite Account khác = Member
```

### Auth (MVP vs mở rộng)

| Hạng mục | MVP | Sau MVP |
|----------|-----|---------|
| Register / login | Email + password | — |
| Bootstrap admin | **Bỏ** (không còn Admin toàn cục) | — |
| Social login | **Không** | Google, GitHub, Microsoft Account |
| JWT claims | Account Id (không Role, không Tenant) | Có thể thêm provider claims nếu cần |

Khi thêm social login: thiết kế `ExternalLogin` (Provider + ProviderUserId) gắn Account; password có thể optional nếu chỉ OAuth — chi tiết khi làm mở rộng.

### Role

- **Không** dùng `Account.Role` toàn cục — bỏ / không dùng cho phân quyền nghiệp vụ.
- Quyền chỉ qua `TenantMembership.Role`.
- MVP: **Owner** | **Member**. Sau MVP có thể thêm Admin, Maintainer, Viewer, Writer, …

| Role | MVP |
|------|-----|
| **Owner** | Đúng 1 / Tenant. Quản trị Tenant, Project, ApiKey, rate-limit config; invite/remove; trash; transfer Owner; xử lý feedback. |
| **Member** | Xử lý feedback (list, status, comment). Không quản trị member / trash. |

Invite chỉ gán **Member** (hoặc role sau này ≠ tạo Owner thứ hai). Đổi Owner = **transfer** tường minh.

### Tenant context

- JWT **không** chứa tenant đang làm việc.
- Header bắt buộc khi thao tác trong Tenant: **`X-Tenant-Id`**.
- Middleware/service: verify membership; filter mọi query theo Tenant.
- Account chưa thuộc Tenant nào / không gửi `X-Tenant-Id` hợp lệ → **response danh sách trống** (không coi là lỗi auth trừ mutation cần Tenant).

### Trash & purge

- Owner → move Tenant to trash (`TrashedAt`).
- API bình thường không trả dữ liệu Tenant đang trash (restore: chi tiết khi implement).
- Job sau **30 ngày**: xóa cứng Tenant + toàn bộ Project, Feedback, Attachment, Comment, Membership, ApiKey, file storage liên quan.

---

## 5. Module theo trách nhiệm

| Module | Vai trò MVP | Trạng thái |
|--------|-------------|------------|
| **Identity** | Account, JWT; register/login email; **bỏ** Role toàn cục + bootstrap admin | Có — chỉnh |
| **Tenancy** | Tenant (1 Owner), Membership, invite Member, trash/purge | Mới |
| **Projects** | Project + ApiKey + generate embed script | Mới |
| **Feedback** | CRUD, search/filter, status | Mới |
| **Attachments** | Upload ảnh + **size limit** | Bọc Files |
| **Comments** | Comment nội bộ | Mới |
| **RateLimit** | Theo ApiKey (Owner cấu hình) + theo IP | Mới |
| **Audit** | Status, invite, trash, … | Tái dùng AuditLogs |

---

## 6. Domain model

### Entities

| Entity | Trường chính |
|--------|--------------|
| **Account** | Id (UUID v7), Email, … — **không** Role toàn cục |
| **Tenant** | Id, Name, TrashedAt? |
| **TenantMembership** | Id, TenantId, AccountId, Role — unique 1 Owner / Tenant |
| **Project** | Id, TenantId, Name; ApiKey (entity hoặc field) |
| **Feedback** | Id, ProjectId, Title, Content, Status, Priority?, Category?, Reporter, MetadataJson, CreatedAt |
| **Attachment** | Id, FeedbackId, Url |
| **Comment** | Id, FeedbackId, Author, Message |

> Priority / Category values, field search, Comment.Author shape: **chốt khi tới phase triển khai tương ứng**.

### Status workflow

```text
NEW → TRIAGED → IN_PROGRESS → RESOLVED → CLOSED
NEW → REJECTED
```

Validation transition trong Domain.

---

## 7. API surface

### Public (ApiKey — embed script)

| Method | Endpoint | Mục đích |
|--------|----------|----------|
| POST | `/api/feedback` | Tạo feedback |
| POST | `/api/feedback/{id}/attachments` | Upload ảnh (size limit) |
| GET | `/api/feedback/{id}` | Poll trạng thái |

Rate limit: **per ApiKey** (quota do Owner cấu hình) + **per IP**.

### Dashboard (JWT + `X-Tenant-Id`)

| Method | Endpoint | Mục đích |
|--------|----------|----------|
| CRUD | `/api/tenants` | Tạo Tenant (→ Owner); trash |
| POST | `/api/tenants/{id}/transfer-owner` | Chuyển Owner (duy nhất) |
| POST | `/api/tenants/{id}/members` | Invite Member |
| DELETE | `/api/tenants/{id}/members/{accountId}` | Remove member |
| PATCH | `/api/projects/{id}/rate-limit` | Owner cấu hình limit ApiKey |
| CRUD | `/api/projects` | Project + generate script metadata/endpoint |
| GET | `/api/feedback` | List + filter |
| GET | `/api/feedback/{id}` | Chi tiết |
| PATCH | `/api/feedback/{id}/status` | Workflow |
| POST | `/api/feedback/{id}/comments` | Comment |

**Scoping:** ApiKey → Project; Dashboard → `X-Tenant-Id` + membership.

---

## 8. Luồng chính

### 8.1 Onboarding

```text
POST /api/auth/register → Account
POST /api/tenants → Tenant + Membership(Owner)
POST /api/projects → Project + ApiKey + script config
POST /api/tenants/{id}/members → Member (Account tồn tại)
```

### 8.2 Embed gửi feedback

```text
POST /api/feedback (ApiKey)
  → rate limit (ApiKey + IP)
  → resolve Project
  → Feedback.Create (UUID v7)
  → optional attachment (size check) → storage
```

### 8.3 Dashboard

```text
Request + JWT + X-Tenant-Id
  → verify membership
  → nếu không có Tenant context hợp lệ → list rỗng
  → filter theo Tenant
```

### 8.4 Trash

```text
Owner → trash Tenant
  → TrashedAt = now
  → background: sau 30 ngày purge cascade + storage
```

---

## 9. Embed script

- Nằm trong **repo này** (không phải dashboard).
- Generate / phục vụ theo **Tenant + Project** (ApiKey, endpoint, config).
- Sản phẩm nhúng `<script>` trỏ tới bản generate của Project tương ứng.

Chi tiết format script: chốt khi triển khai phase Project/script.

---

## 10. Cấu trúc thư mục (phần mới)

```text
src/
├── FeedbackHub.Domain/
│   ├── Entities/     Tenant, TenantMembership, Project, Feedback, Attachment, Comment
│   ├── Enums/        TenantRole (Owner, Member, …), FeedbackStatus, …
│   └── Interfaces/
│
├── FeedbackHub.Application/
│   └── Services/
│       ├── Tenants/       # + membership, invite, trash, transfer owner
│       ├── Projects/      # + ApiKey, rate-limit config, script
│       ├── Feedback/
│       └── Comments/
│
├── FeedbackHub.Infrastructure/
│   ├── Persistence/
│   ├── Services/Storage/
│   └── Services/RateLimiting/
│
└── FeedbackHub.API/
    ├── Controllers/
    ├── Middleware/        # X-Tenant-Id resolution
    └── …                  # embed script endpoints / static generate
```

---

## 11. Công nghệ

| Thành phần | Công nghệ | Ghi chú |
|------------|-----------|---------|
| Runtime | ASP.NET Core (.NET 10) | Monolith |
| ORM | EF Core + PostgreSQL | UUID v7 |
| Mapping | Mapperly | |
| Auth | JWT + ApiKey | Tenant qua header, không qua JWT claim |
| Storage | S3 / R2 / MinIO | Size limit upload |
| Rate limit | ApiKey + IP | Redis nếu cần — chốt khi implement |
| Logging | Serilog | |
| Docs | OpenAPI + Scalar | |
| Frontend | — | **Repo riêng** |

---

## 12. Quyết định đã chốt

| # | Quyết định | Chi tiết |
|---|------------|----------|
| 1 | Role | Không role toàn cục; chỉ `TenantRole` (Owner, Member; mở rộng sau) |
| 2 | Owner | Đúng **1 Owner** / Tenant; invite = Member; đổi Owner bằng transfer |
| 3 | Tenant context | `X-Tenant-Id`; JWT không mang tenant; chưa có Tenant → data trống |
| 4 | Trash | Soft-delete Tenant; **30 ngày** rồi purge toàn bộ dữ liệu |
| 5 | Id | UUID v7 |
| 6 | Rate limit | Per ApiKey (Owner cấu hình; role Admin sau nếu có) + per IP |
| 7 | Upload | Giới hạn kích thước ảnh |
| 8 | Enums / filter chi tiết | Chốt khi tới phase triển khai |
| 9 | Repo này | Backend + embed script generate theo Tenant/Project |
| 10 | Frontend | Repo riêng |
| 11 | Phase sau MVP | Social login (Google/GitHub/Microsoft), AI, tích hợp, pending invite, role mở rộng, … |
| 12 | ApiKey | Project-scoped |
| 13 | Microservices | Không |
| 14 | Auth MVP | Register/login email + password; **bỏ** bootstrap admin; social = mở rộng sau MVP |

---

## 13. Roadmap backend (MVP)

1. Identity: bỏ Role toàn cục + bootstrap admin; `POST /api/auth/register` (email); UUID v7
2. Tenant + Membership (1 Owner) + `X-Tenant-Id` middleware + empty data
3. Invite Member; transfer Owner; trash + job purge 30 ngày
4. Project + ApiKey + generate embed script
5. Rate limit ApiKey + IP; Owner cấu hình
6. Feedback public + attachment size limit
7. Feedback dashboard (list/filter/status/comments) — chi tiết filter khi làm phase
8. Audit hooks

**Sau MVP:** social login (Google, GitHub, Microsoft), pending invite, role mở rộng, AI, webhook, Slack/Jira, analytics, RBAC Project, …
