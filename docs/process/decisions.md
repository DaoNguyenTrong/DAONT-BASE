# Quyết định quy trình

Nhật ký của DAONT-BASE, mới nhất ở trên. Quy tắc đang áp dụng nằm trong `lifecycle.md`; các quyết định từ dự án nguồn được giữ riêng ở `reference/feedback-hub-decisions.md` để tra lý do, không phải lịch sử thực hiện của DAONT-BASE.

### 2026-11-12 — Persona là cổng bắt buộc của giai đoạn 0

Theo lệnh của người dùng, một sản phẩm mới phải có `docs/prd/00-personas.md` ở trạng thái *Approved* trước mọi PRD. Persona viết từ `docs/prd/_template-persona.md`, dựng và duyệt theo `lifecycle.md` § 1b; mỗi user story ghi mã `P-nn`. `scripts/check-personas.ts` chạy trong `scripts/test-gate.sh`, `test.yml` và `.gitlab-ci.yml` để cổng được ép bằng công cụ chứ không chỉ bằng văn bản. Áp dụng ngay vì chưa có module nào đang chạy.

Persona nằm ở tệp riêng thay vì một mục trong PRD-00, để có phiên bản và lượt duyệt riêng, và để sửa persona không kéo PRD-00 về *In review*.

Phương án loại: chỉ thêm mục *Người dùng* vào mẫu PRD (persona bị định nghĩa lại ở từng module, không truy vết được).

### 2026-11-12 — Thay toàn bộ quy trình bằng quy trình FEEDBACK-HUB

Theo lệnh trực tiếp của người dùng, DAONT-BASE dùng toàn bộ cơ chế làm việc hiện tại của FEEDBACK-HUB: vòng đời, agent, rule, skill, lớp Codex, guard lệnh, hạng rủi ro, cổng tài liệu và release. Đường dẫn công cụ được đổi sang StarterKit. Áp dụng ngay theo lệnh này, kể cả ngoài retro.

Chỉ chuyển cơ chế và mẫu; giữ lịch sử nền tảng hiện có, không nhập trạng thái Approved, số đo, yêu cầu sản phẩm hoặc canvas của FEEDBACK-HUB. Chưa có module, canvas và thông số vận hành mới được duyệt cho DAONT-BASE.

Phương án loại: chỉ chép `AGENTS.md` hoặc `.claude/` (thiếu script, generator và lớp Codex nên không thực thi được quy trình).
