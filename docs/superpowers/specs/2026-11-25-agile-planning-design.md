# Thiết kế: lập kế hoạch Agile sau PRD (backlog, bản giao, sprint, Plane)

| Mục | Giá trị |
|---|---|
| Trạng thái | Approved (người dùng duyệt 2026-11-25) |
| Cập nhật | 2026-11-25 |
| Phạm vi | Quy trình (`docs/process/`, `AGENTS.md`, `.claude/agents/`, mẫu tài liệu, script kiểm tra) |

## 1. Ý định

Team vận hành theo Agile, nhưng `docs/process/lifecycle.md` chạy waterfall trong từng module. PRD xong thì thiết kế UI cho cả module, rồi tài liệu kỹ thuật cho cả module, rồi một kế hoạch có các phase chia theo kích thước PR (khoảng 1.500 dòng), không chia theo giá trị giao. Từ sau PRD không có bước nào:

- sắp backlog theo giá trị và cắt module thành các bản giao demo được;
- ước lượng và đo năng lực, để cam kết một mục tiêu sprint;
- chạy theo nhịp sprint cố định, có demo cho PO nghiệm thu và retro sau mỗi sprint;
- định nghĩa Ready và Done cho từng story.

Team đã dùng Plane với sprint nửa tháng (`SPR-YYYY-MM-S1` từ ngày 1 đến 15, `S2` từ 16 đến cuối tháng). Quy trình mới phải khớp với cách dùng đó. Team đã đồng ý đổi Module trên Plane từ chia theo tầng (`[API-BACKEND]`, `[CMS]`…) sang chia theo tính năng.

## 2. Mục tiêu, không phải mục tiêu, phản mục tiêu

- **Mục tiêu**
  - Mỗi sprint giao được ít nhất một phần dùng được, demo được và được PO nghiệm thu.
  - Có backlog sắp thứ tự và ước lượng, để dự báo được ngày xong của mỗi bản giao.
  - Thiết kế UI và tài liệu kỹ thuật làm vừa đủ, vừa lúc cho bản giao kế tiếp, không làm cho cả module từ đầu.
  - Plane là nơi team xem tiến độ. Repo vẫn là nguồn sự thật của nội dung.
- **Không phải mục tiêu**
  - Thay cổng duyệt PRD theo module, hay bỏ chuỗi truy vết hiện có.
  - Đổi hạng kiểm tra, cổng `pr-reviewer`, quy tắc release.
  - Bắt mọi dự án dùng DAONT-BASE phải có Plane. Thiếu Plane thì quy trình vẫn chạy được.
- **Phản mục tiêu**, tức là coi như thất bại dù làm đủ các bước:
  - Nghi thức Agile trở thành giấy tờ: thêm tài liệu, thêm lượt duyệt mà thời gian ra bản giao đầu tiên không ngắn đi.
  - Nội dung (AC, quy tắc) bị chép sang Plane rồi lệch khỏi PRD.
  - Phần nền kiến trúc hay bảo mật bị cắt nhỏ theo sprint, đến bản giao sau mới phải đập đi làm lại mô hình dữ liệu hay quyền.

## 3. Ràng buộc

- Cổng PRD (`scripts/check-prd-approval.ts`), cổng persona (`scripts/check-personas.ts`), hạng rủi ro (`scripts/risk-tier.ts`) giữ nguyên.
- Đổi `AGENTS.md`, `.claude/`, cấu hình CI thuộc vùng cần người dùng merge (`lifecycle.md` § 3).
- Theo `lifecycle.md` § 8, quy trình bị đóng băng giữa module. Hiện chưa có module nào đang chạy (`progress.md` trống), nên được áp dụng ngay, giống quyết định persona ngày 2026-11-12. Lý do ghi vào `docs/process/decisions.md`.
- Subagent không gọi được Plane và không hỏi được người dùng. Chỉ phiên chính ghi lên Plane.
- Trên instance Plane hiện tại, các API estimate, work item type (Epic) và feature flags trả về 404. Thiết kế không dựa vào Epic, và phải kiểm tra lại estimate trước khi dùng (§ 10).

