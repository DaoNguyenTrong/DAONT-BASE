---
name: clean-architecture-review
description: Review code for Clean Architecture violations including Dependency Rule, coupling, circular dependencies, interface segregation, and SRP. Use when reviewing architecture, checking layer boundaries, or auditing code organization.
disable-model-invocation: true
---

# Clean Architecture Review

Review kiến trúc Clean Architecture theo 4 layers và phát hiện vi phạm.

## Layers & Dependency Rule

```
FeedbackHub.Domain (innermost) → FeedbackHub.Application → FeedbackHub.Infrastructure → FeedbackHub.API (outermost)
```

| Layer          | Cho phép import                     | CẤM import                       |
| -------------- | ----------------------------------- | -------------------------------- |
| Domain         | Không import project nào            | Application, Infrastructure, API |
| Application    | Domain                              | Infrastructure, API              |
| Infrastructure | Domain, Application                 | API                              |
| API            | Domain, Application, Infrastructure | -                                |

## Quy trình Review

### Phase 1: Tổng quan với GitNexus

```
Task Progress:
- [ ] 1. Kiểm tra index freshness
- [ ] 2. Xem clusters để hiểu phân vùng chức năng
- [ ] 3. Phát hiện layer violations qua file path
- [ ] 4. Phát hiện circular dependencies
```

**1.1 Kiểm tra index:**

```
Đọc resource: gitnexus://repo/FEEDBACK-HUB/context
```

> Nếu index stale → chạy `npx gitnexus analyze` trước khi tiếp tục.

**1.2 Xem clusters:**

```
gitnexus_query({query: "all clusters"})
```

**1.3 Kiểm tra layer violations qua file path:**

```cypher
-- Domain import Infrastructure/API (vi phạm)
MATCH (a:File)-[:CodeRelation {type: 'IMPORTS'}]->(b:File)
WHERE a.filePath CONTAINS '/FeedbackHub.Domain/'
  AND (b.filePath CONTAINS '/FeedbackHub.Infrastructure/'
    OR b.filePath CONTAINS '/FeedbackHub.API/')
RETURN a.filePath, b.filePath

-- Application import Infrastructure/API (vi phạm)
MATCH (a:File)-[:CodeRelation {type: 'IMPORTS'}]->(b:File)
WHERE a.filePath CONTAINS '/FeedbackHub.Application/'
  AND (b.filePath CONTAINS '/FeedbackHub.Infrastructure/'
    OR b.filePath CONTAINS '/FeedbackHub.API/')
RETURN a.filePath, b.filePath

-- Infrastructure import API (vi phạm)
MATCH (a:File)-[:CodeRelation {type: 'IMPORTS'}]->(b:File)
WHERE a.filePath CONTAINS '/FeedbackHub.Infrastructure/'
  AND b.filePath CONTAINS '/FeedbackHub.API/'
RETURN a.filePath, b.filePath
```

**1.4 Phát hiện circular dependencies:**

```cypher
MATCH (a)-[r:CodeRelation]->(b)-[s:CodeRelation]->(a)
WHERE r.type IN ['CALLS', 'IMPORTS']
  AND s.type IN ['CALLS', 'IMPORTS']
RETURN a.name, b.name, r.type, s.type
```

### Phase 2: Chi tiết với Serena

```
Task Progress:
- [ ] 1. Kiểm tra using statements trong Domain
- [ ] 2. Kiểm tra using statements trong Application
- [ ] 3. Kiểm tra using statements trong Infrastructure
- [ ] 4. Đánh giá Interface Segregation
- [ ] 5. Đánh giá Single Responsibility
```

**2.1 Domain layer violations:**

```
get_symbols_overview({path: "backend/src/FeedbackHub.Domain"})
# Tìm bất kỳ using FeedbackHub.Application, FeedbackHub.Infrastructure, FeedbackHub.API
```

**2.2 Application layer violations:**

```
get_symbols_overview({path: "backend/src/FeedbackHub.Application"})
# Tìm bất kỳ using FeedbackHub.Infrastructure, FeedbackHub.API
```

**2.3 Infrastructure layer violations:**

```
get_symbols_overview({path: "backend/src/FeedbackHub.Infrastructure"})
# Tìm bất kỳ using FeedbackHub.API
```

**2.4 Interface Segregation:**

```
find_symbol({name: "I*", path: "backend/src/FeedbackHub.Application", include_body: true})
# Interface > 5 methods → cân nhắc tách
```

**2.5 Single Responsibility:**

```
# Class có quá nhiều dependencies (> 5 constructor params)
find_symbol({name: "*Service", path: "backend/src/FeedbackHub.Application", include_body: true})
find_symbol({name: "*Service", path: "backend/src/FeedbackHub.Infrastructure", include_body: true})
```

## Violation Categories

| Category               | Severity | Mô tả                                  |
| ---------------------- | -------- | -------------------------------------- |
| 🔴 Dependency Rule     | CRITICAL | Layer trong import layer ngoài         |
| 🔴 Circular Dependency | CRITICAL | A → B → A                              |
| 🟡 Fat Interface       | HIGH     | Interface > 5 methods                  |
| 🟡 God Class           | HIGH     | Class > 300 LOC hoặc > 5 dependencies  |
| 🟢 Leaky Abstraction   | MEDIUM   | Implementation details trong interface |

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

| Task             | Command                           |
| ---------------- | --------------------------------- |
| Full review      | Chạy Phase 1 + Phase 2            |
| Layer check only | Phase 1.3 + 2.1 + 2.2 + 2.3      |
| Coupling check   | Phase 1.4 + 2.5                   |
