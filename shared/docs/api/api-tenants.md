# Tenants API Documentation

Quản lý các tổ chức (Tenants) trong hệ thống Feedback Hub. Mỗi tài khoản có thể tạo nhiều tổ chức và tham gia vào các tổ chức khác.

---

## Authentication

Tất cả các endpoint yêu cầu xác thực JWT Bearer token:

```
Authorization: Bearer <jwt_token>
```

hoặc thông qua cookie:

```
Cookie: access_token=<jwt_token>
```

**Yêu cầu bắt buộc:**
- `X-TimeZone` — IANA timezone ID (ví dụ: `Asia/Ho_Chi_Minh`)

**Tùy chọn:**
- `X-Tenant-Id` — UUID của tổ chức đang làm việc (không bắt buộc; nếu không có hoặc không hợp lệ, không có context Tenant)

---

## Base URL

```
https://api.example.com/api
```

---

## Endpoints

### 1. Tạo Tenant (Create)

**Endpoint:** `POST /tenants`

**Mô tả:** Tạo tổ chức mới. Người dùng hiện tại tự động trở thành chủ sở hữu (Owner) duy nhất của tổ chức.

**Yêu cầu:**

```bash
POST /api/tenants
Authorization: Bearer <jwt_token>
X-TimeZone: Asia/Ho_Chi_Minh
Content-Type: application/json

{
  "name": "Công ty ABC",
  "description": "Phòng bán hàng công ty ABC"
}
```

**Request Body:**

| Field | Type | Required | Constraint | Mô tả |
|-------|------|----------|-----------|-------|
| `name` | string | ✓ | Max 200 ký tự | Tên tổ chức |
| `description` | string | ✗ | Max 1000 ký tự | Mô tả tổ chức |

**Phản hồi thành công (201 Created):**

```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "name": "Công ty ABC",
  "description": "Phòng bán hàng công ty ABC",
  "role": "Owner",
  "createdAt": "2026-07-17T09:30:00Z",
  "updatedAt": null
}
```

**Response Fields:**

| Field | Type | Mô tả |
|-------|------|-------|
| `id` | UUID | ID tổ chức (UUID v7) |
| `name` | string | Tên tổ chức |
| `description` | string \| null | Mô tả tổ chức |
| `role` | enum | Vai trò người dùng trong tổ chức (`Owner`, `Member`) |
| `createdAt` | datetime | Thời gian tạo (UTC) |
| `updatedAt` | datetime \| null | Thời gian cập nhật cuối cùng (UTC) |

**Lỗi (400 Bad Request):**

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "Bad Request",
  "status": 400,
  "detail": "Tên tổ chức là bắt buộc.",
  "instance": "/api/tenants"
}
```

**Lỗi (401 Unauthorized):**

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "Unauthorized",
  "status": 401,
  "detail": "Người dùng xác thực là bắt buộc.",
  "instance": "/api/tenants"
}
```

---

### 2. Danh sách Tenant của người dùng (Get My Tenants)

**Endpoint:** `GET /tenants`

**Mô tả:** Lấy danh sách tất cả các tổ chức mà người dùng hiện tại là thành viên. Nếu người dùng không thuộc tổ chức nào, trả về danh sách rỗng (không phải lỗi 403).

**Yêu cầu:**

```bash
GET /api/tenants
Authorization: Bearer <jwt_token>
X-TimeZone: Asia/Ho_Chi_Minh
```

**Query Parameters:** Không có

**Phản hồi thành công (200 OK):**

```json
[
  {
    "id": "550e8400-e29b-41d4-a716-446655440000",
    "name": "Công ty ABC",
    "description": "Phòng bán hàng",
    "role": "Owner",
    "createdAt": "2026-07-17T09:30:00Z",
    "updatedAt": null
  },
  {
    "id": "660e8400-e29b-41d4-a716-446655440001",
    "name": "Công ty XYZ",
    "description": null,
    "role": "Member",
    "createdAt": "2026-07-17T10:00:00Z",
    "updatedAt": "2026-07-17T10:15:00Z"
  }
]
```