## 4. Phương án

| Phương án | Nội dung | Đánh đổi |
|---|---|---|
| A. Chỉ thêm release planning | Sau PRD, cắt story thành các bản giao; mỗi bản giao là một phase. UI và tech vẫn làm cho cả module từ đầu | Ít thay đổi, nhưng vẫn thiết kế lớn từ đầu nên phản hồi chậm, và không có nhịp sprint hay demo |
| **B. Giai đoạn 1b và vòng sprint (đã chọn)** | PRD giữ nguyên ở mức module. Thêm kế hoạch phát hành và phần nền kiến trúc. Sau đó UI, tech, kế hoạch và triển khai chạy theo bản giao và theo sprint, có DoR/DoD, demo, retro, Plane | Sửa nhiều tệp quy trình và agent, thêm một mẫu tài liệu và một script |
| C. Scrum đầy đủ | Bỏ cổng theo module, PRD thành backlog sống theo story | Phá cổng duyệt PRD, `check-prd-approval.ts` và chuỗi truy vết; không khuyên làm |

Người dùng chọn B (qua việc theo tiếp đề xuất B và chốt cách dùng Plane).

## 5. Thiết kế

### 5.1 Vòng đời mới

| # | Giai đoạn | Đầu ra | Người làm | Duyệt |
|---|---|---|---|---|
| 0 | Ý tưởng, persona | như cũ | như cũ | Người dùng |
| 1 | Yêu cầu | PRD của module, như cũ | `product-manager`, BA review | Người dùng |
| **1b** | **Backlog và kế hoạch phát hành** | `docs/backlog/PRDxx-<tên>.md`: các bản giao `REL-n` với cỡ áo, phụ thuộc; work item trên Plane | `product-manager` cắt bản giao, `tech-lead` ghi cỡ áo, BA review | Người dùng (PO) |
| **1c-UI** | **Thiết kế UI của `REL-1`** | Bảng thiết kế của các story trong `REL-1` | `ui-designer` | Người dùng |
| **1c** | **Nền kiến trúc** | Tài liệu tech v1.0: phần chung của cả module, cộng phần của `REL-1`, cộng điểm các story của `REL-1` | `tech-lead`, `appsec` nếu có hạng 2 | Người dùng |
| — | **Vòng sprint** (lặp lại, mỗi sprint một Cycle Plane) | | | |
| 2 | Refinement: thiết kế UI cho bản giao kế tiếp | Bảng thiết kế của các story trong `REL-n` | `ui-designer` | Người dùng |
| 3 | Refinement: tech cho bản giao kế tiếp | Tài liệu tech tăng phiên bản phụ, thêm mục của `REL-n` | `tech-lead` (`appsec` nếu chạm hạng 2) | Người dùng |
| 4 | Sprint planning | Mục tiêu sprint, story được cam kết, kế hoạch sprint trong `docs/superpowers/plans/` | Phiên chính (`writing-plans`) | Người dùng. Duyệt kế hoạch là cam kết sprint, và là chỗ cấp quyền push/PR như cũ |
| 5 | Triển khai | như cũ | `implementer` | Theo hạng |
| 6 | Review và merge | như cũ | `pr-reviewer` / `/git-accept` | như cũ |
| **6b** | **Sprint review** | Demo từng story theo AC; PO nghiệm thu | Phiên chính chuẩn bị, người dùng nghiệm thu | Người dùng |
| **6c** | **Sprint retro** | Số đo sprint, ý cải tiến | Phiên chính | — |
| 7 | Phát hành | như cũ, thường sau một hoặc vài bản giao | `release-kit:git-release` | Người dùng |
| 8 | Vận hành, phản hồi | Phản hồi đi vào backlog (§ 5.8) | Người dùng | — |

