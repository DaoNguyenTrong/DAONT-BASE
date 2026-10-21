---
name: clean-architecture-review
description: Review code for Clean Architecture violations including Dependency Rule, coupling, circular dependencies, interface segregation, and SRP. Use when reviewing architecture, checking layer boundaries, or auditing code organization.
disable-model-invocation: true
---

# Clean Architecture Review

Review kiến trúc Clean Architecture theo 4 layers và phát hiện vi phạm.

## Layers & Dependency Rule

```
StarterKit.Domain (innermost) → StarterKit.Application → StarterKit.Infrastructure → StarterKit.API (outermost)
```

| Layer          | Cho phép import                     | CẤM import                       |
| -------------- | ----------------------------------- | -------------------------------- |
| Domain         | Không import project nào            | Application, Infrastructure, API |
| Application    | Domain                              | Infrastructure, API              |
| Infrastructure | Domain, Application                 | API                              |
| API            | Domain, Application, Infrastructure | -                                |

API chỉ được dùng Infrastructure trong `Program.cs` (đăng ký DI); mọi file khác của API chỉ gọi Application. `ArchitectureTests.cs` cưỡng chế điều này, chặt hơn `ProjectReference`.

Nguồn xác định của Dependency Rule là `ProjectReference` trong từng `.csproj`, không phải phân tích import. Compiler của .NET đã chặn tham chiếu project vòng tròn, nên kiểm tra vòng tròn chỉ cần ở mức class/namespace.

## Quy trình Review

### Phase 1: Dependency Rule và cấu trúc tổng thể

```
Task Progress:
- [ ] 1. Đọc ProjectReference của 4 .csproj và so với bảng layer
- [ ] 2. Dùng CodeGraph để tìm cặp class/namespace nghi vòng tròn
- [ ] 3. Dùng CodeGraph để xem module boundaries và call path giữa các layer
```

**1.1 Dependency Rule (xác định):**

Đọc các file sau và liệt kê mọi `<ProjectReference>`:

```
backend/src/StarterKit.Domain/StarterKit.Domain.csproj
backend/src/StarterKit.Application/StarterKit.Application.csproj
backend/src/StarterKit.Infrastructure/StarterKit.Infrastructure.csproj
backend/src/StarterKit.API/StarterKit.API.csproj
```

So sánh với bảng ở trên. Mọi tham chiếu nằm ngoài cột "Cho phép" là vi phạm CRITICAL.

**1.2 Vòng tròn ở mức class/namespace:**

Với mỗi cặp nghi ngờ (ví dụ hai service gọi nhau), dùng `codegraph_explore` với tên hai symbol để xem call path. CodeGraph không có truy vấn tìm chu trình tự động, nên cần chọn cặp ứng viên trước.

**1.3 Luồng giữa các layer:**

```
codegraph_explore("<tên class hoặc use case>")
```

Dùng kết quả để xác định module nào đang gọi xuyên layer, trước khi đi sâu vào từng file.

### Phase 2: Chi tiết với Serena

```
Task Progress:
- [ ] 1. Đánh giá Interface Segregation
- [ ] 2. Đánh giá Single Responsibility
```

**2.1 Interface Segregation:**

Dùng Serena `find_symbol` với pattern `I*` trong `backend/src/StarterKit.Application` và `backend/src/StarterKit.Infrastructure`, `depth=1` để liệt kê method của từng interface. Interface có hơn 5 method → cân nhắc tách.

**2.2 Single Responsibility:**

Dùng Serena `find_symbol` với pattern `*Service` trong `backend/src/StarterKit.Application` và `backend/src/StarterKit.Infrastructure`, `include_body=true`. Class có hơn 5 tham số constructor, hoặc hơn 300 dòng, → ghi nhận.

**2.3 Vì sao không kiểm tra `using` ở phase này:** `get_symbols_overview` của Serena không trả về câu lệnh `using`. Vi phạm import đã được Phase 1.1 bắt đầy đủ hơn qua `ProjectReference`, nên không cần kiểm tra lại.

## Violation Categories

| Category               | Severity | Mô tả                                  |
| ---------------------- | -------- | -------------------------------------- |
| 🔴 Dependency Rule     | CRITICAL | Layer trong import layer ngoài         |
| 🔴 Circular Dependency | CRITICAL | A → B → A                              |
| 🟡 Fat Interface       | HIGH     | Interface > 5 methods                  |
| 🟡 God Class           | HIGH     | Class > 300 LOC hoặc > 5 dependencies  |
| 🟢 Leaky Abstraction   | MEDIUM   | Implementation details trong interface |

Chi tiết mẫu vi phạm và cách sửa: `violation-patterns.md` cùng thư mục.

## Output Format

```markdown
# Clean Architecture Review - [Project Name]

## Summary

- Violations found: X
- Critical: X | High: X | Medium: X

## Layer Dependency Violations

| File | Line | Violation | Severity |
| ---- | ---- | --------- | -------- |
| ...  | ...  | ...       | ...      |

## Coupling Issues

[Details...]

## Interface Segregation Issues

[Details...]

## Recommendations

1. [Priority fixes...]
```

## Quick Commands

| Task             | Phần cần chạy                     |
| ---------------- | --------------------------------- |
| Full review      | Phase 1 + Phase 2                 |
| Layer check only | Phase 1.1                         |
| Coupling check   | Phase 1.2 + Phase 2.2             |
