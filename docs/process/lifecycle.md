# Vòng đời phát triển phần mềm

Tài liệu này mô tả đường đi từ ý tưởng đến phát hành, ai làm từng giai đoạn, ai duyệt và khi nào xong. `AGENTS.md` giữ các quy tắc chung (commit, push và PR, công cụ, nơi ghi quyết định); chi tiết từng giai đoạn nằm ở đây.

## 1. Các giai đoạn

| # | Giai đoạn | Sản phẩm đầu ra | Người làm | Cổng duyệt | Xong khi |
|---|---|---|---|---|---|
| 0 | Ý tưởng và tầm nhìn | Persona (`docs/prd/00-personas.md`), tầm nhìn, danh sách module, thứ tự ưu tiên | Phiên chính (`superpowers:brainstorming`) cùng người dùng; persona do `product-manager` viết, `business-analyst` review (§ Persona) | Người dùng | Persona ở trạng thái *Approved*, danh mục module trong `docs/prd/README.md` được chốt |
| 1 | Yêu cầu | PRD của module trong `docs/prd/` | `product-manager`, review bởi `business-analyst` | Người dùng | PRD ở trạng thái *Approved* |
| 1b | Backlog và kế hoạch phát hành | `docs/backlog/PRDxx-<tên>.md`: bản giao `REL-n`, cỡ áo, phụ thuộc; work item trên Plane | `product-manager` cắt bản giao, `tech-lead` ghi cỡ áo, `business-analyst` review | Người dùng (PO) | Kế hoạch phát hành *Approved* (§ Kế hoạch phát hành) |
| 1c-UI | Thiết kế giao diện của `REL-1` | Bảng thiết kế của các story trong `REL-1`, gắn mã yêu cầu | `ui-designer` | Người dùng | Mọi FR của các story đó có màn hình mới hoặc mẫu tương tác mới đều có bảng; màn hình theo mẫu có sẵn chỉ ghi tên mẫu; bảng tối chỉ cho mẫu mới (`ui-designer.md` quy tắc 1); người dùng duyệt |
| 1c | Nền kiến trúc | Tài liệu tech v1.0 trong `docs/tech/`: phần chung, phần của `REL-1`; điểm các story của `REL-1` | `tech-lead` (`appsec` nếu có hạng 2) | Người dùng | Phần chung và `REL-1` *Approved* (§ Nền kiến trúc) |
| 2 | Refinement: thiết kế giao diện cho bản giao kế tiếp | Bảng thiết kế của các story trong `REL-n` | `ui-designer` | Người dùng | Như 1c-UI, cho `REL-n` |
| 3 | Refinement: tech cho bản giao kế tiếp | Tài liệu tech tăng phiên bản phụ, thêm mục của `REL-n` | `tech-lead` (`appsec` nếu chạm hạng 2) | Người dùng | `REL-n` *Approved*, mọi FR/AC của nó có mục thiết kế, story có điểm |
| 4 | Sprint planning | Mục tiêu sprint, story được cam kết, kế hoạch sprint trong `docs/superpowers/plans/` | Phiên chính (`superpowers:writing-plans`) | Người dùng | Người dùng duyệt kế hoạch: sprint được cam kết; đồng thời quyết định có cấp quyền push và PR không |
| 5 | Triển khai | Code, test, mục `docs/feature-map.md` | `implementer`, mỗi lần một task | Theo hạng (§ Hạng kiểm tra) | Mọi task của phase xong, `scripts/test-gate.sh` đạt |
| 6 | Review và merge | PR được merge vào `dev` | `pr-reviewer` (PR chỉ có tài liệu: `/git-accept`) | Tự động, trừ vùng cần người dùng | PR vào `dev`; sau PR cuối của module, phiên chính chạy retro module (§ Lỗi lặp và retro cuối module) |
| 6b | Sprint review | Demo từng story theo AC | Phiên chính chuẩn bị | Người dùng nghiệm thu | Mỗi story của sprint được đạt hoặc ghi lý do chưa đạt |
| 6c | Sprint retro | Số đo sprint, ý cải tiến | Phiên chính | — | Bảng *Sprint* trong `progress.md` có dòng của sprint |
| 7 | Phát hành | Bản phát hành, changelog; thường sau một hoặc vài bản giao | Skill `release-kit:git-release` | Người dùng | Tag và merge vào `main` |
| 8 | Vận hành và phản hồi | Phản hồi từ sản phẩm dùng DAONT-BASE | Người dùng | — | Phản hồi vào backlog theo cỡ việc (§ Cỡ công việc) |

Thứ tự khởi động module: 1 → 1b → 1c-UI → 1c → sprint đầu tiên. Các giai đoạn 2–6c lặp lại theo sprint (§ Vòng sprint).

Phiên chính là người điều phối: gửi mọi câu hỏi cho `solution-architect` trả lời thay người dùng, chỉ hỏi người dùng những câu agent đó chuyển lên, chuyển câu trả lời cho agent, gọi agent theo thứ tự, và không tự làm thay công việc của agent. Subagent không hỏi được người dùng và không gọi được agent khác.

Để bớt lượt gọi, phiên chính gom câu hỏi của một giai đoạn vào một lượt gọi `solution-architect`. Câu hỏi thuần kỹ thuật đã có mặc định hợp lý thì phiên chính tự quyết và ghi lại trong kế hoạch, không gửi đi.

## 1b. Persona

Persona là chân dung một nhóm người dùng thật, dựng theo mục tiêu và hành vi, không theo tuổi hay chức danh. Persona khác vai trò: persona nói ai dùng và vì sao, vai trò (PRD-00 §Vai trò) nói ai được làm gì. Một sản phẩm mới không bắt đầu PRD nào khi `docs/prd/00-personas.md` chưa *Approved*; `scripts/check-personas.ts` chặn điều này trong `scripts/test-gate.sh` và CI.