**Danh sách rỗng (200 OK):**

```json
[]
```

**Lỗi (401 Unauthorized):**

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "Unauthorized",
  "status": 401,
  "detail": "Người dùng xác thực là bắt buộc.",
  "instance": "/api/tenants"
}
```

---

### 3. Chi tiết Tenant (Get Tenant by ID)

**Endpoint:** `GET /tenants/{id}`

**Mô tả:** Lấy chi tiết một tổ chức cụ thể. Người dùng phải là thành viên của tổ chức, nếu không trả về 404 (không phân biệt giữa "không tồn tại" và "không có quyền truy cập").

**Yêu cầu:**

```bash
GET /api/tenants/550e8400-e29b-41d4-a716-446655440000
Authorization: Bearer <jwt_token>
X-TimeZone: Asia/Ho_Chi_Minh
```

**URL Parameters:**

| Parameter | Type | Mô tả |
|-----------|------|-------|
| `id` | UUID | ID tổ chức (UUID v7) |

**Phản hồi thành công (200 OK):**

```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "name": "Công ty ABC",
  "description": "Phòng bán hàng công ty ABC",
  "role": "Owner",
  "createdAt": "2026-07-17T09:30:00Z",
  "updatedAt": null
}
```

**Lỗi (404 Not Found):**

Trả về khi:
- Tổ chức không tồn tại, hoặc
- Người dùng không phải là thành viên của tổ chức

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.2",
  "title": "Not Found",
  "status": 404,
  "detail": "Tenant với id '550e8400-e29b-41d4-a716-446655440000' không tìm thấy.",
  "instance": "/api/tenants/550e8400-e29b-41d4-a716-446655440000"
}
```

**Lỗi (401 Unauthorized):**

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "Unauthorized",
  "status": 401,
  "detail": "Người dùng xác thực là bắt buộc.",
  "instance": "/api/tenants/550e8400-e29b-41d4-a716-446655440000"
}
```

---

### 4. Mời thành viên (Invite Member)

**Endpoint:** `POST /tenants/{id}/members`

**Mô tả:** Chủ sở hữu tổ chức mời một tài khoản hiện có vào tổ chức với vai trò Thành viên. Tài khoản mời phải là Owner của tổ chức, tài khoản được mời phải tồn tại và chưa phải là thành viên.

**Yêu cầu:**

```bash
POST /api/tenants/550e8400-e29b-41d4-a716-446655440000/members
Authorization: Bearer <jwt_token>
X-TimeZone: Asia/Ho_Chi_Minh
Content-Type: application/json

{
  "accountId": "770e8400-e29b-41d4-a716-446655440002"
}
```

**Request Body:**

| Field | Type | Required | Constraint | Mô tả |
|-------|------|----------|-----------|-------|
| `accountId` | UUID | ✓ | Valid UUID | ID tài khoản cần mời |

**Phản hồi thành công (201 Created):**

```json
{
  "tenantId": "550e8400-e29b-41d4-a716-446655440000",
  "accountId": "770e8400-e29b-41d4-a716-446655440002",
  "accountUsername": "john.doe",
  "role": "Member",
  "createdAt": "2026-07-17T10:30:00Z"
}
```

**Response Fields:**

| Field | Type | Mô tả |
|-------|------|-------|
| `tenantId` | UUID | ID tổ chức |
| `accountId` | UUID | ID tài khoản được mời |
| `accountUsername` | string | Username tài khoản được mời |
| `role` | enum | Vai trò (`Member`) |
| `createdAt` | datetime | Thời gian mời (UTC) |

**Lỗi (403 Forbidden):**

Trả về khi người dùng hiện tại không phải Owner của tổ chức:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.3",
  "title": "Forbidden",
  "status": 403,
  "detail": "Chỉ chủ sở hữu tổ chức có thể mời thành viên.",
  "instance": "/api/tenants/550e8400-e29b-41d4-a716-446655440000/members"
}
```

**Lỗi (404 Not Found):**

