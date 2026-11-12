# Vòng đời phát triển phần mềm

Tài liệu này mô tả đường đi từ ý tưởng đến phát hành, ai làm từng giai đoạn, ai duyệt và khi nào xong. `AGENTS.md` giữ các quy tắc chung (commit, push và PR, công cụ, nơi ghi quyết định); chi tiết từng giai đoạn nằm ở đây.

## 1. Các giai đoạn

| # | Giai đoạn | Sản phẩm đầu ra | Người làm | Cổng duyệt | Xong khi |
|---|---|---|---|---|---|
| 0 | Ý tưởng và tầm nhìn | Persona (`docs/prd/00-personas.md`), tầm nhìn, danh sách module, thứ tự ưu tiên | Phiên chính (`superpowers:brainstorming`) cùng người dùng; persona do `product-manager` viết, `business-analyst` review (§1b) | Người dùng | Persona ở trạng thái *Approved*, danh mục module trong `docs/prd/README.md` được chốt |
| 1 | Yêu cầu | PRD trong `docs/prd/` | `product-manager`, review bởi `business-analyst` | Người dùng | PRD ở trạng thái *Approved* |
| 2 | Thiết kế giao diện | Bảng thiết kế trên canvas, gắn mã yêu cầu | `ui-designer` | Người dùng | Mọi FR có màn hình mới hoặc mẫu tương tác mới đều có bảng; màn hình theo mẫu có sẵn chỉ ghi tên mẫu; bảng tối chỉ cho mẫu mới (`ui-designer.md` quy tắc 1); người dùng duyệt |
| 3 | Thiết kế kỹ thuật | Tài liệu trong `docs/tech/` | `tech-lead` (`appsec` trước với module nhạy cảm) | Người dùng | Tài liệu *Approved*, mọi FR/AC có mục thiết kế |
| 4 | Kế hoạch | Kế hoạch trong `docs/superpowers/plans/` | Phiên chính (`superpowers:writing-plans`) | Người dùng | Người dùng duyệt kế hoạch; đồng thời quyết định có cấp quyền push và PR không |
| 5 | Triển khai | Code, test, mục `docs/feature-map.md` | `implementer`, mỗi lần một task | Theo hạng (§2 Hạng kiểm tra) | Mọi task của phase xong, `scripts/test-gate.sh` đạt |
| 6 | Review và merge | PR được merge vào `dev` | `pr-reviewer` (PR chỉ có tài liệu: `/git-accept`) | Tự động, trừ vùng cần người dùng | PR vào `dev`; sau PR cuối của module, phiên chính chạy retro (§8) |
| 7 | Phát hành | Bản phát hành, changelog | Skill `release-kit:git-release` | Người dùng | Tag và merge vào `main` |
| 8 | Vận hành và phản hồi | Phản hồi từ sản phẩm dùng DAONT-BASE | Người dùng | — | Quay lại giai đoạn 0, 1 hoặc 5 theo cỡ việc |

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

**Thay đổi.** Sửa persona đã duyệt thì tăng phiên bản và đưa tài liệu về *In review* (§3); các PRD có story của persona bị sửa được xem lại. Phản hồi ở giai đoạn 8 là nguồn chính để cập nhật persona. Retro cuối module (§8) kiểm tra có persona nào không còn story nào phục vụ.

## 2. Cỡ công việc

Phiên chính chốt cỡ ở đầu mỗi việc và nói rõ cho người dùng; người dùng có thể đổi.

| Cỡ | Khi nào | Đường đi |
|---|---|---|
| **S** | Sửa lỗi, việc vặt, đổi nhỏ không thay đổi hành vi hay hợp đồng | Bỏ giai đoạn 0–4. Lỗi: test thất bại trước. `implementer`, rồi review theo hạng |
| **M** | Đổi hoặc thêm hành vi trong module đã có | Sửa PRD (tăng phiên bản, về *In review* rồi duyệt lại), thiết kế và tài liệu kỹ thuật chỉ phần bị ảnh hưởng, kế hoạch ngắn, rồi giai đoạn 5–6 |
| **L** | Module mới, hoặc thay đổi chạm quyền, phiên, định danh | Đủ giai đoạn 0–6, `appsec` nếu nhạy cảm |