**Cách dựng.** Phiên chính điều phối; tài liệu viết từ `docs/prd/_template-persona.md`, nằm trên nhánh tài liệu và đi qua `/git-accept` như PRD (§5).
1. **Thu bằng chứng.** Phiên chính phỏng vấn người dùng theo `superpowers:brainstorming`, hỏi về người thật và lần gần nhất họ gặp vấn đề. Nguồn theo thứ tự ưu tiên: phỏng vấn người dùng cuối, số liệu hoặc ticket, chuyên gia nghiệp vụ, hiểu biết của người dùng.
2. **Gom nhóm** theo mục tiêu và hành vi: hai người cùng chức danh nhưng khác mục tiêu là hai persona. Giữ 2–5 persona, chọn đúng một persona chính, và ít nhất một phản persona.
3. **Viết.** `product-manager` viết theo mẫu; mọi khẳng định gắn nhãn `[Người dùng]`, `[Dữ liệu]` hoặc `[Giả định]`. Chưa có nghiên cứu thì được viết proto-persona, nhưng mỗi `[Giả định]` có cách kiểm chứng ở §6 của tài liệu.
4. **Phân xử xung đột** giữa persona là quyết định nghiệp vụ: `solution-architect` chuyển lên người dùng, không tự quyết, và không tự dựng persona.
5. **Review.** `business-analyst` review theo checklist bên dưới, rồi người dùng duyệt; thứ tự này do `scripts/check-prd-approval.ts` kiểm như với PRD.

**Checklist duyệt:** mỗi persona có ít nhất 2 mục tiêu, 2 nỗi đau và 2 tình huống; không có hai persona trùng mục tiêu chính; đúng một persona chính và ít nhất một phản persona; không quá một nửa số dòng của một persona là `[Giả định]`; không mô tả tính năng hay giải pháp; thiết bị và ngôn ngữ đủ rõ để `ui-designer` chọn bố cục.

**Dùng ở hạ nguồn.** Mã persona đi vào chuỗi truy vết (§7):
- **PRD module:** §3 liệt kê persona dùng module và persona chính của module; mỗi user story ở §4 ghi mã `P-nn`.
- **Thiết kế:** mỗi bảng ghi persona chính, dùng để chọn thiết bị và mật độ thông tin.
- **Test e2e:** đặt tên kịch bản theo persona.
- **`solution-architect`:** dùng persona làm căn cứ khi trả lời thay người dùng.

**Thay đổi.** Sửa persona đã duyệt thì tăng phiên bản và đưa tài liệu về *In review* (§3); các PRD có story của persona bị sửa được xem lại. Phản hồi ở giai đoạn 8 là nguồn chính để cập nhật persona. Retro cuối module (§ Lỗi lặp và retro cuối module) kiểm tra có persona nào không còn story nào phục vụ.

## 1c. Kế hoạch phát hành

Sau khi PRD *Approved*, module có một tệp `docs/backlog/PRDxx-<tên>.md`, viết từ `docs/backlog/_template.md`: story map, bảng bản giao (`PRDxx-REL-n`), phần *Chưa xếp* và lịch sử thay đổi. `product-manager` viết story map và cắt bản giao, `tech-lead` ghi cỡ áo, `business-analyst` review, người dùng (PO) duyệt. Thứ tự review và duyệt trong lịch sử thay đổi theo cùng quy tắc với PRD (`docs/prd/README.md` § Quy trình, bước 4).

**Quy tắc bản giao:**
- `REL-1` là bộ khung đi xuyên suốt (walking skeleton): luồng chính của persona chính chạy hết từ đầu đến cuối, dù còn mỏng.
- Mỗi bản giao demo được cho người dùng thấy; không có bản giao chỉ gồm việc backend.
- Mỗi story Must nằm trong một bản giao. Nằm ở nhiều bản giao thì mỗi lần ghi tập AC giao lần đó, và các tập AC không trùng nhau.
- **Giao một phần story:** một story được chia qua nhiều bản giao hoặc nhiều sprint theo tập AC của nó, ghi dạng `PRDxx-US-03 (AC-07, AC-08)`. PRD không đổi. Story chỉ *Done* khi mọi AC của nó *Done*.
- Sửa kế hoạch phát hành (đổi thứ tự, dời story) tăng phiên bản của chính nó và người dùng duyệt lại; PRD **không** về *In review*. Thêm hay đổi hành vi thì vẫn phải sửa PRD (cỡ M).
- Thứ tự bản giao là quyết định nghiệp vụ: `solution-architect` đề xuất được nhưng phải chuyển lên người dùng.
- Khi kế hoạch phát hành đã *Approved*, mã `REL` không bao giờ được đánh số lại hay dùng lại: các kế hoạch sprint cũ còn tham chiếu nó, và bộ kiểm tra báo lỗi khi gặp mã `REL` không có.

**Ước lượng hai mức**, mỗi mức nằm đúng một nơi, không chép sang nơi kia:
- **Cỡ áo (S/M/L/XL) theo bản giao**, trong kế hoạch phát hành. `tech-lead` ghi ở giai đoạn 1b, khi chưa có kiến trúc, chỉ để dự báo thô. Cỡ áo không đổi trừ khi kế hoạch được sửa vì lý do khác; không có PR tài liệu nào chỉ để đổi cỡ áo.
- **Story point theo story hoặc phần story** (Fibonacci 1, 2, 3, 5, 8, 13). `tech-lead` đặt ở refinement, khi đã có mục tech (với `REL-1` là ở giai đoạn 1c); người dùng chốt ở sprint planning.
  - Nguồn sự thật là Plane; dự án không có Plane thì là bảng *Story* của `progress.md` (§ Tiến độ module).
  - Kế hoạch sprint ghi ảnh chụp số điểm lúc cam kết để tính velocity; ảnh chụp không được cập nhật về sau.
  - Đổi điểm trước khi story vào sprint không cần duyệt.
  - Story trên 8 điểm phải chia theo tập AC trước khi vào sprint.

**Kiểm tra tự động.** `scripts/check-release-plan.ts` chạy trong `scripts/test-gate.sh` và CI. Nó đối chiếu mỗi kế hoạch phát hành với PRD, và chặn kế hoạch sprint khi kế hoạch phát hành hoặc PRD chưa *Approved*, hoặc khi kế hoạch sprint nhắc một mã `REL` không có. Độ phủ story Must chỉ được kiểm khi cả kế hoạch phát hành lẫn PRD đã *Approved*. PRD được duyệt mà chưa có kế hoạch phát hành thì không lỗi: cổng nằm ở chỗ code bắt đầu.

## 1d. Nền kiến trúc

Tài liệu tech v1.0 của module gồm:
- **Phần chung cho cả module:** §2 Kiến trúc, §3 Mô hình dữ liệu, §5 Phân quyền, §7 Bảo mật, §12 Quyết định, để bản giao sau không phải đập đi làm lại mô hình dữ liệu hay quyền.
- **Phần của `REL-1`:** §4 API, §6 Luồng, §8 Giao diện, §11 Kiểm thử, chỉ cho các story của `REL-1`.