Thứ tự lúc khởi động module là 1 → 1b → 1c-UI → 1c → sprint đầu tiên. Thiết kế UI của `REL-1` đi trước nền kiến trúc vì `tech-lead` vẫn lấy thiết kế làm đầu vào. Với `REL-1`, refinement chính là 1c-UI cộng 1c. Từ `REL-2` trở đi, bước 2 và 3 cho sprint N+1 chạy trong lúc sprint N đang triển khai, tức là chuẩn bị trước một sprint. Cổng "không vào giai đoạn sau khi giai đoạn trước chưa duyệt" (`lifecycle.md` § 3) nay tính **theo story**. Một story chỉ được vào sprint khi đạt DoR (§ 5.5). PRD, kế hoạch phát hành và nền kiến trúc vẫn là cổng của cả module.

### 5.2 Kế hoạch phát hành (giai đoạn 1b)

**Tệp:** `docs/backlog/PRDxx-<tên>.md`, viết từ mẫu mới `docs/backlog/_template.md`. Phần đầu tệp có: Trạng thái (Draft / In review / Approved), Phiên bản, *Dựa trên PRD phiên bản*, Cập nhật.

**Nội dung:**
1. **Story map:** các hoạt động chính của persona chính theo thứ tự trải nghiệm (backbone), và các story `PRDxx-US-nn` xếp dưới từng hoạt động.
2. **Bảng bản giao.** Mỗi bản giao một dòng:

   | REL | Mục tiêu (kết quả demo được, nói theo người dùng) | Story (và tập AC, nếu chỉ giao một phần) | Persona | Phụ thuộc | Cỡ áo (S/M/L/XL) |
   |---|---|---|---|---|---|

3. **Chưa xếp:** các story Could chưa vào bản giao nào.
4. **Lịch sử thay đổi.**

**Quy tắc:**
- `REL-1` là bộ khung đi xuyên suốt (walking skeleton): luồng chính của persona chính chạy hết từ đầu đến cuối, dù còn mỏng.
- Mỗi story Must nằm trong một bản giao. Nếu nằm ở nhiều bản giao thì mỗi lần phải ghi tập AC giao lần đó, và các tập AC không được trùng nhau.
- Mỗi bản giao demo được cho người dùng thấy, nên không có bản giao nào chỉ gồm việc backend.
- **Giao một phần story:** một story được chia qua nhiều bản giao hoặc nhiều sprint theo tập AC của nó, ghi dạng `PRDxx-US-03 (AC-07, AC-08)`. PRD không đổi. Story chỉ *Done* khi mọi AC của nó *Done*.
- Sửa kế hoạch phát hành (đổi thứ tự, dời story) **không** đưa PRD về *In review*. Nó tăng phiên bản của chính kế hoạch, và người dùng duyệt lại kế hoạch. Thêm hay đổi hành vi thì vẫn phải sửa PRD (cỡ M).
- Thứ tự bản giao là quyết định nghiệp vụ. `solution-architect` đề xuất được, nhưng phải chuyển lên người dùng, không tự chốt.

**Kiểm tra tự động:** script mới `scripts/check-release-plan.ts` chạy trong `scripts/test-gate.sh` và CI. Nó báo lỗi khi:
- trong một tệp `docs/backlog/PRDxx-*.md` có tồn tại: một story Must của PRD không nằm trong bản giao nào, tập AC bị trùng, hoặc tệp nhắc một mã US hay AC không có trong PRD;
- một kế hoạch sprint nhắc một module chưa có kế hoạch phát hành *Approved*, hoặc nhắc một mã `REL` không có.

PRD được duyệt mà chưa có kế hoạch phát hành thì **không** lỗi. Nhờ vậy nhánh tài liệu không tự chặn mình trong khoảng giữa giai đoạn 1 và 1b. Cổng thật sự nằm ở chỗ code bắt đầu: không có kế hoạch sprint nào nếu chưa có kế hoạch phát hành đã duyệt. Hiện chưa có PRD nào *Approved*, nên không có dữ liệu cũ cần chuyển.