Cỡ S không được thêm hành vi người dùng nhìn thấy hay đổi API. Nếu việc S hóa ra cần đổi hành vi, dừng và nâng lên M.

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

- **Không vào giai đoạn sau khi giai đoạn trước chưa duyệt.** Ngoại lệ: module nhạy cảm có thể viết tài liệu kỹ thuật song song với thiết kế giao diện khi người dùng cho phép và phạm vi ngoại lệ được ghi trong tài liệu module.
- **Câu hỏi nghiệp vụ phát sinh ở giai đoạn sau quay về PRD**, do `product-manager` sửa, không quyết ở nơi phát hiện. Câu hỏi kỹ thuật thuộc `tech-lead`.
- **Thay đổi sau khi đã duyệt:** sửa PRD đưa nó về *In review*, kéo theo xét lại thiết kế, tài liệu kỹ thuật và kế hoạch bị ảnh hưởng. Tài liệu bị ảnh hưởng cũng về *In review* và tăng phiên bản. Ngoại lệ: điền hay sửa dòng liên kết và metadata (*Thiết kế*, *Kế hoạch*, *Cập nhật*) không tính là thay đổi.
- **Sau 3 lượt sửa PR vẫn chưa đạt:** `pr-reviewer` trả `ESCALATE`, chỉ ra tài liệu cần nghĩ lại (PRD, thiết kế hay kế hoạch). Không có lượt sửa thứ 4.
- **Vùng cần người dùng:** xác thực, token, cookie, phiên, quyền, đổi tổ chức, khoá API, định danh widget, `.claude/`, `.github/`, `AGENTS.md`, cấu hình CI và Lefthook, EF migration, cấu hình triển khai lưu trữ/Redis/SignalR, và thay đổi `.claude/decisions.md` sửa hay xoá mục cũ hoặc có dòng `Supersedes:`. `pr-reviewer` không tự merge các PR này.

## 4. Lệch thiết kế khi triển khai

Khi code không thể làm đúng tài liệu kỹ thuật đã duyệt:

| Mức | Khi nào | Xử lý |
|---|---|---|
| **Nhỏ** | Không đổi hợp đồng API, quyền, mô hình dữ liệu lưu trữ hay hành vi người dùng thấy (ví dụ tên class nội bộ, thêm index, tách service) | `implementer` làm tiếp, ghi vào mục *Lệch nhỏ so với thiết kế* của báo cáo và mô tả PR kèm lý do. `pr-reviewer` chặn nếu chỗ lệch không thật sự nhỏ hoặc không được ghi. Tài liệu kỹ thuật không sửa |
| **Lớn** | Đổi bất kỳ điều nào ở trên | `implementer` dừng task, trả chỗ lệch kèm bằng chứng và đề xuất. Phiên chính gọi `tech-lead` sửa tài liệu trên nhánh đang làm (tăng phiên bản, về *In review*); câu hỏi nghiệp vụ đi qua `product-manager` trước. Người dùng duyệt lại, rồi `implementer` làm tiếp. Bản sửa đi chung PR với code |

Không chắc thì coi là lớn. Để lệch lớn lộ ra sớm, kế hoạch đặt phần có rủi ro kỹ thuật cao thành task spike đầu tiên.

## 5. Commit tài liệu