Trả về khi tổ chức hoặc tài khoản không tồn tại:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.2",
  "title": "Not Found",
  "status": 404,
  "detail": "Account với id '770e8400-e29b-41d4-a716-446655440002' không tìm thấy.",
  "instance": "/api/tenants/550e8400-e29b-41d4-a716-446655440000/members"
}
```

**Lỗi (409 Conflict):**

Trả về khi tài khoản đã là thành viên của tổ chức:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.9",
  "title": "Conflict",
  "status": 409,
  "detail": "Tài khoản đã là thành viên của tổ chức này.",
  "instance": "/api/tenants/550e8400-e29b-41d4-a716-446655440000/members"
}
```

---

### 5. Xóa thành viên (Remove Member)

**Endpoint:** `DELETE /tenants/{id}/members/{accountId}`

**Mô tả:** Chủ sở hữu tổ chức xóa một thành viên. Chủ sở hữu không thể xóa chính mình hoặc xóa Owner cuối cùng của tổ chức.

**Yêu cầu:**

```bash
DELETE /api/tenants/550e8400-e29b-41d4-a716-446655440000/members/770e8400-e29b-41d4-a716-446655440002
Authorization: Bearer <jwt_token>
X-TimeZone: Asia/Ho_Chi_Minh
```

**URL Parameters:**

| Parameter | Type | Mô tả |
|-----------|------|-------|
| `id` | UUID | ID tổ chức |
| `accountId` | UUID | ID tài khoản cần xóa |

**Phản hồi thành công (204 No Content):**

Không trả về body khi xóa thành công.

**Lỗi (403 Forbidden):**

Trả về khi người dùng hiện tại không phải Owner:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.3",
  "title": "Forbidden",
  "status": 403,
  "detail": "Chỉ chủ sở hữu tổ chức có thể xóa thành viên.",
  "instance": "/api/tenants/550e8400-e29b-41d4-a716-446655440000/members/770e8400-e29b-41d4-a716-446655440002"
}
```

**Lỗi (404 Not Found):**

Trả về khi tổ chức hoặc thành viên không tồn tại:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.2",
  "title": "Not Found",
  "status": 404,
  "detail": "Account với id '770e8400-e29b-41d4-a716-446655440002' không tìm thấy.",
  "instance": "/api/tenants/550e8400-e29b-41d4-a716-446655440000/members/770e8400-e29b-41d4-a716-446655440002"
}
```

**Lỗi (409 Conflict):**

Trả về khi cố gắng xóa chính mình hoặc xóa Owner cuối cùng:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.9",
  "title": "Conflict",
  "status": 409,
  "detail": "Không thể xóa chủ sở hữu cuối cùng.",
  "instance": "/api/tenants/550e8400-e29b-41d4-a716-446655440000/members/770e8400-e29b-41d4-a716-446655440002"
}
```

---

### 6. Chuyển quyền Owner (Transfer Ownership)

**Endpoint:** `POST /tenants/{id}/transfer-owner`

**Mô tả:** Chủ sở hữu chuyển quyền Owner sang một thành viên hiện có. Người chủ sở hữu hiện tại sẽ trở thành Thành viên bình thường. Giao dịch này là nguyên tử (atomic).

**Yêu cầu:**

```bash
POST /api/tenants/550e8400-e29b-41d4-a716-446655440000/transfer-owner
Authorization: Bearer <jwt_token>
X-TimeZone: Asia/Ho_Chi_Minh
Content-Type: application/json