(Số mục theo `docs/tech/_template.md`.) Thiết kế giao diện của `REL-1` đi trước, vì `tech-lead` lấy nó làm đầu vào. Cột `REL` ở bảng truy vết của tài liệu ghi mục nào phục vụ bản giao nào. Tài liệu dạng ngắn (§ Độ dài tài liệu) vẫn chia theo cách này.

**Trạng thái tính theo bản giao, không theo cả tài liệu.** Bảng *Trạng thái theo bản giao* ở đầu tài liệu có một dòng cho phần *Chung* và một dòng cho mỗi bản giao (trạng thái, duyệt ở phiên bản nào):
- Mỗi bản giao sau thêm mục cho §4, §6, §8, §11 và tăng phiên bản phụ (1.1, 1.2…). Chỉ dòng của bản giao đó về *In review*; các phần đã duyệt giữ trạng thái, nên refinement cho `REL-n+1` không chặn story của `REL-n`.
- Sửa một mục đã duyệt của một bản giao đưa dòng của bản giao đó về *In review*.
- Sửa phần chung là thay đổi lớn: tăng phiên bản chính, **mọi** dòng về *In review*, các story đang làm dừng (§ Lệch thiết kế khi triển khai), và `appsec` review lại nếu chạm hạng 2.
- DoR và `pr-reviewer` đối chiếu story với phiên bản duyệt của phần chung và của đúng bản giao chứa story đó.
- `docs/tech/README.md` ghi trạng thái theo bản giao (ví dụ `Chung ✓, REL-1 ✓, REL-2 review`).

Module có hạng 2: `appsec` review phần chung trước khi vào sprint đầu tiên.

## 1e. Vòng sprint

Một sprint là một Cycle trên Plane, nửa tháng: `SPR-YYYY-MM-S1` từ ngày 1 đến 15, `SPR-YYYY-MM-S2` từ ngày 16 đến cuối tháng. Một sprint được chứa story của nhiều module, mỗi module một tệp kế hoạch sprint. Với `REL-1`, refinement chính là giai đoạn 1c-UI và 1c; từ `REL-2`, refinement cho sprint N+1 chạy trong sprint N, tức là chuẩn bị trước đúng một sprint.

1. **Refinement.** Cho các story dự kiến làm ở sprint sau, phiên chính gọi `ui-designer` rồi `tech-lead`, đúng cho các story đó. Story đạt DoR (§ Definition of Ready và Definition of Done) thì chuyển *Todo*.
2. **Sprint planning**, ngày đầu sprint.
   - Phiên chính đề xuất danh sách: các story *Todo* theo thứ tự backlog, cho đến khi tổng điểm chạm velocity, tức trung bình điểm *Done* của 3 sprint gần nhất. Chưa đủ 3 sprint thì người dùng tự đặt năng lực.
   - Người dùng chốt mục tiêu sprint (một câu) và danh sách. Phiên chính đưa story vào Cycle, ghi mục tiêu vào mô tả Cycle, và viết kế hoạch sprint bằng `superpowers:writing-plans` (§ Quyền push và mở PR).
   - Người dùng duyệt kế hoạch là sprint được cam kết.
3. **Triển khai và merge** như giai đoạn 5–6. Trạng thái Plane đi theo các mốc ở § Plane.
4. **Sprint review**, ngày cuối sprint.
   - Phiên chính lập kịch bản demo cho từng story từ AC của nó, kèm bằng chứng: e2e của `qa-frontend`, ảnh chụp từ skill `run` trên `dev`.
   - Người dùng nghiệm thu từng story: đạt thì *Done*; chưa đạt thì giữ lại và ghi lý do trong comment; yêu cầu mới phát sinh quay về PRD (cỡ M), không sửa ngay trong sprint.
   - Phiên chính hỏi quyền push và mở PR ghi kết quả sprint (§ Tiến độ module).
5. **Sprint retro**, ngay sau review (§ Lỗi lặp và retro cuối module). Kết thúc sprint trên Plane: `complete` Cycle và dời story dở dang sang Cycle kế tiếp (`transfer_workitems`), theo quyền ghi ở § Plane. Kết quả sprint được ghi vào `progress.md` bằng PR sau sprint (§ Tiến độ module).

## 1f. Definition of Ready và Definition of Done

Brief giao `implementer` và checklist của `pr-reviewer` dẫn chiếu mục này, không chép lại.

**DoR**, cho một story hoặc một phần story theo tập AC:
- thuộc PRD *Approved* và nằm trong một bản giao của kế hoạch phát hành *Approved*;
- có persona và AC;
- có bảng thiết kế đã duyệt, hoặc ghi rõ dùng mẫu có sẵn;
- có mục trong tài liệu tech đã duyệt (§ Nền kiến trúc);
- có story point, không quá 8;
- không có phụ thuộc nào đang chặn;
- không còn câu hỏi nghiệp vụ mở.

**DoD:**
- code đã merge vào `dev` qua cổng của hạng;
- `scripts/test-gate.sh` đạt;
- mỗi AC có test;
- có mục `docs/feature-map.md`;
- không còn lệch lớn so với thiết kế chưa xử lý;
- PO đã nghiệm thu ở sprint review.

## 1g. Plane

Repo là nguồn sự thật của nội dung; Plane là nơi team xem tiến độ.

**Cấu hình.** Mỗi sản phẩm dùng DAONT-BASE chép `.claude/plane.example.json` thành `.claude/plane.json` và điền: `baseUrl` (địa chỉ `https` của Plane), `workspaceSlug`, id và identifier của project, id của từng trạng thái, id của label tầng (`BE`, `FE`, `Mobile`) và label `bug`. Label bản giao `PRDxx-REL-n` được tạo khi cần, không liệt kê trong tệp. Không có `.claude/plane.json` thì bỏ qua mọi bước Plane; khi đó điểm và trạng thái story ghi ở bảng *Story* của `progress.md` (§ Tiến độ module). Bản thân DAONT-BASE không có tệp này.

