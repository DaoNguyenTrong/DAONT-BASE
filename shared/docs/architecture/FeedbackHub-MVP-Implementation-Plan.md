# Feedback Hub — MVP Implementation Plan

Kế hoạch triển khai MVP cho **repo này** (Backend API + embed script).  
Bám:

- [FeedbackHub-MVP-Architecture.md](./FeedbackHub-MVP-Architecture.md)
- [FeedbackHub-Backend-Architecture.md](./FeedbackHub-Backend-Architecture.md)

Dashboard Vue: **repo riêng** — không nằm trong plan này (chỉ cần API contract sẵn sàng để FE tích hợp).

---

## Mục tiêu MVP (Definition of Done)

MVP backend + script xong khi:

1. Account đăng ký/đăng nhập **email + password**; **không** còn role toàn cục; **không** bootstrap admin.
2. Tạo Tenant (1 Owner) → Project + ApiKey → generate embed script.
3. Invite Member (Account đã tồn tại); transfer Owner; trash Tenant + purge sau 30 ngày.
4. Dashboard client gọi API với JWT + `X-Tenant-Id`; chưa có Tenant → data trống.
5. Embed gửi feedback + upload ảnh (size limit); poll status; rate limit ApiKey + IP.
6. API dashboard: list/filter, đổi status (workflow), comment nội bộ.
7. Mọi Id = UUID v7.

**Ngoài MVP** (làm sau khi DoD trên xong): social login (Google, GitHub, Microsoft), pending invite, role Tenant mở rộng, AI, webhook, Slack/Jira, analytics, RBAC theo Project, voting, NPS.

---

## Sơ đồ phụ thuộc phase

```text
P0 Foundation ✅ DONE
 └─ P1 Tenancy core (Tenant, Membership, X-Tenant-Id)  ← next
      ├─ P2 Membership ops (invite, transfer, trash/purge)
      └─ P3 Project + ApiKey + embed script
           └─ P4 Rate limit (ApiKey + IP)
                └─ P5 Public Feedback + Attachments
                     └─ P6 Dashboard Feedback (list, status, comments)
                          └─ P7 Hardening & MVP cut
```

P2 và P3 có thể song song sau P1 nếu đủ người; P4 nên trước hoặc cùng P5 (public API cần rate limit).

---

## Phase 0 — Foundation

**Status: DONE** (2026-07-16)

**Mục tiêu:** Nền identity + Id strategy sẵn sàng cho tenancy.

### Việc làm
- **Bỏ** bootstrap admin (`BootstrapAdmin` endpoint, secret settings, luồng tạo Admin đầu tiên).
- Thêm **`POST /api/auth/register`** (email + password) → cấp JWT như login.
- Giữ login / refresh / revoke; JWT **không** chứa tenant đang làm việc; **không** claim Role toàn cục.
- Rà soát Account/Auth; **loại bỏ** `Account.Role` toàn cục cho phân quyền nghiệp vụ.
- Endpoint từng `[Authorize(Roles=Admin)]`: chuyển sang authenticated-only (`[Authorize]`) — quyền thật từ P1 qua TenantRole.
- Chuẩn hóa **UUID v7** qua `IdGenerator.NewUuidV7()`.
- Xác nhận Files/Storage, AuditLogs còn compile/wire dưới `[Authorize]`.
- **Không** implement social login trong phase này.

### Deliverables
- Register + login email/password; không bootstrap admin; không phụ thuộc global role.
- Convention UUID v7 trong code (`IdGenerator`).
- Migration `RemoveAccountRole` (drop `accounts.Role`).
- `AppDbContextFactory` cho `dotnet ef` design-time.

### Acceptance
- [x] `POST /api/auth/register` tạo Account + trả JWT.
- [x] Đăng nhập JWT OK; bootstrap-admin đã gỡ.
- [x] Không còn check quyền nghiệp vụ dựa trên `Account.Role`.
- [x] Entity mới dùng UUID v7.

### Verify (re-check 2026-07-16)
- Build: succeeded, 0 warnings / 0 errors.
- Tests: Domain 55 passed, Application 26 passed.
- Không còn `AccountRole`, `BootstrapAdmin*`, `IsAdmin`, `AuthSettings`, `Authorize(Roles=…)`, hay `ClaimTypes.Role` trong source (ngoài tên migration).

### Ghi chú
- Social login (Google / GitHub / Microsoft) = **sau MVP**.
- Controllers Accounts / ApiKeys / AuditLogs / SystemSettings tạm mở cho mọi user đã login đến P1.

---

## Phase 1 — Tenancy core

**Mục tiêu:** Tenant + membership + context header.