{
  "newOwnerId": "770e8400-e29b-41d4-a716-446655440002"
}
```

**Request Body:**

| Field | Type | Required | Constraint | Mô tả |
|-------|------|----------|-----------|-------|
| `newOwnerId` | UUID | ✓ | Valid UUID | ID tài khoản nhận quyền Owner |

**Phản hồi thành công (200 OK):**

```json
{
  "tenantId": "550e8400-e29b-41d4-a716-446655440000",
  "accountId": "770e8400-e29b-41d4-a716-446655440002",
  "accountUsername": "jane.smith",
  "role": "Owner",
  "createdAt": "2026-07-17T09:15:00Z"
}
```

**Response Fields:**

| Field | Type | Mô tả |
|-------|------|-------|
| `tenantId` | UUID | ID tổ chức |
| `accountId` | UUID | ID tài khoản mới là Owner |
| `accountUsername` | string | Username tài khoản mới |
| `role` | enum | Vai trò (`Owner`) |
| `createdAt` | datetime | Thời gian tạo membership (UTC) |

**Lỗi (403 Forbidden):**

Trả về khi người dùng hiện tại không phải Owner:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.3",
  "title": "Forbidden",
  "status": 403,
  "detail": "Chỉ chủ sở hữu tổ chức có thể chuyển quyền.",
  "instance": "/api/tenants/550e8400-e29b-41d4-a716-446655440000/transfer-owner"
}
```

**Lỗi (404 Not Found):**

Trả về khi tổ chức hoặc tài khoản mới không tồn tại, hoặc tài khoản mới không phải thành viên:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.2",
  "title": "Not Found",
  "status": 404,
  "detail": "Account với id '770e8400-e29b-41d4-a716-446655440002' không tìm thấy.",
  "instance": "/api/tenants/550e8400-e29b-41d4-a716-446655440000/transfer-owner"
}
```

**Lỗi (409 Conflict):**

Trả về khi cố gắng chuyển quyền cho chính mình:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.9",
  "title": "Conflict",
  "status": 409,
  "detail": "Không thể chuyển quyền cho chính mình.",
  "instance": "/api/tenants/550e8400-e29b-41d4-a716-446655440000/transfer-owner"
}
```

---

### 7. Danh sách thành viên (List Members)

**Endpoint:** `GET /tenants/{id}/members`

**Mô tả:** Lấy danh sách tất cả thành viên của tổ chức với vai trò của họ. Nếu người dùng không phải thành viên của tổ chức, trả về danh sách rỗng (không phải lỗi 403).

**Yêu cầu:**

```bash
GET /api/tenants/550e8400-e29b-41d4-a716-446655440000/members
Authorization: Bearer <jwt_token>
X-TimeZone: Asia/Ho_Chi_Minh
```

**URL Parameters:**

| Parameter | Type | Mô tả |
|-----------|------|-------|
| `id` | UUID | ID tổ chức |

**Phản hồi thành công (200 OK):**

```json
[
  {
    "tenantId": "550e8400-e29b-41d4-a716-446655440000",
    "accountId": "660e8400-e29b-41d4-a716-446655440001",
    "accountUsername": "alice.johnson",
    "role": "Owner",
    "createdAt": "2026-07-17T09:15:00Z"
  },
  {
    "tenantId": "550e8400-e29b-41d4-a716-446655440000",
    "accountId": "770e8400-e29b-41d4-a716-446655440002",
    "accountUsername": "bob.williams",
    "role": "Member",
    "createdAt": "2026-07-17T10:30:00Z"
  }
]
```

**Response Fields:**

Mảng các đối tượng TenantMembership:

| Field | Type | Mô tả |
|-------|------|-------|
| `tenantId` | UUID | ID tổ chức |
| `accountId` | UUID | ID tài khoản |
| `accountUsername` | string | Username tài khoản |
| `role` | enum | Vai trò (`Owner`, `Member`) |
| `createdAt` | datetime | Thời gian tham gia (UTC) |

**Danh sách rỗng (200 OK):**

Trả về khi người dùng không phải thành viên:

```json
[]
```

---

## Data Types

### TenantRole Enum

```typescript
type TenantRole = 'Owner' | 'Member';
```

- **Owner**: Chủ sở hữu tổ chức. Một tổ chức chỉ có duy nhất 1 Owner.
- **Member**: Thành viên của tổ chức.

### UUID Format

Tất cả ID sử dụng UUID v7 (128-bit identifier):

```
550e8400-e29b-41d4-a716-446655440000
```

---

## Error Handling

