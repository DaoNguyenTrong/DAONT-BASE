# Bối cảnh hệ thống

DAONT-BASE là nền tảng khởi đầu. Tệp này giữ bối cảnh để `solution-architect` và `tech-lead` chọn giải pháp đúng quy mô; việc nhập quy trình không xác nhận yêu cầu sản phẩm, tải, SLA hay cam kết pháp lý.

Giá trị **[Người dùng]** là đã được xác nhận; **[Đề xuất]** là giả định được ghi rõ để xét duyệt. **Chưa xác định** không phải một giả định đã được duyệt: dùng dữ liệu hiện có để làm việc độc lập, chuyển các câu hỏi ảnh hưởng quyết định qua `solution-architect` theo `AGENTS.md`.

## Người dùng

Chưa xác định sản phẩm, nhóm người dùng, vai trò và thiết bị mục tiêu. Nền tảng hiện hỗ trợ tiếng Việt và tiếng Anh; xem `.claude/rules/localization.md`.

## Quy mô

Chưa xác định số tổ chức, tài khoản, người dùng đồng thời, lưu lượng và tăng trưởng dữ liệu. Không dùng số liệu của FEEDBACK-HUB làm giới hạn DAONT-BASE.

## Triển khai và vận hành

Các khả năng hiện có được mô tả trong `.claude/decisions.md`, `.claude/rules/` và cấu hình triển khai của repo. Số máy API, provider cache, lưu trữ, mục tiêu sẵn sàng, sao lưu, RPO và RTO của sản phẩm chưa được chốt. Không suy ra yêu cầu vận hành từ cấu hình mẫu.

## Hiệu năng

Chưa có mục tiêu thời gian đáp ứng hoặc ngân sách dung lượng được xác nhận cho sản phẩm.

## Chi phí

Chưa có ngân sách hay dịch vụ trả phí mới được duyệt. Tiền, giấy phép và dịch vụ trả phí cần người dùng quyết theo `.claude/agents/solution-architect.md`.

## Dữ liệu và quyền riêng tư

Phân loại dữ liệu, thời gian lưu giữ và yêu cầu pháp lý của sản phẩm chưa được chốt. Các quy tắc xác thực, phân quyền và tách tổ chức hiện có vẫn áp dụng; xem `.claude/rules/authentication.md`.

## Thiết kế giao diện

Áp dụng quy tắc thiết kế trong `.claude/agents/ui-designer.md`. Canvas của DAONT-BASE chưa được thiết lập; khi được duyệt, ghi liên kết trong PRD. Canvas của FEEDBACK-HUB không phải nguồn thiết kế cho repo này.

## Lịch sử

| Ngày | Thay đổi | Nguồn |
|---|---|---|
| 2026-11-12 | Tạo khung bối cảnh khi thay toàn bộ quy trình bằng quy trình FEEDBACK-HUB; chưa xác nhận yêu cầu sản phẩm | Lệnh của người dùng |
