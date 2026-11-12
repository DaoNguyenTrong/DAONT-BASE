# Tham khảo: quyết định quy trình FEEDBACK-HUB

Bản nguồn tại thời điểm đồng bộ 2026-11-12, giữ nguyên bên dưới để tra lý do. Các PR, module, sự kiện, phê duyệt và số đo trong bản này thuộc FEEDBACK-HUB, không phải DAONT-BASE. Các đường dẫn bên dưới được hiểu từ gốc FEEDBACK-HUB. Quy tắc hiện hành của DAONT-BASE nằm ở `../lifecycle.md` và các tệp được nó dẫn chiếu.

---

# Quyết định quy trình

Quyết định về cách làm việc (agent, cổng duyệt, review) ghi ở đây, mới nhất ở trên. Tệp này chỉ thêm mục mới, không sửa mục cũ: quyết định sau thay quyết định trước bằng một mục mới. Quy tắc đang áp dụng nằm ở `lifecycle.md`, và tham chiếu `§` không ghi tên tệp trong các mục là mục của tệp đó. Quyết định kỹ thuật theo `AGENTS.md` § Decision Log.

### 2026-11-12 — Bớt chi phí nền và thủ tục tài liệu
Soi lại quy trình sau khi có hạng kiểm tra, người dùng cho làm tám việc:
1. `/git-accept` không gọi agent review nữa: `scripts/check-prd-approval.ts` kiểm thứ tự duyệt PRD, phiên chính đọc diff. PR tài liệu là hạng 0, nên một lượt agent mỗi PR tài liệu là thừa.
2. Nhật ký quyết định chuyển sang tệp này, `serena.md` chỉ nạp khi làm `backend/` hoặc `frontend/`. Đọc quy tắc không còn phải đọc kèm 14 KB lịch sử.
3. Hạng 2 chỉ còn một lượt review Opus cuối phase: chính `pr-reviewer` chạy Opus. Trước đây review cuối và `pr-reviewer` đọc cùng một diff.
4. Số đo có thêm token agent, lấy từ báo cáo hoàn thành của từng agent. Retro module 00 bỏ số token vì cho là không đo được; với agent thì đo được.
5. Mỗi module một PR tài liệu (PRD và tài liệu kỹ thuật), merge một lần. Module 00 có 6 PR tài liệu cho 1 PR code.
6. Bảng tối chỉ vẽ cho mẫu mới và hệ thống; màu đã lấy từ token nên màn hình theo mẫu cũ không cần bảng tối riêng.
7. Hook `codegraph prompt-hook` (cấu hình người dùng) không chạy trong repo này, nhờ tệp đánh dấu. Nó chèn khoảng 9 KB mã vào mọi prompt, kể cả prompt không liên quan tới mã.
8. Khi chạy kế hoạch, phiên chính đề nghị chuyển sang Sonnet và mở phiên mới cho mỗi phase; người dùng quyết.

Với repo này, hạng 1 không có review Opus nào, khác quy tắc chung "final review: opus, once" ở `~/.claude/CLAUDE.md`; đây là lệnh của người dùng. Áp dụng ngoài retro theo lệnh của người dùng.

Phương án loại:
- Script kiểm đường dẫn cũ trong tài liệu: chưa có lỗi lặp lại nào thuộc loại này (ngưỡng ở §8).
- Tắt mọi hook (`disableAllHooks`): sẽ mất luôn guard Bash.
- Bỏ hẳn bảng tối: dark là bảng màu riêng, mẫu mới vẫn cần thiết kế tối.

### 2026-11-12 — Kiểm tra theo hạng rủi ro, không theo cỡ việc
Người dùng thấy chạy đủ quy trình quá tốn. Ở module 00, mọi thay đổi đều trả cùng một mức review sau từng task. Thế nhưng 22 commit `fix` thuộc các nhóm lỗi lặp lại tập trung ở auth, token, đồng thời và xác minh email, còn nhóm lỗi giao diện chỉ có 3 commit và test tự động bắt được. Vì vậy:
- Mỗi thay đổi có một hạng 0/1/2, do `scripts/risk-tier.ts` tính theo đường dẫn và dấu hiệu trong diff (§2 Hạng kiểm tra).
- Hạng 1 bỏ review sau từng task, chỉ còn một lượt review cho cả PR.
- Module không nhạy cảm dùng tài liệu kỹ thuật dạng ngắn (§5b). Bảng thiết kế chỉ vẽ cho màn hình hoặc mẫu tương tác mới (§1).
- Câu hỏi gửi SA được gom một lượt mỗi giai đoạn (§1).
- Retro đo tỉ lệ `fix` theo hạng (§8).