Tất cả lỗi trả về JSON với định dạng Problem Details (RFC 9110):

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.X",
  "title": "Error Title",
  "status": 400,
  "detail": "Chi tiết lỗi bằng Tiếng Việt hoặc Tiếng Anh",
  "instance": "/api/tenants"
}
```

### HTTP Status Codes

| Code | Mô tả |
|------|-------|
| 200 | OK - Yêu cầu thành công |
| 201 | Created - Tài nguyên được tạo thành công |
| 204 | No Content - Yêu cầu thành công, không có nội dung trả về (xóa) |
| 400 | Bad Request - Dữ liệu không hợp lệ (validation error) |
| 401 | Unauthorized - Không xác thực hoặc token hết hạn |
| 403 | Forbidden - Không có quyền thực hiện thao tác (Owner only) |
| 404 | Not Found - Tài nguyên không tìm thấy hoặc không có quyền |
| 409 | Conflict - Xung đột dữ liệu (ví dụ: đã là thành viên, không thể xóa chính mình) |
| 500 | Internal Server Error - Lỗi server |

---

## Headers

### Required Headers

```
Authorization: Bearer <jwt_token>
X-TimeZone: Asia/Ho_Chi_Minh
Content-Type: application/json
```

### Optional Headers

```
X-Tenant-Id: 550e8400-e29b-41d4-a716-446655440000
```

---

## Examples

### Example 1: Tạo Tenant

```bash
curl -X POST https://api.example.com/api/tenants \
  -H "Authorization: Bearer eyJhbGciOiJIUzI1NiIs..." \
  -H "X-TimeZone: Asia/Ho_Chi_Minh" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Công ty ABC",
    "description": "Phòng bán hàng"
  }'
```

**Response:**

```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "name": "Công ty ABC",
  "description": "Phòng bán hàng",
  "role": "Owner",
  "createdAt": "2026-07-17T09:30:00Z",
  "updatedAt": null
}
```

### Example 2: Lấy danh sách Tenant

```bash
curl -X GET https://api.example.com/api/tenants \
  -H "Authorization: Bearer eyJhbGciOiJIUzI1NiIs..." \
  -H "X-TimeZone: Asia/Ho_Chi_Minh"
```

**Response:**

```json
[
  {
    "id": "550e8400-e29b-41d4-a716-446655440000",
    "name": "Công ty ABC",
    "description": "Phòng bán hàng",
    "role": "Owner",
    "createdAt": "2026-07-17T09:30:00Z",
    "updatedAt": null
  }
]
```

### Example 3: Lấy chi tiết Tenant

```bash
curl -X GET https://api.example.com/api/tenants/550e8400-e29b-41d4-a716-446655440000 \
  -H "Authorization: Bearer eyJhbGciOiJIUzI1NiIs..." \
  -H "X-TimeZone: Asia/Ho_Chi_Minh"
```

**Response:**

```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "name": "Công ty ABC",
  "description": "Phòng bán hàng",
  "role": "Owner",
  "createdAt": "2026-07-17T09:30:00Z",
  "updatedAt": null
}
```

### Example 4: Mời thành viên vào Tenant

```bash
curl -X POST https://api.example.com/api/tenants/550e8400-e29b-41d4-a716-446655440000/members \
  -H "Authorization: Bearer eyJhbGciOiJIUzI1NiIs..." \
  -H "X-TimeZone: Asia/Ho_Chi_Minh" \
  -H "Content-Type: application/json" \
  -d '{
    "accountId": "770e8400-e29b-41d4-a716-446655440002"
  }'
```

**Response:**

```json
{
  "tenantId": "550e8400-e29b-41d4-a716-446655440000",
  "accountId": "770e8400-e29b-41d4-a716-446655440002",
  "accountUsername": "john.doe",
  "role": "Member",
  "createdAt": "2026-07-17T10:30:00Z"
}
```

### Example 5: Xóa thành viên khỏi Tenant

```bash
curl -X DELETE https://api.example.com/api/tenants/550e8400-e29b-41d4-a716-446655440000/members/770e8400-e29b-41d4-a716-446655440002 \
  -H "Authorization: Bearer eyJhbGciOiJIUzI1NiIs..." \
  -H "X-TimeZone: Asia/Ho_Chi_Minh"