### Việc làm
- Domain: `Tenant`, `TenantMembership`, enum `TenantRole` (`Owner`, `Member`; thiết kế mở rộng sau).
- Constraint: **đúng 1 Owner** / Tenant.
- Tạo Tenant → tự gán caller = Owner.
- Middleware / filter: đọc **`X-Tenant-Id`**, verify membership.
- Không có Tenant / header không hợp lệ → **list rỗng** (không 403 trừ mutation bắt buộc Tenant).
- EF configuration + migration.
- API: CRUD Tenant cơ bản (create, get, list của user).

### Deliverables
- `POST/GET /api/tenants`
- Middleware `X-Tenant-Id`
- Membership persist

### Acceptance
- [ ] User tạo Tenant → là Owner duy nhất.
- [ ] Request có `X-Tenant-Id` đúng membership → scoped đúng Tenant.
- [ ] User chưa có Tenant → list data trống.
- [ ] Không thể tạo membership Owner thứ hai qua API thường.

---

## Phase 2 — Membership ops (invite, transfer, trash)

**Phụ thuộc:** P1

### Việc làm
- Invite bằng email: chỉ Account **đã tồn tại** → `TenantMembership(Member)`.
- Remove member (Owner only); không remove/hạ Owner cuối.
- **Transfer Owner** sang Account khác (Member hoặc Account trong Tenant — chốt rule chi tiết khi code).
- Soft-delete / trash Tenant (`TrashedAt`).
- Background job: sau **30 ngày** purge toàn bộ dữ liệu Tenant (Project, Feedback, files, membership, ApiKey, …).
- (Optional trong phase) restore từ trash trước hạn 30 ngày.

### Deliverables
- `POST /api/tenants/{id}/members`
- `DELETE /api/tenants/{id}/members/{accountId}`
- `POST /api/tenants/{id}/transfer-owner`
- Trash + purge job

### Acceptance
- [ ] Invite email chưa có Account → reject rõ ràng.
- [ ] Invite thành công → Member; vẫn đúng 1 Owner.
- [ ] Transfer Owner: đúng 1 Owner mới, Owner cũ thành Member (hoặc rule đã chốt).
- [ ] Trash ẩn Tenant khỏi API bình thường.
- [ ] Purge 30 ngày xóa cascade + storage liên quan.

---

## Phase 3 — Project + ApiKey + embed script

**Phụ thuộc:** P1 (P2 nên có trước khi production, có thể overlap)

### Việc làm
- Domain: `Project` thuộc Tenant.
- ApiKey **Project-scoped** (tái dùng entity `ApiKey` hiện có gắn Project, hoặc model tương đương — chốt khi implement).
- API CRUD Project trong scope `X-Tenant-Id` (Owner tạo/sửa/xóa; Member đọc — chốt chi tiết khi code).
- **Generate embed script** theo Tenant/Project (endpoint hoặc artifact phục vụ `<script>`).
- Document snippet tích hợp cho sản phẩm nhúng.

### Deliverables
- `/api/projects` CRUD
- ApiKey resolve → Project
- Script generate theo Project

### Acceptance
- [ ] Tạo Project trong Tenant → có ApiKey.
- [ ] Request ApiKey resolve đúng Project, không cross-tenant.
- [ ] Lấy được script/config embed cho Project.
- [ ] Member không làm được thao tác Owner-only (nếu đã phân).

### Chốt khi làm phase
- ApiKey: entity riêng vs field trên Project.
- Format chính xác của embed script.

---

## Phase 4 — Rate limiting

**Phụ thuộc:** P3 (cần ApiKey)

### Việc làm
- Rate limit **per ApiKey** — Owner cấu hình (quota/window).
- Rate limit **per IP** (default hệ thống).
- Áp dụng cho public feedback endpoints (và endpoint nhạy cảm khác nếu cần).
- Chọn store: memory / Redis — **chốt khi implement**.

### Deliverables
- Config rate limit trên Project/ApiKey (Owner API)
- Middleware/filter rate limit
- Response 429 khi vượt

### Acceptance
- [ ] Vượt hạn ApiKey → 429.
- [ ] Vượt hạn IP → 429.
- [ ] Owner cập nhật được cấu hình limit cho ApiKey của Project.

---

## Phase 5 — Public Feedback + Attachments

**Phụ thuộc:** P3, P4

### Việc làm
- Domain: `Feedback`, `Attachment`; status workflow cơ bản (`NEW` mặc định khi tạo).
- `POST /api/feedback` (ApiKey) + metadata JSON.
- `GET /api/feedback/{id}` (ApiKey) — payload tối thiểu an toàn (status-focused; chi tiết field khi implement).
- `POST /api/feedback/{id}/attachments` — upload ảnh, **giới hạn kích thước** (+ loại file chốt khi làm).
- Lưu storage qua Files hiện có.

