# Feedback Hub MVP Architecture

## 1. Mục tiêu

Xây dựng một nền tảng tập trung thu thập và quản lý feedback từ nhiều sản phẩm thông qua một widget JavaScript nhúng (`<script>`). Feedback được quản lý tập trung theo Tenant/Project và có workflow xử lý để các sản phẩm có thể biết trạng thái phản hồi.

**Phạm vi repo này:** Backend API + script embed (generate theo Tenant/Project). Frontend dashboard làm ở repo riêng.

## 2. Phạm vi MVP

### Có
- Đăng ký / đăng nhập Account bằng **email + password** (dashboard). Không bootstrap admin toàn cục.
- Tạo Tenant; trong Tenant tạo Project (+ ApiKey).
- Invite user vào Tenant (Member; mở rộng role sau).
- Script embed generate theo Tenant/Project.
- Gửi feedback.
- Đính kèm ảnh (có giới hạn kích thước).
- Workflow trạng thái.
- API lấy trạng thái feedback.
- Tìm kiếm và lọc (chi tiết field chốt khi tới phase).
- Rate limit theo ApiKey (Owner cấu hình) và theo IP.
- ID dùng UUID v7 (không đoán được).
- Soft-delete Tenant (trash) → sau 30 ngày xóa toàn bộ dữ liệu Tenant.

### Chưa có (triển khai sau khi hoàn thành MVP)
- Social login (Google, GitHub, Microsoft Account) — mở rộng Auth.
- AI phân loại.
- Jira/GitHub integration.
- Notification.
- Analytics nâng cao.
- Voting.
- Phân quyền theo từng Project.
- Invite email chưa có Account (pending invite).
- Role Tenant mở rộng (Admin, Maintainer, Viewer, Writer, …).

## 3. Mô hình tổ chức (Account / Tenant / Project)

**Đã chốt.**

```text
Register email + password → Account (JWT)
    → tạo Tenant (mình = Owner duy nhất)
        → tạo Project (+ ApiKey)
            → generate script embed cho Project
            → invite Account khác vào Tenant (Member; role khác thêm sau)
```

| Khái niệm | Ý nghĩa |
|-----------|---------|
| **Account** | Tài khoản dashboard (JWT). Không có role toàn cục; không bootstrap Admin hệ thống. Đăng ký email/password trong MVP; social login = mở rộng sau. Thuộc nhiều Tenant qua membership. |
| **Tenant** | Tổ chức — đơn vị cô lập dữ liệu cao nhất. **Đúng 1 Owner.** |
| **Project** | Sản phẩm trong Tenant. Widget/script auth bằng ApiKey của Project. |
| **TenantMembership** | Account ↔ Tenant + role (MVP: Owner \| Member; mở rộng sau). |
| **Reporter** | Người gửi feedback từ sản phẩm (metadata) — **không** phải Account. |

### Role trong Tenant

| Role (MVP) | Quyền |
|------------|--------|
| **Owner** | Duy nhất 1 người/Tenant. Quản lý Tenant, Project, ApiKey, rate-limit; invite/remove member; trash Tenant; xử lý feedback. Có thể chuyển Owner sang Account khác (transfer). |
| **Member** | Xem/xử lý feedback, comment, đổi status. Không quản trị member / không trash Tenant. |

Role bổ sung sau MVP (Admin, Maintainer, Viewer, Writer, …) — thiết kế membership để mở rộng enum/role mà không đổi mô hình.

### Tenant context (dashboard)

- JWT **không** mang tenant đang làm việc.
- Client gửi header **`X-Tenant-Id`**.
- API kiểm tra Account có `TenantMembership` với Tenant đó.
- User chưa có Tenant nào / không gửi header hợp lệ → **view data trống** (list rỗng), không lỗi quyền trừ khi thao tác yêu cầu Tenant.

### Invite (MVP)

1. Owner mời bằng email.
2. Email đã có Account → tạo `TenantMembership` với role **Member** (không tạo Owner thứ hai).
3. Email chưa có Account → từ chối (pending invite = sau MVP).

### Trash Tenant

