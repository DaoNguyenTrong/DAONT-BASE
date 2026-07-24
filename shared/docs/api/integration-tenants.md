# Tenant Integration Guide

Hướng dẫn tích hợp Tenant API cho Frontend Dashboard.

---

## Overview

Hệ thống Feedback Hub sử dụng multi-tenant architecture. Mỗi tài khoản có thể quản lý nhiều tổ chức (Tenants), và mỗi Tenant có các thành viên với các vai trò khác nhau.

**Phase 1 (Hiện tại):** Chỉ hỗ trợ CRUD Tenant cơ bản + context header.

---

## Basic Flow

### 1. User registers → Login → Get JWT token

```javascript
const response = await fetch('/api/auth/login', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({
    username: 'user@example.com',
    password: 'password'
  })
});

const { accessToken } = await response.json();
// Save token to localStorage or sessionStorage
localStorage.setItem('access_token', accessToken);
```

### 2. Fetch user's tenants

```javascript
const headers = {
  'Authorization': `Bearer ${localStorage.getItem('access_token')}`,
  'X-TimeZone': 'Asia/Ho_Chi_Minh',  // Required
  'Content-Type': 'application/json'
};

const response = await fetch('/api/tenants', { headers });
const tenants = await response.json();

console.log(tenants);
// [
//   {
//     id: '550e8400-e29b-41d4-a716-446655440000',
//     name: 'Công ty ABC',
//     description: 'Phòng bán hàng',
//     role: 'Owner',
//     createdAt: '2026-07-17T09:30:00Z',
//     updatedAt: null
//   }
// ]
```

### 3. Create new tenant

```javascript
const response = await fetch('/api/tenants', {
  method: 'POST',
  headers,
  body: JSON.stringify({
    name: 'Công ty XYZ',
    description: 'Phòng marketing'
  })
});

const newTenant = await response.json();
console.log(newTenant.id);  // UUID của tổ chức mới
```

### 4. Switch tenant context (set X-Tenant-Id for future requests)

```javascript
const selectedTenantId = tenants[0].id;
headers['X-Tenant-Id'] = selectedTenantId;

// Lưu tenant context vào localStorage
localStorage.setItem('current_tenant_id', selectedTenantId);
```

---

## API Helper Class

### TypeScript/JavaScript

```typescript
class TenantService {
  private baseUrl = 'https://api.example.com/api';
  private token: string;
  private timezone: string = 'Asia/Ho_Chi_Minh';
  private currentTenantId?: string;

  constructor(accessToken: string) {
    this.token = accessToken;
  }

  private getHeaders(): HeadersInit {
    const headers: HeadersInit = {
      'Authorization': `Bearer ${this.token}`,
      'X-TimeZone': this.timezone,
      'Content-Type': 'application/json'
    };
    
    if (this.currentTenantId) {
      headers['X-Tenant-Id'] = this.currentTenantId;
    }
    
    return headers;
  }

  async getMyTenants(): Promise<Tenant[]> {
    const response = await fetch(`${this.baseUrl}/tenants`, {
      headers: this.getHeaders()
    });
    
    if (!response.ok) {
      throw new Error(`Failed to fetch tenants: ${response.statusText}`);
    }
    
    return response.json();
  }

  async getTenantById(id: string): Promise<Tenant> {
    const response = await fetch(`${this.baseUrl}/tenants/${id}`, {
      headers: this.getHeaders()
    });
    
    if (!response.ok) {
      throw new Error(`Failed to fetch tenant: ${response.statusText}`);
    }
    
    return response.json();
  }

  async createTenant(data: CreateTenantRequest): Promise<Tenant> {
    const response = await fetch(`${this.baseUrl}/tenants`, {
      method: 'POST',
      headers: this.getHeaders(),
      body: JSON.stringify(data)
    });
    
    if (!response.ok) {
      throw new Error(`Failed to create tenant: ${response.statusText}`);
    }
    
    return response.json();
  }

  setCurrentTenant(tenantId: string): void {
    this.currentTenantId = tenantId;
  }

  getCurrentTenant(): string | undefined {
    return this.currentTenantId;
  }
}

// Types
interface Tenant {
  id: string;
  name: string;
  description?: string;
  role: 'Owner' | 'Member';
  createdAt: string;
  updatedAt?: string;
}

interface CreateTenantRequest {
  name: string;
  description?: string;
}
```

### Usage

```typescript
// Initialize
const tenantService = new TenantService(accessToken);

// Get my tenants
const tenants = await tenantService.getMyTenants();

// Set active tenant
tenantService.setCurrentTenant(tenants[0].id);

// Get tenant details
const tenant = await tenantService.getTenantById(tenants[0].id);

// Create new tenant
const newTenant = await tenantService.createTenant({
  name: 'Công ty ABC',
  description: 'Phòng bán hàng'
});

console.log(newTenant.id);  // UUID của tổ chức mới
```

---

## React Integration Example

### Custom Hook