- **PRD và tài liệu kỹ thuật của một module** nằm chung một nhánh `chore/<module>-docs` cắt từ `dev` ngay từ bản nháp đầu, với **một PR nháp** cho cả module. Phiên chính commit mỗi phiên bản lên nhánh đó, để lịch sử các phiên bản và báo cáo review (comment trên PR) không mất giữa các phiên làm việc. Bắt đầu soạn PRD của module là lệnh push và mở PR nháp đó.
  - Mỗi lần người dùng duyệt một tài liệu (PRD trước, tài liệu kỹ thuật sau), phiên chính commit bản duyệt lên nhánh. Link bảng thiết kế ghi ở dòng *Thiết kế* của PRD.
  - Các agent ở giai đoạn sau (`ui-designer`, `tech-lead`, `writing-plans`) đọc tài liệu từ nhánh này.
  - PR được merge **một lần** qua `/git-accept`, khi mọi tài liệu của module đã *Approved*. Nếu thiết kế hay tài liệu kỹ thuật bị chặn lâu, người dùng có thể cho merge riêng PRD đã duyệt.
- **Kế hoạch** được phiên chính commit trên nhánh `feature/*` của nó, đi chung PR với code.
- **Mọi PR**, kể cả PR chỉ có tài liệu, có mục `## Changelog` theo định dạng của `release-kit:git-mr` (`- Added: …`, `- Fixed: …`, hoặc chỉ `none` cho thay đổi nội bộ), và luôn có một mục `## ` khác theo sau: `git-release` đọc mục này tới heading `## ` kế tiếp, nên dòng chữ tự do ở cuối (dòng ghi nguồn) sẽ làm nó hỏng. `git-release` dựng CHANGELOG của bản phát hành từ các mục này; không sửa `CHANGELOG.md` bằng tay.
- **Sửa tài liệu do lệch lớn** được phiên chính commit trên nhánh đang làm, đi chung PR với code; vì vậy `pr-reviewer` luôn đối chiếu code với đúng phiên bản tài liệu.
- `implementer` không sửa và không commit PRD, tài liệu kỹ thuật hay kế hoạch.

## 5b. Độ dài tài liệu

- **Kế hoạch** mô tả cho từng task: giao diện (chữ ký public, DTO, endpoint), danh sách tệp, ca kiểm thử và tiêu chí nghiệm thu. Chỉ chép đoạn mã ngắn khi chữ ký hoặc thuật toán khó tả bằng lời. Không viết sẵn mã hoàn chỉnh: `implementer` viết lại nó, và bản trong kế hoạch sẽ lệch khỏi mã thật.
- **PRD** của một module thường nhắm khoảng 500 dòng. Đây là giới hạn mềm: module lớn được vượt nếu ghi lý do ở đầu PRD. Không cắt phạm vi để giữ số dòng, vì cắt phạm vi là quyết định của người dùng.
- **Tài liệu kỹ thuật dạng ngắn** cho module ghi `không cần` ở cột *appsec* của `progress.md`. Bắt buộc có §1, §3, §4, §5, §11, §12 và §13 của mẫu. Các mục còn lại được để một dòng "Không áp dụng: <lý do>". Nhắm khoảng 200 dòng. Với các module này, `business-analyst` review PRD một lượt; lượt thứ hai chỉ khi lượt đầu có phát hiện `blocking`.

## 6. Quyền push và mở PR

Mặc định chỉ push hay mở PR khi người dùng ra lệnh. Ở giai đoạn 4, khi duyệt kế hoạch, người dùng có thể cấp **một lần cho cả kế hoạch**: mỗi khi một phase xong và `scripts/test-gate.sh` đạt, `implementer` được push nhánh của phase đó và mở một PR vào `dev`. Quyền này ghi trong đầu kế hoạch và trong brief giao cho `implementer`; không áp dụng cho `main`, và không thay cho cổng `pr-reviewer`.

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

Mã yêu cầu đi suốt chuỗi: persona (`P-nn`) → user story (`PRDxx-US-nn`) → PRD (`PRDxx-FR-nn`) → tiêu đề bảng thiết kế → mục tài liệu kỹ thuật → task trong kế hoạch → test → dòng `PRD:` của feature-map → mô tả PR. Một mã không có hạ nguồn, hoặc một thành phần không có mã thượng nguồn, là một phát hiện ở bất kỳ khâu review nào.