- Owner đưa Tenant vào trash (soft-delete).
- Trong thời gian trash: dữ liệu không phục vụ API bình thường (có thể khôi phục — chi tiết khi implement).
- Sau **30 ngày**: xóa vĩnh viễn toàn bộ dữ liệu thuộc Tenant (Project, Feedback, Attachment, Comment, Membership, ApiKey, …).

## 4. Kiến trúc

```text
Product A/B/C
     │
     ▼
 Embed Script (generate theo Tenant/Project)
     │
 HTTPS + ApiKey
     ▼
 Feedback API (.NET)   ← repo này
     │
 ├── PostgreSQL (UUID v7)
 ├── Object Storage (S3/R2/MinIO)
 └── Rate limit (ApiKey + IP)

Dashboard (Vue)        ← repo riêng
     │
 REST API (JWT + X-Tenant-Id)
```

| Client | Auth | Việc chính |
|--------|------|------------|
| Embed script | ApiKey (Project) | Gửi feedback, upload ảnh, xem trạng thái |
| Dashboard | JWT + `X-Tenant-Id` | Tenant/Project, invite, list/filter, status, comment |

## 5. Thành phần

### Embed script (repo này)
- Generate theo Tenant/Project (ApiKey / config gắn Project).
- Popup feedback, upload ảnh (tuân giới hạn kích thước).
- Thu thập metadata: URL, Browser, Screen, Product version, User (Reporter nếu có).

### Feedback API
- POST /api/feedback
- GET /api/feedback
- GET /api/feedback/{id}
- PATCH /api/feedback/{id}/status
- Rate limit: theo ApiKey (Owner cấu hình) + theo IP.
- Upload ảnh: giới hạn kích thước (và loại file — chốt khi implement phase attachment).

### Dashboard (repo riêng)
- Đăng ký / đăng nhập **email + password** (social login = sau MVP)
- Quản lý Tenant / Project / members
- Cấu hình rate limit ApiKey
- Danh sách / chi tiết feedback, đổi status, comment, bộ lọc

## 6. Workflow

```
NEW → TRIAGED → IN_PROGRESS → RESOLVED → CLOSED
NEW → REJECTED
```

## 7. Database

Mọi Id: **UUID v7**.

### Account
- Id, Email, … (identity) — **không** có Role toàn cục

### Tenant
- Id, Name
- DeletedAt / TrashedAt (nullable) — trash + lịch 30 ngày purge

### TenantMembership
- Id, TenantId, AccountId, Role (Owner | Member | … sau)
- Constraint: mỗi Tenant đúng **một** membership Role = Owner

### Project
- Id, TenantId, Name, ApiKey (hoặc quan hệ ApiKey entity)

### Feedback
- Id, ProjectId, Title, Content, Status, Priority, Category, Reporter, MetadataJson, CreatedAt

### Attachment
- Id, FeedbackId, Url

### Comment
- Id, FeedbackId, Author, Message

> Priority, Category, field search/filter, Author = AccountId hay string: **chốt khi triển khai phase tương ứng**.

## 8. Metadata

```json
{
  "url": "/map",
  "browser": "Chrome",
  "os": "Windows",
  "screen": "1920x1080",
  "version": "1.2.0"
}
```

## 9. Công nghệ

| Thành phần | Công nghệ |
|------------|-----------|
| Backend + embed script | ASP.NET Core + JS script generate |
| Database | PostgreSQL (UUID v7) |
| Frontend dashboard | Vue 3 + Vite (**repo riêng**) |
| Storage | S3 / R2 / MinIO |
| Rate limit | Theo ApiKey + IP (Redis nếu cần phân tán — chốt khi implement) |

## 10. Roadmap

### MVP (repo này: API + script)
- Bỏ bootstrap admin; register/login **email + password**; bỏ role toàn cục
- Tenant membership (1 Owner, Member) + invite Account có sẵn
- `X-Tenant-Id` context; empty data khi chưa có Tenant
- Project + ApiKey + generate embed script
- Feedback + workflow + upload ảnh (size limit)
- Rate limit ApiKey + IP
- UUID v7; Tenant trash + purge 30 ngày

### Sau khi hoàn thành MVP
- Social login (Google, GitHub, Microsoft Account)
- AI phân loại, webhook, Slack/Jira, analytics
- Pending invite, role Tenant mở rộng, (optional) RBAC theo Project
- Voting, NPS, public roadmap, duplicate detection