```

**Response:** 204 No Content (không có body)

### Example 6: Chuyển quyền Owner

```bash
curl -X POST https://api.example.com/api/tenants/550e8400-e29b-41d4-a716-446655440000/transfer-owner \
  -H "Authorization: Bearer eyJhbGciOiJIUzI1NiIs..." \
  -H "X-TimeZone: Asia/Ho_Chi_Minh" \
  -H "Content-Type: application/json" \
  -d '{
    "newOwnerId": "770e8400-e29b-41d4-a716-446655440002"
  }'
```

**Response:**

```json
{
  "tenantId": "550e8400-e29b-41d4-a716-446655440000",
  "accountId": "770e8400-e29b-41d4-a716-446655440002",
  "accountUsername": "jane.smith",
  "role": "Owner",
  "createdAt": "2026-07-17T09:15:00Z"
}
```

### Example 7: Lấy danh sách thành viên Tenant

```bash
curl -X GET https://api.example.com/api/tenants/550e8400-e29b-41d4-a716-446655440000/members \
  -H "Authorization: Bearer eyJhbGciOiJIUzI1NiIs..." \
  -H "X-TimeZone: Asia/Ho_Chi_Minh"
```

**Response:**

```json
[
  {
    "tenantId": "550e8400-e29b-41d4-a716-446655440000",
    "accountId": "660e8400-e29b-41d4-a716-446655440001",
    "accountUsername": "alice.johnson",
    "role": "Owner",
    "createdAt": "2026-07-17T09:15:00Z"
  },
  {
    "tenantId": "550e8400-e29b-41d4-a716-446655440000",
    "accountId": "770e8400-e29b-41d4-a716-446655440002",
    "accountUsername": "bob.williams",
    "role": "Member",
    "createdAt": "2026-07-17T10:30:00Z"
  }
]
```

---

## Implementation Notes

### Localization

Tất cả error messages được hỗ trợ hai ngôn ngữ:
- **Tiếng Việt** (mặc định): `X-Culture: vi` hoặc `Accept-Language: vi`
- **Tiếng Anh**: `X-Culture: en` hoặc `Accept-Language: en`

### Pagination

Phiên bản hiện tại (Phase 1) trả về danh sách không phân trang. Trong các phiên bản sau sẽ hỗ trợ pagination.

### Rate Limiting

Phase 1 không có rate limiting. Phase 4 sẽ triển khai rate limiting per API Key + per IP.

### X-Tenant-Id Header

Header này được sử dụng cho các endpoint liên quan đến Project (Phase 3+). Trong Phase 1, nó không ảnh hưởng đến Tenant endpoints.

---

## Completed Features

| Feature | Phase | Mô tả |
|---------|-------|-------|
| Invite Member | P2 | ✓ Mời tài khoản khác vào tổ chức (POST /tenants/{id}/members) |
| Remove Member | P2 | ✓ Xóa thành viên khỏi tổ chức (DELETE /tenants/{id}/members/{accountId}) |
| Transfer Ownership | P2 | ✓ Chuyển quyền Owner sang tài khoản khác (POST /tenants/{id}/transfer-owner) |
| List Members | P2 | ✓ Danh sách tất cả thành viên tổ chức (GET /tenants/{id}/members) |

---

## Future Features (Roadmap)

| Feature | Phase | Mô tả |
|---------|-------|-------|
| Trash/Purge | P2 | Xóa tổ chức (soft delete + purge sau 30 ngày) |
| Update Tenant | P3+ | Cập nhật tên/mô tả tổ chức |
| Delete Tenant | P3+ | Xóa tổ chức ngay lập tức (Owner only) |

---

## Contact

Nếu có thắc mắc hoặc báo cáo lỗi, vui lòng tạo issue trên GitHub.