## 8. Lỗi lặp và retro cuối module

Cùng một góp ý xuất hiện hai lần thì không thêm quy tắc văn bản nữa: biến nó thành lint, kiểm thử kiến trúc hoặc generator (`AGENTS.md`).

**Retro cuối module.** Khi PR cuối của một module đã merge vào `dev`, và trước khi đánh *Merged* trong `progress.md`, phiên chính chạy retro:
1. Phân loại các commit `fix` của module theo nguyên nhân gốc (một agent Sonnet đọc `git log` và `git show` của các nhánh module).
2. Ghi mỗi nhóm nguyên nhân vào `docs/agent-error-log.md`, và xử lý các ý trong mục *Chờ retro* của tệp đó.
3. Nhóm gặp từ hai lần trở lên thì biến thành kiểm tra tự động (việc cỡ S cho `implementer`), hoặc ghi lý do không làm được vào mục đó. Mục đã tự động hoá ghi "Đã chuyển thành: …".
4. Ghi số đo của module vào `progress.md` § Số đo, kèm bảng của `bun scripts/risk-tier.ts --fix-ratio <dải commit của module>` và tổng token agent cộng từ mục `## Verification` của các PR phase.
5. Gom các thay đổi quy trình của module vào một PR quy trình, kèm danh sách persona không còn story nào phục vụ (§1b), nếu có.
6. Nếu tỉ lệ `fix` của hạng 1 cao hơn của hạng 2, đưa đường dẫn của các commit `fix` hạng 1 vào danh sách hạng 2 của `scripts/risk-tier.ts`, trong PR quy trình ở bước 5.

**Đóng băng quy trình giữa module.** Thay đổi quy trình (tài liệu này, `AGENTS.md`, `.claude/agents/`, `.claude/rules/`) chỉ áp dụng ở retro. Giữa module chỉ sửa lỗi quy trình đang chặn việc, kèm lý do trong một mục của `docs/process/decisions.md`. Ý cải tiến phát sinh giữa module được ghi vào mục *Chờ retro* của `docs/agent-error-log.md`.

**Mỗi quy tắc một nơi.** Mỗi quy tắc có một tệp sở hữu; nơi khác chỉ trỏ tới nó bằng `tệp § mục`, không chép lại nội dung. `AGENTS.md` là mục lục và chỉ giữ những quy tắc mọi phiên cần ngay. Nếu tham chiếu `§` hỏng xuất hiện từ hai lần, thêm một script kiểm tra vào `scripts/test-gate.sh`.

## 9. Quyết định quy trình

Nhật ký quyết định quy trình nằm ở `docs/process/decisions.md`, mới nhất ở trên.

## 10. Tiến độ module

Bảng tiến độ nằm ở `docs/process/progress.md`: mỗi module một dòng, mỗi giai đoạn một cột. Phiên chính là người duy nhất cập nhật bảng này: trong PR tài liệu khi PRD hoặc tài liệu kỹ thuật được duyệt, trong PR tính năng cho các cột *Plan* và *Code*, và đánh *Merged* ở PR tài liệu tiếp theo của module, chỉ sau khi retro cuối module (§8) xong. Bảng *Số đo* trong cùng tệp ghi sáu số mỗi module: số ngày lịch (từ bản nháp PRD đầu tiên tới khi PR cuối merge), số PR, tỉ lệ commit `fix` (`git log`), tỉ lệ `fix` theo hạng (`bun scripts/risk-tier.ts --fix-ratio`), số câu hỏi tới người dùng, và token agent.

**Token agent.** Báo cáo hoàn thành của mỗi agent có số token của agent đó. Cuối mỗi phase, phiên chính ghi tổng theo loại agent và theo hạng vào mục `## Verification` của PR phase, ví dụ `implementer h1: 210k; pr-reviewer h1: 45k`. Token của chính phiên chính không đo được nên không ghi.