**Ước lượng có hai mức, mỗi mức nằm đúng một nơi:**
- **Cỡ áo theo bản giao**, ghi trong kế hoạch phát hành.
  - `tech-lead` ghi ở 1b, khi chưa có kiến trúc. Nó chỉ dùng để dự báo thô.
  - Cỡ áo không đổi trừ khi kế hoạch phát hành được sửa vì lý do khác. Không có PR tài liệu nào chỉ để đổi cỡ áo.
- **Story point theo story hoặc phần story** (Fibonacci 1, 2, 3, 5, 8, 13).
  - `tech-lead` đặt ở refinement, khi đã có mục tech (với `REL-1` là ở 1c). Người dùng chốt ở sprint planning.
  - Nguồn sự thật là Plane. Khi dự án không có Plane, nguồn sự thật là bảng story của kế hoạch sprint.
  - Kế hoạch sprint ghi lại ảnh chụp số điểm lúc cam kết, để tính velocity. Ảnh chụp này không được cập nhật về sau.
  - Đổi điểm trước khi story vào sprint không cần duyệt gì.
  - Story trên 8 điểm phải chia theo tập AC trước khi vào sprint.

### 5.3 Nền kiến trúc (giai đoạn 1c)

Tài liệu tech v1.0 của module gồm hai phần:
- **Phần chung cho cả module:** §2 Kiến trúc, §3 Mô hình dữ liệu, §5 Phân quyền, §7 Bảo mật, §12 Quyết định. Mục đích là để bản giao sau không phải đập đi làm lại mô hình dữ liệu hay quyền.
- **Phần của `REL-1`:** §4 API, §6 Luồng, §8 Giao diện, §11 Kiểm thử, chỉ cho các story của `REL-1`.

Các quy tắc khác:
- §1 *Phạm vi và truy vết* của mẫu tech thêm cột `REL`.
- **Trạng thái tính theo bản giao, không theo cả tài liệu.** Phần đầu tài liệu tech có bảng *Trạng thái theo bản giao*:

  | Phần | Trạng thái | Duyệt ở phiên bản |
  |---|---|---|
  | Chung | Approved | 1.0 |
  | REL-1 | Approved | 1.0 |
  | REL-2 | In review | 1.1 (nháp) |

- Mỗi bản giao sau thêm mục cho §4, §6, §8, §11 và tăng phiên bản phụ (1.1, 1.2…). Chỉ dòng của bản giao đó về *In review*. Các phần đã duyệt vẫn giữ trạng thái, nên story của `REL-n` đang làm không bị chặn khi refinement cho `REL-n+1` đang chạy.
- DoR và `pr-reviewer` đối chiếu story với phiên bản duyệt của phần chung và của đúng bản giao chứa story đó.
- Sửa phần chung là thay đổi lớn: tăng phiên bản chính, **mọi** dòng về *In review*, các story đang làm dừng lại theo quy tắc lệch lớn (`lifecycle.md` § 4), và `appsec` review lại nếu chạm hạng 2. Sửa một mục đã duyệt của một bản giao cũng đưa dòng của bản giao đó về *In review*.
- `docs/tech/README.md` và cột *Tech* của `progress.md` ghi trạng thái theo bản giao (ví dụ `Chung ✓, REL-1 ✓, REL-2 review`).
- Module có hạng 2: `appsec` review phần chung trước khi vào sprint đầu tiên, giống cổng hiện nay.
- Module dùng tài liệu tech dạng ngắn (`lifecycle.md` § 5b) vẫn theo cách chia này.

### 5.4 Vòng sprint

Một sprint là một Cycle trên Plane, giữ nhịp và cách đặt tên hiện có. Một sprint được chứa story của nhiều module, mỗi module một tệp kế hoạch sprint.

1. **Refinement.** Chạy trong sprint N, cho các story dự kiến làm ở sprint N+1:
   - phiên chính gọi `ui-designer`, rồi `tech-lead`, cho đúng các story đó;
   - story đạt DoR thì chuyển sang *Todo* trên Plane.