**Cấu hình CI.** Đặt secret `PLANE_API_KEY` trên GitHub, hoặc biến CI/CD cùng tên trên GitLab. **Trên cả GitHub lẫn GitLab, ai push được một nhánh đều đọc được `PLANE_API_KEY`**:
- GitHub: mọi workflow trên mọi nhánh được push (ví dụ một tệp mới chạy `on: push`) đều đọc được secret của repo, không chỉ qua PR. Vì vậy chuyển sang `pull_request_target` thôi thì không đóng được lỗ này, và việc checkout commit base chỉ cố định script nào chạy khi workflow không bị sửa.
- GitLab: job MR chạy script và `plane.json` của nhánh nguồn. Biến phải *masked* và giới hạn *environment scope* là `plane` (job `plane:sync` khai báo environment đó), vì biến không giới hạn scope tới được mọi job. Biến không được đặt *protected*, vì pipeline MR của nhánh không được bảo vệ cần nó. Scope không chặn được người push được nhánh: họ vẫn viết được một job khai báo environment `plane` để đọc biến.

Quy tắc bắt buộc:
- Token là của một tài khoản bot quyền thấp, chỉ là thành viên của đúng project đó, và có ngày hết hạn nếu Plane cho phép.
- **Không bao giờ chạy pipeline của MR từ fork trong project cha** (GitLab): pipeline đó chạy tệp CI của fork với biến của project cha. Luật bỏ qua fork trong `.gitlab-ci.yml` không bảo vệ được, vì fork sửa được nó.
- Thay (rotate) `PLANE_API_KEY` khi một người mất quyền push (GitLab: quyền Developer trở lên), hoặc sau khi một pipeline của fork đã chạy trong project cha.
- PR từ fork trên GitHub không nhận được secret chỉ khi cài đặt repo gửi secret cho workflow của PR từ fork đang tắt (cài đặt này chỉ có ở repo private).

**Ánh xạ:**

| Repo | Plane |
|---|---|
| Module (PRD) | Module `[PRDxx] <tên tính năng>` (chia theo tính năng, không theo tầng) |
| Story `PRDxx-US-nn`, hoặc một phần theo tập AC | Work item `PRDxx-US-nn — <tiêu đề>`, `external_source = daont-base`, `external_id = PRDxx-US-nn` (giao một phần thì thêm `#AC-07,AC-08`) |
| Bản giao `REL-n` | Label `PRDxx-REL-n` |
| Tầng kỹ thuật | Label `BE` / `FE` / `Mobile` |
| Sprint | Cycle `SPR-YYYY-MM-Sn` |
| Điểm | Estimate point |
| Lỗi (cỡ S) | Work item gắn label `bug` |

Nếu bản Plane đang dùng không cho ghi điểm estimate qua MCP (spec `docs/superpowers/specs/2026-11-25-agile-planning-design.md` § 10), điểm được ghi bằng label `pt-1`, `pt-2`, `pt-3`, `pt-5`, `pt-8`, `pt-13`.

Mô tả work item chỉ có link tới mục PRD, bảng thiết kế và mục tech; AC không chép sang Plane. Link PR gắn vào work item bằng `workitem_link`. Mục `## Requirements` của mô tả PR có dòng `Plane: KNX-12, KNX-15`, liệt kê các work item mà PR phục vụ. PR không phục vụ work item nào (ví dụ PR quy trình ở retro, tooling, tài liệu) ghi `Plane: none (<lý do>)`; PR triển khai một story hoặc một item `bug` bắt buộc liệt kê id.

Commit phục vụ một story hoặc một `bug` kết thúc bằng trailer `Plane: KNX-12`; commit không phục vụ work item nào thì không cần. Tên nhánh không đổi.

**Mốc cập nhật trạng thái:**

| Mốc trong repo | Trạng thái Plane |
|---|---|
| PRD và kế hoạch phát hành *Approved* | Tạo work item ở *Backlog*, gắn Module và label (điểm gắn sau, ở refinement) |
| Story đạt DoR | *Todo* |
| `implementer` bắt đầu task của story | *In Progress* |
| PR của story được mở | *Ready For Testing* |
| PR merge vào `dev` | *Testing* |
| PO nghiệm thu ở sprint review và story đạt DoD | *Done* |
| Bị chặn bởi câu hỏi nghiệp vụ hoặc lệch lớn | *Pending*, kèm comment trỏ tới câu hỏi |
| Story bị bỏ khỏi kế hoạch phát hành | *Cancelled* |

**CI đồng bộ.** `scripts/plane-sync.ts` lấy id từ các dòng `Plane:` của mô tả PR và của commit message, với PR vào `dev`:
- PR được mở hoặc cập nhật, không phải draft: gắn link PR, chuyển *Ready For Testing*;
- PR merge: chuyển *Testing*;
- PR đóng mà không merge: item đang *Ready For Testing* chuyển về *In Progress*, item ở trạng thái khác giữ nguyên. GitLab không chạy pipeline cho trường hợp này, nên phiên chính làm tay.

CI không đổi trạng thái của item đang *Done*, *Cancelled* hoặc *Pending*, và lỗi Plane không làm hỏng PR. Phiên chính làm các mốc còn lại, và làm tay các mốc trên khi CI bị bỏ qua (PR từ fork, thiếu cấu hình) hoặc in cảnh báo.

**Giới hạn trên GitLab:**
- mô tả MR có thể bị cắt ngắn, và push vào `dev` chỉ đọc id từ commit message;
- push merge `main` vào `dev` (sau phát hành hoặc hotfix) cũng là push vào `dev`: mọi id trên các dòng `Plane:` của commit trong khoảng được push đều chuyển *Testing*, trừ item đang *Done*, *Cancelled* hoặc *Pending*;
- chỉ sửa mô tả MR thì không có pipeline nào chạy; id mới được đọc ở lần push kế tiếp;
- bỏ draft cũng không tự chạy pipeline, nên cần push thêm hoặc chạy lại pipeline MR. Bản GitLab không có biến `CI_MERGE_REQUEST_DRAFT` vẫn đồng bộ cả MR draft.

**Quyền ghi.** Ngoài job CI ở trên, chỉ phiên chính ghi lên Plane; subagent không gọi Plane.
- Không cần hỏi lại, đúng tại các mốc trên: tạo work item, đổi trạng thái, label, điểm, gắn link, viết comment.
- Cần người dùng xác nhận mỗi lần: tạo, `complete` hoặc sửa Cycle; đưa story vào hay rút khỏi Cycle (đó là cam kết sprint); xoá bất cứ thứ gì; sửa Module.

## 2. Cỡ công việc

Phiên chính chốt cỡ ở đầu mỗi việc và nói rõ cho người dùng; người dùng có thể đổi.