Quyết định này áp dụng ngoài retro, theo lệnh của người dùng (ngoại lệ của đóng băng quy trình ở §8).

Phương án loại:
- Gộp PRD và tài liệu kỹ thuật thành một spec: phải sửa ba agent và chuỗi truy vết, trong khi dạng ngắn đạt phần lớn mức tiết kiệm.
- Job CI gắn nhãn hạng cho PR: cần quyền ghi trong CI, trong khi test của script đã chạy ở `test / comments`.
- Để người làm tự đánh hạng: một lần đánh giá sai là mất hết lớp kiểm tra.

### 2026-11-12 — Codex dùng chung nội dung Claude, không giữ bản prompt thứ hai

Codex không nạp `paths:` của rule, file agent, slash command hay `settings.json` của Claude. Chép prompt sang cấu hình Codex sẽ lệch ngay lần sửa đầu tiên, còn ghi quy tắc git vào `AGENTS.md` thì không chặn được lệnh. Vì vậy `.claude/` vẫn là nguồn nội dung; Codex chỉ có lớp mỏng trỏ về nguồn đó (agent, skill, hook gọi cùng script guard). Phương án loại: duy trì hai bộ prompt, và coi hướng dẫn trong `AGENTS.md` là đủ thay cho hook.

### 2026-11-12 — Retro module 00: mỗi phase một PR, retro bắt buộc, đóng băng quy trình
Đánh giá quy trình sau module 00 cho thấy:
- PR #32 gom cả 8 phase: 607 tệp, +61.779/−10.594 dòng, 100 commit. Không ai review hết được, nên cổng người dùng ở §3 chỉ còn hình thức.
- 42 trên 158 commit của nhánh là `fix`, trong đó bảy nhóm nguyên nhân lặp lại (`docs/agent-error-log.md`, mục 2026-11-12).
- Nhật ký lỗi không có mục mới từ 2026-10-19 và chưa mục nào được tự động hoá: quy tắc ở §8 chưa có bước nào ép thực hiện.
- Khoảng 10 quyết định quy trình trong 5 ngày; kế hoạch mỗi phase dài 400–900 dòng, phần lớn là mã viết sẵn.

Người dùng yêu cầu áp dụng các cải tiến; SA (thay người dùng) quyết chi tiết:
- Mỗi phase một PR, giới hạn mềm 1.500 dòng mã, xếp chồng tối đa một tầng (§6).
- Kế hoạch không chép mã hoàn chỉnh; PRD module thường nhắm khoảng 500 dòng (§5b).
- Retro bắt buộc cuối module, đóng băng quy trình giữa module, mỗi quy tắc một nơi (§8).
- Bốn số đo mỗi module trong `progress.md`; bỏ số token vì không đo chính xác được.
- PR #32 vẫn là một PR: tách sau 100 commit tốn hơn lợi ích, vì một số fix của phase 1 và 3 nằm cuối nhánh. Thay vào đó `appsec` (Opus) review phần bảo mật, `pr-reviewer` review phần còn lại, và người dùng nhận một checklist rủi ro thay vì cả diff.

Phương án loại: tách PR #32 thành 8 PR xếp chồng (rebase theo phase dễ hỏng, phải chạy lại CI từng phần); kiểm tra CI cho mọi tham chiếu `§` ngay bây giờ (chưa có bằng chứng tham chiếu hỏng lặp lại); giới hạn cứng 400 dòng cho PRD (dễ thành cắt phạm vi).

### 2026-10-26 — Tăng tốc chạy kế hoạch trong một bản checkout
Người dùng thấy chạy kế hoạch còn chậm, nhưng muốn giữ nguyên quy tắc dự án. Quy tắc chung (gộp task nhỏ, cho review chạy chồng lên task kế tiếp, brief mang đủ phán quyết, nhịp chạy test) nằm trong `~/.claude/CLAUDE.md` § Subagent-driven execution pacing. Ở dự án này còn thêm:
- Mỗi lúc chỉ một agent build backend, vì các agent dùng chung `obj/` (`-m:1`). Hai task backend chạy nối tiếp nhau. Một task backend và một task frontend được chạy song song.
- Trong lúc làm, agent chỉ chạy project test bị ảnh hưởng. Trước khi commit, agent chạy `backend/scripts/test.sh` hoặc `bun run --cwd frontend test:run` một lần.
- Chỉ phiên chính chạy `scripts/test-gate.sh`, ở cuối phase và sau lượt sửa cuối.

Phương án loại: hai luồng song song bằng git worktree. Mỗi luồng phải cài và build lại từ đầu, các tệp dùng chung (`locales`, `router`, `feature-map.md`) phải gộp tay, và cách này trái với `AGENTS.md` § Branch & worktree. Chỉ dùng khi người dùng duyệt riêng.