2. **Sprint planning.** Chạy vào ngày đầu sprint.
   - **Phiên chính đề xuất danh sách:** gồm các story *Todo* theo thứ tự backlog, cho đến khi tổng điểm chạm velocity, tức trung bình điểm *Done* của 3 sprint gần nhất. Khi chưa có 3 sprint thì người dùng tự đặt năng lực.
   - **Người dùng chốt** mục tiêu sprint (một câu) và danh sách. Sau đó phiên chính:
     - đưa các story đó vào Cycle;
     - ghi mục tiêu vào mô tả Cycle;
     - viết kế hoạch sprint bằng `writing-plans`.
   - **Kế hoạch sprint:**
     - tên tệp: `docs/superpowers/plans/YYYY-MM-DD-<module>-<SPR-…>.md`;
     - phần đầu: mục tiêu sprint, tên Cycle, các story và AC kèm điểm, các `REL` liên quan;
     - phase trong kế hoạch vẫn là đơn vị PR, giới hạn khoảng 1.500 dòng như cũ.
   - Người dùng duyệt kế hoạch là sprint được cam kết.
3. **Triển khai và merge.** Như giai đoạn 5–6 hiện nay, nhánh `feature/<module>-phase-<n>`. Trạng thái Plane đi theo các mốc ở § 5.6.
4. **Sprint review.** Ngày cuối sprint.
   - Phiên chính lập kịch bản demo cho từng story từ AC của nó, kèm bằng chứng: e2e của `qa-frontend`, ảnh chụp từ skill `run` trên `dev`.
   - Người dùng nghiệm thu từng story:
     - đạt → *Done*;
     - chưa đạt → giữ lại và ghi lý do trong comment;
     - yêu cầu mới phát sinh → quay về PRD (cỡ M), không sửa ngay trong sprint.
5. **Sprint retro.** Ngay sau review.
   - **Phiên chính ghi:** điểm cam kết so với điểm *Done*, số story dời sang sprint sau, story bị từ chối khi review, thời gian story nằm ở *Pending*, và các commit `fix` của sprint.
   - **Điều chỉnh ngay:** năng lực và cách sắp xếp sprint kế tiếp.
   - **Hoãn đến retro module:** ý cải tiến quy trình được ghi vào *Chờ retro* trong `docs/agent-error-log.md`, vì quy trình vẫn đóng băng giữa module (§ 8 giữ nguyên).
   - **Kết thúc sprint trên Plane:** `complete` Cycle, rồi `transfer_workitems` để dời các story dở dang sang Cycle kế tiếp.

### 5.5 Definition of Ready và Definition of Done

**DoR**, áp cho một story hoặc một phần story theo tập AC:
- thuộc PRD *Approved* và nằm trong một bản giao của kế hoạch phát hành *Approved*;
- có persona và AC;
- có bảng thiết kế đã duyệt, hoặc ghi rõ dùng mẫu có sẵn;
- có mục trong tài liệu tech đã duyệt;
- có story point, và không quá 8;
- không có phụ thuộc nào đang chặn;
- không còn câu hỏi nghiệp vụ mở.

**DoD:**
- code đã merge vào `dev` qua cổng của hạng;
- `scripts/test-gate.sh` đạt;
- mỗi AC có test;
- có mục `docs/feature-map.md`;
- không còn lệch lớn so với thiết kế chưa xử lý;
- PO đã nghiệm thu khi sprint review.

Hai định nghĩa này đặt ở `lifecycle.md`. Brief giao `implementer` và checklist của `pr-reviewer` dẫn chiếu về đó, không chép lại.

### 5.6 Plane

**Cấu hình:**
- Mỗi sản phẩm dùng DAONT-BASE có một tệp `.claude/plane.json`, gồm: project id và identifier; id của từng trạng thái (backlog, todo, inProgress, readyForTesting, testing, done, pending, cancelled); id của các label tầng (`BE`, `FE`, `Mobile`) và label `bug`.
- Không có tệp này thì phiên chính bỏ qua mọi bước Plane. Khi đó trạng thái story ghi trong bảng *Sprint* của `progress.md` (§ 5.9). Bản thân DAONT-BASE không có tệp này.

**Ánh xạ:**