| Cỡ | Khi nào | Đường đi |
|---|---|---|
| **S** | Sửa lỗi, việc vặt, đổi nhỏ không thay đổi hành vi hay hợp đồng | Work item `bug` ở *Backlog* (§ Plane); vào sprint hiện tại nếu người dùng đồng ý, không thì sprint sau. Đường đi kỹ thuật: bỏ giai đoạn 0–4; lỗi thì test thất bại trước; `implementer`, rồi review theo hạng |
| **M** | Đổi hoặc thêm hành vi trong module đã có | Sửa PRD (tăng phiên bản, về *In review* rồi duyệt lại), cập nhật kế hoạch phát hành, refinement (thiết kế và tài liệu kỹ thuật chỉ phần bị ảnh hưởng), rồi vào backlog như story mới |
| **L** | Module mới, hoặc thay đổi chạm quyền, phiên, định danh | Đủ giai đoạn 0 → 1c, rồi vòng sprint; `appsec` nếu nhạy cảm |

Cỡ S không được thêm hành vi người dùng nhìn thấy hay đổi API. Nếu việc S hóa ra cần đổi hành vi, dừng và nâng lên M.

Lỗi phát hiện ở sprint review trên story của chính sprint đó không phải work item mới: story đó chưa *Done*.

### Hạng kiểm tra

Cỡ quyết định cần những tài liệu nào; hạng quyết định kiểm tra kỹ đến đâu. Hạng của một thay đổi là hạng cao nhất trong các tệp của nó, do `bun scripts/risk-tier.ts --base origin/dev` tính, không do ai tự đánh giá. Người làm được nâng hạng, không được hạ.

| Hạng | Thay đổi nào | Kiểm tra |
|---|---|---|
| **0** | Tài liệu, văn bản quy trình, locale, OpenAPI export và client sinh tự động, thay đổi chỉ trong test | Chỉ CI. Phiên chính tự xem diff, không gọi agent review |
| **1** | Mọi thay đổi còn lại: hành vi thường ở vùng không nhạy cảm (CRUD, màn hình, danh sách, bộ lọc) | `implementer` Sonnet, gộp task theo màn hình. Không review sau từng task; một lượt review Sonnet cho cả PR của phase (`pr-reviewer`). Không cài lỗi thử test, không Opus |
| **2** | Đăng nhập, token, cookie, phiên, quyền, đổi tổ chức và cơ chế lọc theo tổ chức, khoá API, khoá dữ liệu và đồng thời, EF migration, cấu hình CI và hook, chính script tính hạng | Đủ quy trình: `appsec` review tài liệu kỹ thuật trước kế hoạch, reviewer Opus sau từng task, khoảng 5 lỗi cài thử test. Review cuối phase chính là `pr-reviewer`, do phiên chính gọi với `model: opus`; không có lượt review cuối riêng. Người dùng merge (§3 Vùng cần người dùng) |

Danh sách đường dẫn và dấu hiệu của từng hạng chỉ nằm trong `scripts/risk-tier.ts`. Tài liệu kỹ thuật của một module mới nêu những đường dẫn hạng 2 mà module thêm vào, và PR đầu tiên của module thêm chúng vào script.

**Phiên chính khi chạy kế hoạch.** Mỗi lượt gọi tool của phiên chính đọc lại toàn bộ ngữ cảnh, nên model và độ dài phiên của nó là khoản tốn lớn nhất.
- Khi kế hoạch đã duyệt, phiên chính đề nghị người dùng chuyển sang `/model sonnet` để chạy. Opus để cho brainstorming, lập kế hoạch và review hạng 2.
- Mỗi phase chạy trong một phiên mới, tiếp tục từ ledger của kế hoạch (`.superpowers/sdd/<kế hoạch>/progress.md`).
- Người dùng quyết; phiên chính không tự đổi model.
- Hook `codegraph prompt-hook` của người dùng không chạy trong repo này (tệp đánh dấu `.claude/no-codegraph-prompt-hook`): agent tự gọi `codegraph_explore` khi cần, thay vì nhận ngữ cảnh mã ở mọi prompt.

## 3. Cổng giữa các giai đoạn

- **Không vào giai đoạn sau khi giai đoạn trước chưa duyệt, tính theo story:** story chỉ vào sprint khi đạt DoR (§ Definition of Ready và Definition of Done). PRD, kế hoạch phát hành và nền kiến trúc vẫn là cổng của cả module. Ngoại lệ: module nhạy cảm có thể viết tài liệu kỹ thuật song song với thiết kế giao diện khi người dùng cho phép và phạm vi ngoại lệ được ghi trong tài liệu module.
- **Câu hỏi nghiệp vụ phát sinh ở giai đoạn sau quay về PRD**, do `product-manager` sửa, không quyết ở nơi phát hiện. Câu hỏi kỹ thuật thuộc `tech-lead`.
- **Thay đổi sau khi đã duyệt:** sửa PRD đưa nó về *In review*, kéo theo xét lại thiết kế, tài liệu kỹ thuật và kế hoạch bị ảnh hưởng. Tài liệu bị ảnh hưởng cũng về *In review* và tăng phiên bản; riêng tài liệu kỹ thuật theo trạng thái từng bản giao (§ Nền kiến trúc). Ngoại lệ: điền hay sửa dòng liên kết và metadata (*Thiết kế*, *Kế hoạch*, *Cập nhật*) không tính là thay đổi.
- **Sau 3 lượt sửa PR vẫn chưa đạt:** `pr-reviewer` trả `ESCALATE`, chỉ ra tài liệu cần nghĩ lại (PRD, thiết kế hay kế hoạch). Không có lượt sửa thứ 4.
- **Vùng cần người dùng:** xác thực, token, cookie, phiên, quyền, đổi tổ chức, khoá API, định danh widget, `.claude/`, `.github/`, `AGENTS.md`, cấu hình CI và Lefthook, EF migration, cấu hình triển khai lưu trữ/Redis/SignalR, và thay đổi `.claude/decisions.md` sửa hay xoá mục cũ hoặc có dòng `Supersedes:`. `pr-reviewer` không tự merge các PR này.

## 4. Lệch thiết kế khi triển khai

Khi code không thể làm đúng tài liệu kỹ thuật đã duyệt:

| Mức | Khi nào | Xử lý |
|---|---|---|
| **Nhỏ** | Không đổi hợp đồng API, quyền, mô hình dữ liệu lưu trữ hay hành vi người dùng thấy (ví dụ tên class nội bộ, thêm index, tách service) | `implementer` làm tiếp, ghi vào mục *Lệch nhỏ so với thiết kế* của báo cáo và mô tả PR kèm lý do. `pr-reviewer` chặn nếu chỗ lệch không thật sự nhỏ hoặc không được ghi. Tài liệu kỹ thuật không sửa |
| **Lớn** | Đổi bất kỳ điều nào ở trên | `implementer` dừng task, trả chỗ lệch kèm bằng chứng và đề xuất. Phiên chính gọi `tech-lead` sửa tài liệu trên nhánh đang làm (tăng phiên bản, về *In review* theo § Nền kiến trúc); câu hỏi nghiệp vụ đi qua `product-manager` trước. Người dùng duyệt lại, rồi `implementer` làm tiếp. Bản sửa đi chung PR với code |

Thay đổi phần chung của tài liệu kỹ thuật dừng mọi story đang làm của module cho đến khi người dùng duyệt lại.

Không chắc thì coi là lớn. Để lệch lớn lộ ra sớm, kế hoạch đặt phần có rủi ro kỹ thuật cao thành task spike đầu tiên.

## 5. Commit tài liệu

- **Tài liệu khởi động của module** (PRD, kế hoạch phát hành, tài liệu tech v1.0 và link bảng thiết kế của `REL-1`) nằm chung một nhánh `chore/<module>-docs` cắt từ `dev` ngay từ bản nháp đầu, với **một PR nháp** cho cả module. Phiên chính commit mỗi phiên bản lên nhánh đó, để lịch sử các phiên bản và báo cáo review (comment trên PR) không mất giữa các phiên làm việc. Bắt đầu soạn PRD của module là lệnh push và mở PR nháp đó.
  - Mỗi lần người dùng duyệt một tài liệu, phiên chính commit bản duyệt lên nhánh. Link bảng thiết kế ghi ở dòng *Thiết kế* của PRD.
  - Các agent ở giai đoạn sau (`ui-designer`, `tech-lead`, `writing-plans`) đọc tài liệu từ nhánh này.
  - PR được merge **một lần** qua `/git-accept`, khi cả bốn đã duyệt. Nếu thiết kế hay tài liệu kỹ thuật bị chặn lâu, người dùng có thể cho merge riêng PRD đã duyệt.
- **Mỗi bản giao sau `REL-1`** có một nhánh `chore/<module>-rel-<n>-docs`: PR chỉ có tài liệu (tài liệu tech tăng phiên bản phụ, link thiết kế), merge qua `/git-accept`.
- **Sửa kế hoạch phát hành giữa module** đi bằng một PR tài liệu nhỏ.
- **Kế hoạch sprint** được phiên chính commit trên nhánh `feature/*` của nó, đi chung PR với code.
- **Mọi PR**, kể cả PR chỉ có tài liệu, có mục `## Changelog` theo định dạng của `release-kit:git-mr` (`- Added: …`, `- Fixed: …`, hoặc chỉ `none` cho thay đổi nội bộ), và luôn có một mục `## ` khác theo sau: `git-release` đọc mục này tới heading `## ` kế tiếp, nên dòng chữ tự do ở cuối (dòng ghi nguồn) sẽ làm nó hỏng. `git-release` dựng CHANGELOG của bản phát hành từ các mục này; không sửa `CHANGELOG.md` bằng tay.
- **Sửa tài liệu do lệch lớn** được phiên chính commit trên nhánh đang làm, đi chung PR với code; vì vậy `pr-reviewer` luôn đối chiếu code với đúng phiên bản tài liệu.
- `implementer` không sửa và không commit PRD, tài liệu kỹ thuật hay kế hoạch.

## 5b. Độ dài tài liệu

- **Kế hoạch** mô tả cho từng task: giao diện (chữ ký public, DTO, endpoint), danh sách tệp, ca kiểm thử và tiêu chí nghiệm thu. Chỉ chép đoạn mã ngắn khi chữ ký hoặc thuật toán khó tả bằng lời. Không viết sẵn mã hoàn chỉnh: `implementer` viết lại nó, và bản trong kế hoạch sẽ lệch khỏi mã thật.
- **PRD** của một module thường nhắm khoảng 500 dòng. Đây là giới hạn mềm: module lớn được vượt nếu ghi lý do ở đầu PRD. Không cắt phạm vi để giữ số dòng, vì cắt phạm vi là quyết định của người dùng.
- **Tài liệu kỹ thuật dạng ngắn** cho module ghi `không cần` ở cột *appsec* của `progress.md`. Bắt buộc có §1, §3, §4, §5, §11, §12 và §13 của mẫu. Các mục còn lại được để một dòng "Không áp dụng: <lý do>". Nhắm khoảng 200 dòng. Với các module này, `business-analyst` review PRD một lượt; lượt thứ hai chỉ khi lượt đầu có phát hiện `blocking`.

## 6. Quyền push và mở PR

Mặc định chỉ push hay mở PR khi người dùng ra lệnh. Ở giai đoạn 4, khi duyệt kế hoạch, người dùng có thể cấp **một lần cho cả kế hoạch**: mỗi khi một phase xong và `scripts/test-gate.sh` đạt, `implementer` được push nhánh của phase đó và mở một PR vào `dev`. Quyền này ghi trong đầu kế hoạch và trong brief giao cho `implementer`; không áp dụng cho `main`, và không thay cho cổng `pr-reviewer`.

**Kế hoạch sprint.** Kế hoạch của giai đoạn 4 là kế hoạch sprint, mỗi module một tệp `docs/superpowers/plans/YYYY-MM-DD-<module>-<SPR-…>.md`; tên tệp phải chứa mã sprint `SPR-YYYY-MM-S1` hoặc `-S2`. Đầu kế hoạch ghi:
- mục tiêu sprint và tên Cycle;
- các story và AC được cam kết, kèm điểm;
- các bản giao liên quan, ghi đủ mã `PRDxx-REL-n`;
- một dòng bắt đầu ngay đầu dòng bằng `` **Release plan:** `docs/backlog/<tệp>.md` ``, trỏ tới kế hoạch phát hành của module (`scripts/check-release-plan.ts` đọc dòng này).