### 2026-10-26 — Chọn model theo việc, mặc định Sonnet
Người dùng thấy các agent chạy ổn nhưng tốn kém. Ở module 00, gần như mọi implementer và reviewer chạy Opus, mỗi agent tốn 120–290 nghìn token. Vì vậy `implementer` và `code-reviewer` khai `model: sonnet` trong frontmatter, để một lượt gọi không ghi model cũng không thành Opus. Phiên chính chỉ đặt Opus qua tham số `model` cho:
- reviewer của task đụng tới đăng nhập, token, cookie, khoá tổ chức, kiểm quyền hoặc lọc theo tổ chức; implementer của các task này vẫn chạy Sonnet;
- review cuối mỗi phase.

Ngoài ra:
- Vòng sửa giao cho một implementer Sonnet mới, không gọi lại agent cũ, vì agent được gọi lại giữ model của nó. Chỉ khi sửa lần thứ hai vẫn chưa đạt mới chuyển sang Opus.
- Task cơ học (export contract, feature map, tài liệu, sửa chỉ trong test) không có review riêng; phiên chính tự xem diff.
- Chỉ task bảo mật mới cài lỗi để thử test, khoảng 5 lỗi.

Phương án loại: chỉ nhắc phiên chính chọn model mỗi lần gọi. Một lần quên thì agent đó chạy Opus.

### 2026-10-23 — `solution-architect` trả lời câu hỏi thay người dùng
Người dùng thấy bị hỏi quá nhiều: PRD-00 và thiết kế module 00 đã có hơn 80 câu hỏi, phần lớn được chấp nhận theo đề xuất. Từ nay mọi câu hỏi của agent đi tới `solution-architect` trước. Agent này đóng vai một SA dày dặn kinh nghiệm, chuyên Vue 3 và dashboard, ưu tiên cách đơn giản nhất thật sự cần, SOLID và Clean Architecture, giao diện tối giản và cân đối. Nó trả lời thay người dùng và chỉ chuyển lên những câu thuộc danh sách người dùng phải quyết:
- duyệt tài liệu và kế hoạch;
- các lệnh mà quy tắc dành cho người dùng;
- tiền và pháp lý;
- hướng sản phẩm, hoặc đổi một quyết định người dùng đã đưa;
- nới quy tắc bảo mật;
- lựa chọn khó đảo ngược mà không đoán được ý người dùng.

Câu trả lời của SA được ghi nguồn "SA (thay người dùng)", để người dùng soát lại khi duyệt tài liệu. SA quyết theo bối cảnh hệ thống trong `docs/process/system-context.md` (quy mô, hạ tầng, giới hạn). Mỗi lần người dùng bác một câu trả lời của SA, phiên chính ghi bài học vào `.claude/sa-calibration.md` trong cùng PR với tài liệu được sửa; SA đọc tệp này trước khi trả lời. Phương án loại: để `product-manager` tự ghi nhiều mặc định hơn. Cách này không có người chịu trách nhiệm cho câu trả lời, và câu hỏi về thiết kế, kỹ thuật vẫn phải hỏi người dùng.

### 2026-10-22 — Soạn PRD: khoanh phạm vi, chỉ hỏi điều người dùng phải quyết, BA review sớm
Bản nháp đầu của PRD-00 đi qua 8 phiên bản và 78 câu hỏi; người dùng chấp nhận đề xuất ở 77 câu, PRD phình tới 246 AC vì quyết luôn chi tiết của PRD-03/04/05/07, và lỗi nặng nhất (đường leo quyền có sẵn trong nền tảng) chỉ lộ ra khi BA review ở bản 0.5. Vì vậy:
- Mỗi PRD chỉ quyết phần của module mình; phần thuộc module khác chuyển vào §12 *Chuyển cho PRD khác*.
- Câu hỏi mở chỉ dành cho điều người dùng phải quyết, tối đa khoảng 8 câu mỗi lượt; phần còn lại, kể cả mức ưu tiên, là *Mặc định đề xuất*, được duyệt một lần cùng PRD.
- BA review ngay bản nháp đầu, và báo cáo được giữ lại cho lần review sau.
- Phiên chính trình hệ quả ngay khi một câu trả lời chạm bảo mật hay tách dữ liệu, trước khi chuyển cho agent viết.
- Trong bản nháp, mục bị bỏ được xoá thẳng thay vì gạch `~~Removed~~`; vẫn không đánh số lại và không dùng lại mã. Gạch bỏ chỉ áp dụng từ khi PRD được duyệt lần đầu.
- Bản nháp nằm trên nhánh riêng với một PR nháp từ đầu (§ Commit tài liệu), để báo cáo review được lưu thành comment.
- Mỗi PR một mục đích (§ Quyền push và mở PR).