| Repo | Plane |
|---|---|
| Module (PRD) | Module, đặt tên `[PRDxx] <tên tính năng>` |
| Story `PRDxx-US-nn`, hoặc một phần theo tập AC | Work item tên `PRDxx-US-nn — <tiêu đề>`, `external_source = daont-base`, `external_id = PRDxx-US-nn` (giao một phần thì thêm `#AC-07,AC-08`) |
| Bản giao `REL-n` | Label `PRDxx-REL-n` |
| Tầng kỹ thuật | Label `BE` / `FE` / `Mobile` |
| Sprint | Cycle `SPR-YYYY-MM-Sn` |
| Điểm | Estimate point |
| Lỗi (cỡ S) | Work item gắn label `bug` |

**Nguyên tắc chia việc giữa hai nơi:**
- Mô tả work item chỉ có link về mục PRD, link bảng thiết kế và link mục tech; AC không chép sang Plane.
- Liên kết PR gắn vào work item bằng `workitem_link`.
- Mô tả PR có dòng `Plane: KNX-12, KNX-15` liệt kê các work item mà PR đó phục vụ.

**Mốc cập nhật trạng thái:**

| Mốc trong repo | Trạng thái Plane |
|---|---|
| PRD *Approved* và kế hoạch phát hành *Approved* | Tạo work item ở *Backlog*, gắn Module, label và điểm |
| Story đạt DoR | *Todo* |
| `implementer` bắt đầu task của story | *In Progress* |
| PR của story được mở | *Ready For Testing* |
| PR merge vào `dev` | *Testing* |
| PO nghiệm thu ở sprint review và đạt DoD | *Done* |
| Bị chặn bởi câu hỏi nghiệp vụ hoặc lệch lớn | *Pending*, kèm comment trỏ tới câu hỏi |
| Story bị bỏ khỏi kế hoạch phát hành | *Cancelled* |

**Quyền ghi:**
- Chỉ phiên chính ghi lên Plane.
- Theo quy trình này, phiên chính được làm các việc sau mà không cần hỏi lại: tạo work item, đổi trạng thái, label, điểm, gắn link và viết comment, đúng tại các mốc ở trên.
- Các việc sau cần người dùng xác nhận mỗi lần: tạo, `complete` hoặc sửa Cycle; đưa story vào hay rút khỏi Cycle (vì đó là cam kết sprint); xoá bất cứ thứ gì; sửa Module.

### 5.7 Cỡ việc trong mô hình mới

| Cỡ | Đường đi |
|---|---|
| S (lỗi, việc vặt) | Work item `bug` ở *Backlog*. Vào sprint hiện tại nếu người dùng đồng ý, không thì vào sprint sau. Đường đi kỹ thuật như cũ |
| M (đổi hoặc thêm hành vi) | Sửa PRD như cũ, rồi cập nhật kế hoạch phát hành, rồi refinement và vào backlog như story mới |
| L (module mới) | Đủ giai đoạn 0 → 1c, rồi vòng sprint |

Lỗi phát hiện ở sprint review trên story của chính sprint đó không phải work item mới: story đó chưa *Done*.

### 5.8 Commit tài liệu và PR

- **Nhánh tài liệu của module** (`chore/<module>-docs`, một PR nháp như cũ) mang: PRD, kế hoạch phát hành, tài liệu tech v1.0 (nền kiến trúc cộng `REL-1`) và link thiết kế của `REL-1`. Nó merge một lần qua `/git-accept` khi cả bốn đã duyệt.
- **Mỗi bản giao sau** có một nhánh `chore/<module>-rel-<n>-docs`, là PR chỉ có tài liệu (tech tăng phiên bản phụ, link thiết kế), merge qua `/git-accept`.
- **Sửa kế hoạch phát hành giữa module** đi bằng một PR tài liệu nhỏ.
- **Kế hoạch sprint** đi trên nhánh `feature/*` cùng code, như kế hoạch hiện nay.
- `check-prd-approval.ts` mở rộng để kiểm cả thứ tự duyệt của kế hoạch phát hành: BA review trước, người dùng duyệt sau. Hoặc `check-release-plan.ts` tự làm việc này (để cho người làm, § 9).