**Mỗi phase một PR.** Mỗi phase của kế hoạch là một nhánh `feature/<module>-phase-<n>` và một PR vào `dev`. Một PR nhắm tối đa khoảng 1.500 dòng mã nguồn, không tính test và tệp sinh tự động (`shared/openapi/`, `frontend/src/api/generated/`); phase chạm vùng cần người dùng (§3) nên nhỏ hơn. Khi lập kế hoạch, phase ước lượng vượt mức này thì tách làm hai. Nếu vượt khi đang làm thì tách tại ranh giới task, không tách giữa task. Con số 1.500 được xem lại ở retro cuối module đầu tiên theo quy trình này.

**Xếp chồng một tầng.** Để triển khai không phải chờ review, nhánh phase N+1 được cắt từ nhánh phase N khi PR của phase N đã qua `pr-reviewer` (đạt, hoặc đang ở lượt sửa đầu tiên). Không xếp chồng quá một tầng, và không xếp chồng khi phase N đã qua hơn một lượt sửa. Khi phase N merge, nhánh N+1 rebase lên `origin/dev` rồi mới mở hoặc review PR, nên quy tắc một PR một lúc bên dưới vẫn giữ.

**Mỗi PR một mục đích.** Không gộp việc khác loại vào một PR, nhất là tài liệu đang chờ duyệt với thay đổi tooling hay cấu hình: PR gộp chỉ merge được khi phần chậm nhất xong, và phải đi qua cổng chặt nhất trong các phần của nó. Khi người dùng yêu cầu gộp, phiên chính nói rõ cái giá đó trước khi làm.

**Thứ tự merge — lịch sử gần thẳng.** Mỗi lúc chỉ một PR vào `dev` được review và merge. Trước khi review, nhánh phải được rebase lên `origin/dev` mới nhất (`git rebase origin/dev`, rồi `git push --force-with-lease`); merge bằng `gh pr merge --merge` để mỗi PR vẫn có một merge commit đánh dấu ranh giới. Merge xong PR này mới rebase nhánh kế tiếp. Nhánh chưa được rebase thì `pr-reviewer` và `/git-accept` trả *chưa sẵn sàng*, không tính là một lượt. Phiên chính điều phối thứ tự và yêu cầu rebase.

**Dọn nhánh sau khi merge.** Ngay sau khi merge, người merge (`pr-reviewer`, `/git-accept`, hoặc phiên chính khi người dùng cho merge) xoá nhánh trên remote, chỉ sau khi xác nhận nhánh đã nằm trong `origin/dev`: `git fetch --prune origin && git merge-base --is-ancestor origin/<branch> origin/dev && git push origin --delete <branch>`. Phiên chính đưa checkout về `dev` (`git switch dev && git pull --ff-only`) rồi xoá nhánh local bằng `git branch -d <branch>` — `-d`, không bao giờ `-D`, để git từ chối nếu nhánh còn commit chưa merge. Không xoá nhánh của PR chưa merge, bị đóng, hoặc đang chờ `ESCALATE`.

## 6b. Phát hành, ổn định và hotfix

Dùng skill `release-kit:git-release` (cấu hình ở `.claude/release-kit.json`; `CONTRIBUTING.md` § Release Process).

- **Cắt bản (Phase 1, trên `dev`):** skill cắt `release/vX.Y.Z` và soạn mục CHANGELOG ngay trên nhánh đó, không commit gì vào `dev`.
- **Ổn định:** lỗi tìm thấy khi QA được sửa trên nhánh `fix/*` cắt từ `release/vX.Y.Z` (`implementer`), PR vào chính nhánh release đó, rebase lên `origin/release/vX.Y.Z` trước khi review. `pr-reviewer` review như PR vào `dev` nhưng **không merge**: PASS được ghi lại và người dùng cho merge.
- **Phát hành (Phase 2):** skill chạy cổng bắt buộc, hỏi người dùng xác nhận, rồi tự mở và merge PR `release/vX.Y.Z → main` và gắn tag. Đó là cổng duy nhất của PR vào `main`.
- **Dọn nhánh `release/*` và `hotfix/*` sau khi merge vào `main`:** không dùng `gh pr merge --delete-branch`; người merge xoá nhánh trên remote chỉ sau khi xác nhận nhánh đã nằm trong `origin/main`: `git fetch --prune origin && git merge-base --is-ancestor origin/<branch> origin/main && git push origin --delete <branch>`, rồi xoá nhánh local bằng `git branch -d <branch>` — `-d`, không bao giờ `-D`.
- **Hotfix:** `implementer` sửa trên `hotfix/<tên>` cắt từ `main`; phiên chính chạy `code-reviewer` trên diff `origin/main...hotfix/<tên>`; rồi `git-release` lo CHANGELOG, cổng bắt buộc, PR vào `main` (người dùng xác nhận) và tag.
- **Đưa `main` về `dev` sau mỗi lần phát hành hoặc hotfix:** người dùng merge `main` vào `dev` bằng merge commit rồi push thẳng (`git checkout dev && git pull && git merge origin/main && git push`); `git-release` chỉ nhắc, không mở PR (`devWrites: "push"`). Đây là ngoại lệ của quy tắc rebase và của "không merge local": nó chính là một merge của `main`, và không được squash.
- **Phát hành nhanh (`quick`, `dev → main`, không có giai đoạn ổn định):** CHANGELOG được commit và push thẳng vào `dev` (`devWrites: "push"`), rồi skill mở PR vào `main`. Chỉ dùng khi bạn yêu cầu rõ `quick` và không cần QA riêng.
- **Cấu hình liên quan** trong `.claude/release-kit.json`: `devWrites: "push"`, `devMergeCommand: "/git-accept"`, `pushAfterRebase: "force-with-lease"` (`release-kit:git-sync` và `git-mr` tự push sau khi rebase, chỉ ghi đè đúng head cũ của nhánh), `prExtraSections` (các mục mô tả PR mà quy trình yêu cầu; `release-kit:git-mr` tạo sẵn).

## 7. Truy vết

Mã yêu cầu đi suốt chuỗi: persona (`P-nn`) → user story (`PRDxx-US-nn`) → yêu cầu (`PRDxx-FR-nn`) → bản giao (`PRDxx-REL-n`) → tiêu đề bảng thiết kế → mục tài liệu kỹ thuật → kế hoạch sprint → task → test → dòng `PRD:` của feature-map → mô tả PR (có dòng `Plane:`, § Plane). Một mã không có hạ nguồn, hoặc một thành phần không có mã thượng nguồn, là một phát hiện ở bất kỳ khâu review nào. Story Must không nằm trong bản giao nào, và PR thiếu dòng `Plane:` hoặc ghi dòng đó sai quy tắc (§ Plane) khi dự án có `.claude/plane.json`, cũng là phát hiện.