Phương án loại: giữ nguyên quy trình và chỉ nhắc agent hỏi ít hơn (không có tiêu chí để phân loại, lặp lại vẫn xảy ra).

### 2026-10-22 — Bật lại `quick` qua PR khi release-kit hỗ trợ
release-kit v2.0.2 thêm `devWrites: "pr"` và `devMergeCommand`, nên phát hành nhanh và việc đưa `main` về `dev` không còn ghi thẳng vào `dev`; `quick` được bật lại. `pushAfterRebase` và `prExtraSections` cho phép dùng `git-sync`/`git-mr` đúng quy trình rebase và mẫu mô tả PR. Thay thế mục "Phát hành theo release-kit" ở điểm tắt `quick`.

### 2026-10-22 — Agent review không dùng skill `code-review`
Skill này chạy một lượt review nền có thể kết thúc sau khi agent đã kết luận. Ở PR #7, `pr-reviewer` merge rồi lượt nền mới báo một lỗi mất dữ liệu. Lỗi lặp hai lần (PR #4, #7) nên đổi thành quy tắc: agent tự review trong lượt chạy và chỉ merge khi mọi kiểm tra đã xong. Phương án loại: giữ skill và chờ (agent không có cách chờ đáng tin).

### 2026-10-22 — Phát hành theo release-kit, không ghi thẳng vào `dev`
CHANGELOG được dựng từ mục `## Changelog` của từng PR, nên mọi PR phải có mục này. Fix trong giai đoạn ổn định và hotfix cũng đi qua review; đưa `main` về `dev` là một PR thay vì push thẳng. Chế độ `quick` tạm tắt vì nó commit thẳng vào `dev`, cho đến khi plugin có tuỳ chọn đi qua PR. Phương án loại: sửa `CHANGELOG.md` bằng tay trong mỗi PR (xung đột giữa các nhánh song song).

### 2026-10-22 — Xoá nhánh ngay sau khi merge
Nhánh đã merge để lại trên remote và local làm danh sách nhánh rối và dễ rebase nhầm. Người merge xoá nhánh remote sau khi xác nhận nó đã nằm trong `dev`; nhánh local xoá bằng `git branch -d` để git tự chặn nếu còn commit chưa merge. Phương án loại: `gh pr merge --delete-branch` (nó xoá luôn nhánh local và đổi checkout dùng chung giữa các agent).

### 2026-10-22 — Merge từng PR một, rebase trước, giữ merge commit
Nhiều nhánh song song merge chồng nhau làm lịch sử `dev` rối. Mỗi lúc chỉ một PR được review và merge, nhánh luôn rebase lên `dev` mới nhất trước khi review, và merge bằng `--merge` để vẫn thấy ranh giới từng PR và revert cả PR bằng một lệnh. Phương án loại: `--rebase` (mất ranh giới PR) và `--squash` (mất commit theo task mà review từng task dùng).

### 2026-10-22 — Lệch thiết kế chia hai mức; tài liệu commit khi được duyệt
`implementer` không được sửa tài liệu kỹ thuật, nếu không nó tự hợp thức hoá chỗ lệch của chính mình. Lệch nhỏ chỉ khai báo trong PR để không phải duyệt lại vì chi tiết nội bộ; lệch lớn chạm vào thứ người dùng đã duyệt nên phải duyệt lại. Tài liệu commit khi được duyệt để `pr-reviewer` đối chiếu với bản đúng. Phương án loại: để `implementer` tự sửa tài liệu kỹ thuật.

### 2026-10-22 — Review và merge vào `dev` do agent gác cổng, không làm bằng tay
Không có tài khoản reviewer thứ hai và tác giả không tự duyệt được PR của mình, nên `pr-reviewer` review theo quy tắc repo, PRD và thiết kế, tối đa 3 lượt, và tự merge khi đạt. Xác thực, phiên, quyền, CI, file quy tắc agent và migration vẫn cần người dùng. Đánh đổi: cổng chạy phía client nên ép được quy trình, không phải kiểm soát truy cập. Phương án loại: bật branch protection bắt buộc review (không ai duyệt được).

### 2026-10-22 — Quyết định ghi theo phạm vi, không duyệt riêng
Quyết định module nằm ở §12 tài liệu kỹ thuật, được duyệt cùng tài liệu; quyết định nền tảng ghi vào `.claude/decisions.md` trong cùng PR, chỉ được thêm; quyết định quy trình ghi ở đây. Phương án loại: một cổng duyệt riêng cho mọi mục (chậm, trùng với duyệt tài liệu) và bỏ hẳn `decisions.md` (quyết định nền tảng mất chỗ ở).
