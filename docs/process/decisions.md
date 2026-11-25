# Quyết định quy trình

Nhật ký của DAONT-BASE, mới nhất ở trên. Quy tắc đang áp dụng nằm trong `lifecycle.md`; các quyết định từ dự án nguồn được giữ riêng ở `reference/feedback-hub-decisions.md` để tra lý do, không phải lịch sử thực hiện của DAONT-BASE.

### 2026-11-25 — Serena chạy ở chế độ REPL

Serena 2.1 có `agent_interface: REPL`: chỉ còn `initial_instructions` và `serena_repl`, agent gọi thao tác bằng Python (`s.lsp.find_symbol(...)`), nên tên tool kiểu cũ trong frontmatter của `implementer` và `tech-lead` không còn tồn tại. Chốt REPL trong `.serena/project.yml` để mọi máy dùng cùng giao diện mà tài liệu mô tả. Lý do: gộp nhiều truy vấn trong một lượt gọi và lọc kết quả bằng code trước khi đưa vào ngữ cảnh. Facade `shell` bị loại để lệnh shell vẫn qua hook `claude-bash-guard.ts`.

Cái giá: allowlist tool không còn giới hạn được từng thao tác, nên ràng buộc chỉ-đọc của `tech-lead` chỉ còn nằm trong prompt. Áp dụng ngoài retro vì là thay đổi tương thích công cụ, không đổi quy trình.

Phương án loại: giữ `agent_interface: tools` (vẫn chạy, nhưng bỏ chế độ mới người dùng muốn dùng).

### 2026-11-25 — Mẫu title và mô tả work item trên Plane

Người dùng yêu cầu item ngắn gọn, dễ hiểu và dùng tiếng Việt; quy tắc ở `lifecycle.md` § Plane. Mô tả story lặp lại một câu "Là…, tôi muốn…, để…" của PRD để đọc hiểu ngay trên Plane; AC vẫn không chép vì hay đổi và dễ lệch. Áp dụng ngoài retro vì chưa có module nào đang chạy.

### 2026-11-25 — CI đồng bộ Plane và trailer `Plane:` trong commit

Theo lệnh của người dùng, bổ sung vào PR #82. CI gắn link và chuyển trạng thái theo PR, và commit phục vụ work item mang trailer `Plane:`. Lý do: cập nhật tay dễ sót, và push vào `dev` trên GitLab chỉ có commit message để lấy id. Quy tắc nằm ở `lifecycle.md` § Plane.

### 2026-11-25 — Ba chỗ sửa sau review PR #81: bảng *Story*, PR sau sprint, `Plane: none`

Theo lệnh của người dùng; áp dụng ngoài retro vì chưa có module nào đang chạy. Quy tắc nằm ở `lifecycle.md` § Tiến độ module và § Plane.
- Không có Plane thì điểm và trạng thái story ghi ở bảng *Story* mới của `progress.md`. Lý do: bảng *Sprint* không chứa được, và kế hoạch sprint chưa có lúc DoR đã cần điểm.
- *REL xong*, bảng *Sprint* và bảng *Story* sau sprint đi bằng một PR tài liệu nhỏ qua `/git-accept`. Lý do: story chỉ *Done* sau khi các PR tính năng đã merge.
- PR không phục vụ work item nào ghi `Plane: none (<lý do>)`. Lý do: yêu cầu id chặn cả PR quy trình, tooling và tài liệu.

### 2026-11-25 — Lập kế hoạch Agile sau PRD: kế hoạch phát hành, vòng sprint, Plane

Người dùng chọn phương án B: PRD giữ ở mức module, thêm kế hoạch phát hành và nền kiến trúc, rồi thiết kế, tài liệu kỹ thuật, kế hoạch và triển khai đi theo bản giao và theo sprint, có DoR/DoD, demo, retro và Plane. Lý do: quy trình cũ chạy waterfall trong từng module, không có bước nào giao theo giá trị. Quy tắc nằm ở `lifecycle.md`; thiết kế và các đề xuất chưa được người dùng xác nhận riêng ở `docs/superpowers/specs/2026-11-25-agile-planning-design.md`. Áp dụng ngay, ngoài retro, vì chưa có module nào đang chạy (bảng trong `progress.md` trống).

Phương án loại:
- A, chỉ thêm release planning: UI và tech vẫn làm cho cả module từ đầu nên phản hồi chậm, không có nhịp sprint hay demo.
- C, Scrum đầy đủ: bỏ cổng theo module, phá cổng duyệt PRD, `check-prd-approval.ts` và chuỗi truy vết.

### 2026-11-12 — Bỏ PR reconcile: merge `main` vào `dev` trực tiếp

Theo lệnh của người dùng, `devWrites` đổi từ `"pr"` sang `"push"`. Sau phát hành hoặc hotfix, người dùng tự merge `main` vào `dev` bằng merge commit rồi push; `git-release` chỉ nhắc. CHANGELOG của `quick` được commit thẳng vào `dev`. Lý do: PR reconcile có diff rỗng (v1.6.5, PR #79), chỉ mang merge commit và tag về `dev`, mà vẫn tốn một lượt merge. Thay thế quyết định 2026-10-22 ở phần ghi vào `dev`; `devMergeCommand` và quy trình `/git-accept` cho PR thường không đổi. Không được squash merge này, vì lần phát hành sau sẽ dừng ở bước kiểm tra `main` là tổ tiên của `dev`.

Phương án loại: fast-forward `dev → main` (bỏ được commit chỉ có ở `main`, nhưng cần mở quyền push vào `main`, vốn yêu cầu PR và CI).

### 2026-11-12 — Persona là cổng bắt buộc của giai đoạn 0

Theo lệnh của người dùng, một sản phẩm mới phải có `docs/prd/00-personas.md` ở trạng thái *Approved* trước mọi PRD. Persona viết từ `docs/prd/_template-persona.md`, dựng và duyệt theo `lifecycle.md` § 1b; mỗi user story ghi mã `P-nn`. `scripts/check-personas.ts` chạy trong `scripts/test-gate.sh`, `test.yml` và `.gitlab-ci.yml` để cổng được ép bằng công cụ chứ không chỉ bằng văn bản. Áp dụng ngay vì chưa có module nào đang chạy.

Persona nằm ở tệp riêng thay vì một mục trong PRD-00, để có phiên bản và lượt duyệt riêng, và để sửa persona không kéo PRD-00 về *In review*.

Phương án loại: chỉ thêm mục *Người dùng* vào mẫu PRD (persona bị định nghĩa lại ở từng module, không truy vết được).

### 2026-11-12 — Thay toàn bộ quy trình bằng quy trình FEEDBACK-HUB

Theo lệnh trực tiếp của người dùng, DAONT-BASE dùng toàn bộ cơ chế làm việc hiện tại của FEEDBACK-HUB: vòng đời, agent, rule, skill, lớp Codex, guard lệnh, hạng rủi ro, cổng tài liệu và release. Đường dẫn công cụ được đổi sang StarterKit. Áp dụng ngay theo lệnh này, kể cả ngoài retro.

Chỉ chuyển cơ chế và mẫu; giữ lịch sử nền tảng hiện có, không nhập trạng thái Approved, số đo, yêu cầu sản phẩm hoặc canvas của FEEDBACK-HUB. Chưa có module, canvas và thông số vận hành mới được duyệt cho DAONT-BASE.

Phương án loại: chỉ chép `AGENTS.md` hoặc `.claude/` (thiếu script, generator và lớp Codex nên không thực thi được quy trình).