## 8. Lỗi lặp và retro cuối module

Cùng một góp ý xuất hiện hai lần thì không thêm quy tắc văn bản nữa: biến nó thành lint, kiểm thử kiến trúc hoặc generator (`AGENTS.md`).

**Sprint retro.** Sau mỗi sprint review (§ Vòng sprint):
- Phiên chính ghi: điểm cam kết so với điểm *Done*, số story dời sang sprint sau, số story bị từ chối khi review, thời gian story nằm ở *Pending*, và các commit `fix` của sprint. Phần đưa vào `progress.md` và PR mang nó: § Tiến độ module.
- Điều chỉnh ngay: năng lực và cách sắp xếp sprint kế tiếp.
- Ý cải tiến quy trình không áp dụng ngay: ghi vào mục *Chờ retro* của `docs/agent-error-log.md` (đóng băng quy trình bên dưới).

**Retro cuối module.** Khi PR cuối của một module đã merge vào `dev`, và trước khi đánh *Merged* trong `progress.md`, phiên chính chạy retro:
1. Phân loại các commit `fix` của module theo nguyên nhân gốc (một agent Sonnet đọc `git log` và `git show` của các nhánh module).
2. Ghi mỗi nhóm nguyên nhân vào `docs/agent-error-log.md`, và xử lý các ý trong mục *Chờ retro* của tệp đó.
3. Nhóm gặp từ hai lần trở lên thì biến thành kiểm tra tự động (việc cỡ S cho `implementer`), hoặc ghi lý do không làm được vào mục đó. Mục đã tự động hoá ghi "Đã chuyển thành: …".
4. Ghi số đo của module vào `progress.md` § Số đo, kèm bảng của `bun scripts/risk-tier.ts --fix-ratio <dải commit của module>` và tổng token agent cộng từ mục `## Verification` của các PR phase.
5. Gom các thay đổi quy trình của module vào một PR quy trình, kèm danh sách persona không còn story nào phục vụ (§ Persona), nếu có.
6. Nếu tỉ lệ `fix` của hạng 1 cao hơn của hạng 2, đưa đường dẫn của các commit `fix` hạng 1 vào danh sách hạng 2 của `scripts/risk-tier.ts`, trong PR quy trình ở bước 5.

**Đóng băng quy trình giữa module.** Thay đổi quy trình (tài liệu này, `AGENTS.md`, `.claude/agents/`, `.claude/rules/`) chỉ áp dụng ở retro. Giữa module chỉ sửa lỗi quy trình đang chặn việc, kèm lý do trong một mục của `docs/process/decisions.md`. Ý cải tiến phát sinh giữa module được ghi vào mục *Chờ retro* của `docs/agent-error-log.md`.

**Mỗi quy tắc một nơi.** Mỗi quy tắc có một tệp sở hữu; nơi khác chỉ trỏ tới nó bằng `tệp § mục`, không chép lại nội dung. `AGENTS.md` là mục lục và chỉ giữ những quy tắc mọi phiên cần ngay. Nếu tham chiếu `§` hỏng xuất hiện từ hai lần, thêm một script kiểm tra vào `scripts/test-gate.sh`.

## 9. Quyết định quy trình

Nhật ký quyết định quy trình nằm ở `docs/process/decisions.md`, mới nhất ở trên.

## 10. Tiến độ module

Bảng tiến độ nằm ở `docs/process/progress.md`; phiên chính là người duy nhất cập nhật nó.
- **Bảng module**, mỗi module một dòng: PRD, Kế hoạch phát hành, Nền kiến trúc, UI/Tech theo REL (bản giao mới nhất đã duyệt cả hai trên tổng số, ví dụ `REL-2/4`), appsec, REL xong (ví dụ `1/4`), Merged. Cập nhật trong PR tài liệu khi một tài liệu được duyệt, ghi *REL xong* ở PR sau sprint (bên dưới), và đánh *Merged* ở PR tài liệu tiếp theo của module, chỉ sau khi retro cuối module (§ Lỗi lặp và retro cuối module) xong.
- **Bảng *Story***, chỉ dùng khi dự án không có `.claude/plane.json`; khi đó nó là nguồn sự thật của điểm và trạng thái story. Mỗi story, hoặc phần story theo tập AC, một dòng: Story, REL, Điểm, Trạng thái, Cycle. Chỉ ghi ở mốc có commit để mang theo:
  - refinement: thêm dòng với điểm và *Todo*, trong PR tài liệu của bản giao đó (§ Commit tài liệu);
  - sprint planning: Cycle, commit cùng kế hoạch sprint;
  - sprint retro: *Done* nếu story đạt DoD, không thì giữ *Todo* (PR sau sprint bên dưới).

  Trạng thái ở giữa không ghi: các PR đã cho thấy.
- **Bảng *Sprint***, mỗi sprint một dòng: Cycle, mục tiêu, điểm cam kết, điểm *Done*, số story dời, số story bị từ chối ở review. Khi có Plane, số liệu lấy từ Cycle.
- **Bảng *Số đo***, mỗi module tám số: số ngày lịch (từ bản nháp PRD đầu tiên tới khi PR cuối merge), số PR, tỉ lệ commit `fix` (`git log`), tỉ lệ `fix` theo hạng (`bun scripts/risk-tier.ts --fix-ratio`), số câu hỏi tới người dùng, token agent, số sprint, và velocity trung bình.

**PR sau sprint.** Story chỉ *Done* sau khi PO nghiệm thu, lúc các PR tính năng đã merge. Vì vậy sau sprint review và retro, phiên chính ghi *REL xong*, dòng của bảng *Sprint* và bảng *Story* (khi dùng) trong một PR nhỏ chỉ có tài liệu, merge qua `/git-accept`. Quyền push và mở PR đó được hỏi người dùng ở sprint review.

**Token agent.** Báo cáo hoàn thành của mỗi agent có số token của agent đó. Cuối mỗi phase, phiên chính ghi tổng theo loại agent và theo hạng vào mục `## Verification` của PR phase, ví dụ `implementer h1: 210k; pr-reviewer h1: 45k`. Token của chính phiên chính không đo được nên không ghi.