### 5.9 Truy vết và số đo

**Chuỗi truy vết (`lifecycle.md` § 7):**
- Chuỗi mới: persona → US → FR → **REL** → bảng thiết kế → mục tech → **kế hoạch sprint** → task → test → feature-map → PR (có dòng **Plane**).
- Phát hiện mới ở các khâu review: story Must không có REL; PR không có dòng `Plane:` khi dự án có `.claude/plane.json`.

**`progress.md`:**
- Cột của bảng module: PRD | Kế hoạch phát hành | Nền kiến trúc | UI/Tech theo REL (ví dụ `REL-2/4`) | appsec | REL xong (ví dụ `1/4`) | Merged.
- Bảng mới *Sprint*, mỗi sprint một dòng: Cycle, mục tiêu, điểm cam kết, điểm *Done*, số story dời, số story bị từ chối ở review. Khi có Plane, số liệu lấy từ Cycle.
- Bảng *Số đo* của module thêm hai cột: số sprint, và velocity trung bình.

## 6. Tệp bị ảnh hưởng

| Tệp | Thay đổi |
|---|---|
| `docs/process/lifecycle.md` | § 1 bảng giai đoạn; mục mới cho 1b, 1c, vòng sprint, DoR/DoD, Plane; § 2 cỡ việc; § 3 cổng theo story; § 5 commit tài liệu; § 6 kế hoạch sprint và phase; § 7 truy vết; § 8 thêm retro sprint; § 10 tiến độ |
| `docs/process/progress.md` | Đổi cột, thêm bảng *Sprint*, thêm cột vào *Số đo* |
| `docs/process/decisions.md` | Mục quyết định mới, kèm lý do được áp dụng ngoài retro |
| `AGENTS.md` | Bảng Software Lifecycle: thêm 1b, 1c, 6b, 6c; sửa mô tả giai đoạn 4 |
| `docs/prd/README.md` | Bước 7 của quy trình trỏ sang kế hoạch phát hành |
| `docs/backlog/_template.md`, `docs/backlog/README.md` | Mới |
| `docs/tech/_template.md`, `docs/tech/README.md` | Cột REL ở §1; bảng *Trạng thái theo bản giao*; ghi chú phần chung và phần theo bản giao |
| `.claude/agents/product-manager.md` | Viết kế hoạch phát hành và story map |
| `.claude/agents/tech-lead.md` | Cỡ áo ở 1b, story point ở refinement, nền kiến trúc, tài liệu tăng theo bản giao |
| `.claude/agents/business-analyst.md` | Checklist review kế hoạch phát hành |
| `.claude/agents/ui-designer.md` | Thiết kế theo bản giao |
| `.claude/agents/solution-architect.md` | Thứ tự bản giao là câu chuyển lên người dùng |
| `.claude/agents/implementer.md`, `.claude/agents/pr-reviewer.md` | Dẫn chiếu DoD; dòng `Plane:` trong PR; `pr-reviewer` đối chiếu theo phiên bản duyệt của bản giao |
| `.claude/release-kit.json` | `prExtraSections`: thêm dòng `Plane:` nếu cơ chế cho phép |
| `scripts/check-release-plan.ts` (+ test), `scripts/test-gate.sh`, `.github/workflows/test.yml`, `.gitlab-ci.yml` | Script mới và chỗ gọi nó |
| `scripts/risk-tier.ts` | Không đổi. `docs/backlog/` là tài liệu, hạng 0 theo quy tắc hiện có (người làm xác nhận) |

Các tệp `.codex/agents/*.toml` chỉ trỏ tới `.claude/agents/*.md`, nên không cần sửa.

## 7. Áp dụng và chuyển tiếp