```typescript
// useTenant.ts
import { useState, useCallback, useEffect } from 'react';

export function useTenant(accessToken: string) {
  const [tenants, setTenants] = useState<Tenant[]>([]);
  const [currentTenantId, setCurrentTenantId] = useState<string>();
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string>();

  const tenantService = new TenantService(accessToken);

  useEffect(() => {
    // Load tenants on mount
    fetchTenants();
    
    // Load saved tenant from localStorage
    const saved = localStorage.getItem('current_tenant_id');
    if (saved) {
      setCurrentTenantId(saved);
      tenantService.setCurrentTenant(saved);
    }
  }, [accessToken]);

  const fetchTenants = useCallback(async () => {
    setLoading(true);
    setError(undefined);
    try {
      const data = await tenantService.getMyTenants();
      setTenants(data);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unknown error');
    } finally {
      setLoading(false);
    }
  }, []);

  const switchTenant = useCallback((tenantId: string) => {
    setCurrentTenantId(tenantId);
    tenantService.setCurrentTenant(tenantId);
    localStorage.setItem('current_tenant_id', tenantId);
  }, []);

  const createTenant = useCallback(async (data: CreateTenantRequest) => {
    setLoading(true);
    setError(undefined);
    try {
      const newTenant = await tenantService.createTenant(data);
      setTenants([...tenants, newTenant]);
      switchTenant(newTenant.id);
      return newTenant;
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Unknown error';
      setError(message);
      throw err;
    } finally {
      setLoading(false);
    }
  }, [tenants]);

  return {
    tenants,
    currentTenantId,
    loading,
    error,
    switchTenant,
    createTenant,
    fetchTenants
  };
}
```

### Component Usage

```tsx
function TenantSelector() {
  const { accessToken } = useAuth();
  const { tenants, currentTenantId, loading, switchTenant, createTenant } = 
    useTenant(accessToken);

  const handleCreateTenant = async () => {
    const name = prompt('Tên tổ chức:');
    if (!name) return;
    
    try {
      await createTenant({ name });
      alert('Tổ chức được tạo thành công!');
    } catch (error) {
      alert('Lỗi: ' + error.message);
    }
  };

  if (loading) return <p>Đang tải...</p>;

  return (
    <div>
      <select value={currentTenantId} onChange={(e) => switchTenant(e.target.value)}>
        <option value="">-- Chọn tổ chức --</option>
        {tenants.map(tenant => (
          <option key={tenant.id} value={tenant.id}>
            {tenant.name} ({tenant.role})
          </option>
        ))}
      </select>

      <button onClick={handleCreateTenant}>
        + Tạo tổ chức mới
      </button>

      {currentTenantId && tenants.find(t => t.id === currentTenantId) && (
        <div>
          <h2>Đang làm việc với: {tenants.find(t => t.id === currentTenantId)?.name}</h2>
        </div>
      )}
    </div>
  );
}
```

---

## Error Handling

### Common Errors

```typescript
try {
  const tenants = await tenantService.getMyTenants();
} catch (error) {
  if (error instanceof Error) {
    if (error.message.includes('401')) {
      // Token expired - redirect to login
      window.location.href = '/login';
    } else if (error.message.includes('404')) {
      // Tenant not found
      console.error('Tenant not found');
    } else if (error.message.includes('400')) {
      // Validation error
      console.error('Invalid request data');
    } else {
      // Other errors
      console.error('Unexpected error:', error.message);
    }
  }
}
```

---

## Headers Configuration

### Always Required

```javascript
const requiredHeaders = {
  'Authorization': `Bearer ${accessToken}`,
  'X-TimeZone': 'Asia/Ho_Chi_Minh',  // Must be valid IANA timezone
  'Content-Type': 'application/json'
};
```

### Optional (for Phase 3+)

```javascript
const optionalHeaders = {
  'X-Tenant-Id': currentTenantId,  // For project-scoped operations
  'X-Culture': 'en'  // for English error messages (default: 'vi')
};
```

---

## Testing

### Manual Testing with cURL

```bash
# 1. Login
curl -X POST https://api.example.com/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "username": "user@example.com",
    "password": "password"
  }'

# Save token from response

# 2. Get tenants
TOKEN="eyJhbGciOiJIUzI1NiIs..."
curl -X GET https://api.example.com/api/tenants \
  -H "Authorization: Bearer $TOKEN" \
  -H "X-TimeZone: Asia/Ho_Chi_Minh"

# 3. Create tenant
curl -X POST https://api.example.com/api/tenants \
  -H "Authorization: Bearer $TOKEN" \
  -H "X-TimeZone: Asia/Ho_Chi_Minh" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Công ty ABC",
    "description": "Phòng bán hàng"
  }'
```

---

## Best Practices

1. **Always send X-TimeZone header** — API bắt buộc require header này
2. **Cache tenant list** — Lưu danh sách tenant vào state để giảm API calls
3. **Handle empty list gracefully** — Nếu user chưa có tenant nào, show create button
4. **Persist tenant context** — Lưu `X-Tenant-Id` vào localStorage
5. **Refresh tenants after create** — Sau khi tạo tenant mới, gọi `getMyTenants()` để update list
6. **Show user's role** — Hiển thị vai trò (Owner/Member) để user biết quyền của mình

---

## Timezone Handling

IANA Timezone examples:

```
Asia/Ho_Chi_Minh
Asia/Bangkok
Asia/Singapore
America/New_York
Europe/London
Australia/Sydney
```

Lấy timezone của user:

```javascript
const timezone = Intl.DateTimeFormat().resolvedOptions().timeZone;
// Kết quả: "Asia/Ho_Chi_Minh"
```

---

## Next Steps (Roadmap)

- **Phase 2:** Invite members, transfer owner, trash/purge
- **Phase 3:** Projects, API Keys, Embed script generation
- **Phase 4:** Rate limiting
- **Phase 5:** Public feedback submission + attachments
- **Phase 6:** Dashboard feedback management, comments, workflow

---

## Support

Xem chi tiết tại `docs/api-tenants.md`