### Deliverables
- Public create / get / upload
- Attachment URL liên kết Feedback

### Acceptance
- [ ] Widget/script tạo feedback được trong đúng Project.
- [ ] Upload vượt size → reject.
- [ ] Rate limit áp dụng (P4).
- [ ] Id UUID v7; không lộ cross-project.

### Chốt khi làm phase
- Exact response shape GET public.
- Max size / MIME types.
- Priority, Category có trên create public hay chỉ dashboard.

---

## Phase 6 — Dashboard Feedback (list, workflow, comments)

**Phụ thuộc:** P5 (và P1 context)

### Việc làm
- `GET /api/feedback` — pagination + filter/search (**field chốt khi làm phase**).
- `GET /api/feedback/{id}` — chi tiết + attachments.
- `PATCH /api/feedback/{id}/status` — transition Domain:
  - `NEW → TRIAGED → IN_PROGRESS → RESOLVED → CLOSED`
  - `NEW → REJECTED`
- `Comment` entity + `POST/GET …/comments` (JWT + Tenant).
- Scope: chỉ data thuộc Tenant/`X-Tenant-Id`; Project trong Tenant.
- Audit khi đổi status (và invite/trash nếu chưa gắn ở P2).

### Deliverables
- Dashboard feedback APIs
- Workflow validation
- Comments nội bộ

### Acceptance
- [ ] List/filter đúng Tenant; không leak.
- [ ] Transition hợp lệ OK; không hợp lệ → lỗi domain.
- [ ] Member xử lý feedback được; Owner-only vẫn giữ ở quản trị.
- [ ] Comment gắn đúng Feedback + Author (shape chốt khi làm).

### Chốt khi làm phase
- Filter/search fields.
- Priority / Category enum values.
- Comment.Author = AccountId hay display string.

---

## Phase 7 — Hardening & MVP cut

**Phụ thuộc:** P0–P6

### Việc làm
- OpenAPI/Scalar đủ cho FE repo + embed.
- Localization/message lỗi chính.
- Test trọng tâm: membership/Owner constraint, `X-Tenant-Id`, rate limit, workflow, upload size, trash/purge.
- CORS / cấu hình embed domain (chốt khi harden).
- Checklist DoD MVP (mục đầu file).
- Ghi chú API contract cho repo frontend.

### Acceptance
- [ ] Toàn bộ checkbox DoD MVP pass.
- [ ] `dotnet build` + test critical paths xanh.
- [ ] Tài liệu tích hợp ngắn (script + headers JWT/`X-Tenant-Id`/ApiKey).

---

## Ngoài scope plan này

| Hạng mục | Khi nào |
|----------|---------|
| Frontend dashboard (Vue) | Repo riêng, song song/sau khi API P1+ ổn |
| Social login (Google, GitHub, Microsoft) | **Sau MVP** — mở rộng Auth |
| Pending invite (email chưa có Account) | Sau MVP |
| Role Admin / Maintainer / Viewer / Writer | Sau MVP |
| AI, webhook, Slack/Jira, analytics | Sau MVP |
| RBAC theo Project | Sau MVP |
| Voting, NPS, public roadmap | Sau MVP |

---

## Thứ tự đề xuất khi làm một mình

| Tuần tự | Phase | Lý do |
|---------|-------|--------|
| ✅ | P0 | Sạch identity — **DONE** |
| 1 (next) | P1 | Mọi thứ sau phụ thuộc Tenant |
| 2 | P3 | Có Project/ApiKey/script sớm để thử embed |
| 3 | P2 | Invite/trash (cần trước khi multi-user thật) |
| 4 | P4 → P5 | Public path có bảo vệ |
| 5 | P6 | Dashboard API |
| 6 | P7 | Ship MVP |

Nếu ưu tiên demo một mình trước: **P0 → P1 → P3 → P5 (rate limit mặc định cứng) → P6**, rồi quay lại P2/P4 cấu hình đủ.

---

## Tham chiếu nhanh — quyết định đã chốt

| Chủ đề | Quyết định |
|--------|------------|
| Auth MVP | Register/login email + password; bỏ bootstrap admin |
| Social login | Sau MVP (Google, GitHub, Microsoft) |
| Role | Chỉ Tenant-scoped; MVP Owner + Member |
| Owner | Đúng 1 / Tenant; transfer tường minh |
| Context | `X-Tenant-Id`; JWT không mang tenant |
| Empty state | Chưa có Tenant → data trống |
| Trash | Soft-delete; purge 30 ngày |
| Id | UUID v7 |
| Rate limit | Per ApiKey (Owner config) + per IP |
| Upload | Size limit |
| Repo | API + embed script; FE riêng |
| Chi tiết enum/filter | Chốt trong phase tương ứng |