- Một PR quy trình duy nhất vào `dev`. PR chạm `AGENTS.md`, `.claude/` và CI nên người dùng tự merge, `pr-reviewer` không tự merge.
- Thứ tự làm đề xuất:
  1. Script và test của nó trước (hạng 2 vì chạm CI).
  2. Mẫu tài liệu.
  3. `lifecycle.md`, `progress.md`, `decisions.md`.
  4. Các agent.
  5. `AGENTS.md`.
- Áp dụng ngay khi merge, vì chưa có module nào đang chạy.
- Việc trên Plane do người dùng làm hoặc xác nhận, không thuộc PR này:
  - đổi Module của các dự án đang chạy sang chia theo tính năng, tầng chuyển thành label;
  - tạo `.claude/plane.json` trong từng sản phẩm.

## 8. Các quyết định là đề xuất của tôi (người dùng chưa xác nhận riêng)

1. Kế hoạch phát hành nằm trong repo (`docs/backlog/`), không chỉ là label trên Plane. Lý do: các agent và Codex chỉ đọc repo, CI kiểm được độ phủ story Must, và lịch sử git giữ lại lý do đổi thứ tự. Plane chỉ phản chiếu bằng label.
2. Sửa kế hoạch phát hành không kéo PRD về *In review*.
3. Story được giao một phần theo tập AC, để không phải sửa PRD mỗi khi chia nhỏ story.
4. Điểm theo dãy Fibonacci, story trên 8 điểm phải chia. Velocity là trung bình 3 sprint gần nhất.
5. Retro sprint chỉ điều chỉnh năng lực và cách sắp xếp sprint. Thay đổi quy trình vẫn chờ retro module (giữ quy tắc đóng băng).
6. Chuẩn bị trước đúng một sprint (refinement cho sprint N+1 trong sprint N).
7. Phiên chính được tự cập nhật trạng thái Plane tại các mốc; mọi thao tác trên Cycle cần người dùng xác nhận.
8. Mỗi bản giao sau `REL-1` có một PR tài liệu riêng qua `/git-accept`.
9. Không có `.claude/plane.json` thì không dùng Plane, và quy trình vẫn chạy bằng `progress.md`.
10. Ước lượng hai mức: cỡ áo theo bản giao nằm trong repo, story point theo story nằm trên Plane (hoặc trong kế hoạch sprint khi không có Plane). Không mức nào chép sang nơi kia.
11. Thiết kế UI và tech của `REL-1` làm trước sprint đầu tiên (1c-UI rồi 1c). Từ `REL-2`, chuẩn bị trước một sprint.
12. Trạng thái tài liệu tech tính theo bản giao. Refinement cho bản giao sau không chặn bản giao đang làm; chỉ sửa phần chung mới chặn tất cả.
13. `check-release-plan.ts` không bắt PRD đã duyệt phải có kế hoạch phát hành ngay. Nó chặn ở chỗ kế hoạch sprint đầu tiên.

## 9. Để lại cho người làm

- Cách viết chính xác của `check-release-plan.ts`: phân tích bảng markdown thế nào, và có gộp kiểm thứ tự duyệt vào nó hay mở rộng `check-prd-approval.ts`.
- Cách diễn đạt và đánh số mục trong `lifecycle.md`. Phải giữ "mỗi quy tắc một nơi".
- Cấu trúc chi tiết của `.claude/plane.json`.
- Cách `release-kit` thêm dòng `Plane:`: qua `prExtraSections`, hay chỉ quy định trong `implementer.md`.

## 10. Rủi ro

- **API estimate của Plane trả về 404 trên instance hiện tại.** Cần kiểm tra trên giao diện xem có ghi điểm qua MCP được không. Nếu không được, ghi điểm bằng label `pt-3`, `pt-5`… hoặc để người dùng nhập tay.
- **Nghi thức nặng hơn giá trị.** Số đo theo dõi: số ngày từ khi PRD được duyệt đến khi `REL-1` *Done*. Retro module đầu tiên theo quy trình này xem lại con số đó.
- **Nền kiến trúc làm quá nhiều trước.** Giới hạn: phần chung chỉ gồm §2, §3, §5, §7, §12. Mọi thứ khác đi theo bản giao.
