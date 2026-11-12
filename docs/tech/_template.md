# TECH-XX — <Tên module>

| Mục | Giá trị |
|---|---|
| Mã | `TECHXX` |
| PRD | `PRDXX` (phiên bản đã duyệt) |
| Thiết kế giao diện | <link canvas và tên các bảng> |
| Trạng thái | Draft / In review / Approved |
| Phiên bản | 0.1 |
| Cần appsec review | Có / Không (module nhạy cảm: định danh, token, cookie, phiên, link riêng, khoá công khai) |
| Người duyệt | <người duyệt> |
| Cập nhật | YYYY-MM-DD |
| Phụ thuộc | TECH-00, … |
| Kế hoạch triển khai | <đường dẫn, khi đã có> |

## 1. Phạm vi và truy vết

Các mã FR/AC mà tài liệu này bao phủ, và những gì cố ý chưa làm. Bảng truy vết ở mục 13.

## 2. Kiến trúc

Thành phần mới hoặc thay đổi, vị trí trong các tầng (Domain → Application → Infrastructure → API; frontend), luồng dữ liệu chính. Có sơ đồ khi giúp ích.

## 3. Mô hình dữ liệu

Entity và trường (tên, kiểu, bắt buộc, giới hạn), quan hệ, ràng buộc và chỉ mục, thuộc tổ chức nào (`IHasOrganization`), quy tắc xoá hoặc vô hiệu, dữ liệu cần giữ lịch sử.

## 4. Hợp đồng API

| Mã | Phương thức và route | Mô tả | Yêu cầu | Phản hồi | Lỗi | Chính sách truy cập |
|---|---|---|---|---|---|---|
| | | | | | | |

Kèm phân trang, giới hạn tần suất, quy ước lỗi (`ProblemDetails`), thay đổi hợp đồng cần xuất lại OpenAPI.

## 5. Phân quyền

Từng endpoint và từng truy vấn danh sách: ai được gọi, quyền nào, kiểm tra ở đâu, phạm vi theo tổ chức và theo sản phẩm được áp ở phía máy chủ như thế nào. Trường hợp bị từ chối trả mã nào.

## 6. Luồng chính

Sơ đồ tuần tự hoặc các bước cho những luồng khó hoặc nhạy cảm. Gồm cả nhánh lỗi và hết hạn.

## 7. Bảo mật và quyền riêng tư

Tài sản cần bảo vệ, mô hình đe doạ ngắn (kẻ tấn công, cách tấn công, biện pháp), dữ liệu nhạy cảm và cách che hoặc xoá, những gì không được ghi log.

## 8. Giao diện: ghép với thiết kế

Màn hình và trạng thái nào dùng API nào, trạng thái phía máy khách (store), cách dùng token và component của design system, i18n (vi, en), xử lý tải, trống, lỗi, quyền.

## 9. Hiệu năng và quy mô

Khối lượng dự kiến (ví dụ 20 sản phẩm, số feedback), truy vấn nặng và chỉ mục, tác vụ nền, giới hạn, kích thước tệp.

## 10. Di chuyển dữ liệu và triển khai

Migration, thứ tự triển khai các phần, cờ tính năng, tương thích ngược, cách quay lại.

## 11. Chiến lược kiểm thử

Với từng AC: kiểm thử ở tầng nào (Domain, Application, Infrastructure, API, frontend, e2e), dữ liệu và công cụ cần có. Theo `.claude/rules/testing.md`.

## 12. Quyết định, rủi ro, phương án đã loại

| Quyết định | Vì sao | Phương án đã loại và lý do | Phạm vi (Module / Nền tảng) |
|---|---|---|---|

Đây là nơi duy nhất ghi quyết định của module, được duyệt cùng tài liệu. Quyết định *Nền tảng* (xuyên suốt, không thuộc riêng module) được `implementer` thêm vào `.claude/decisions.md` trong PR triển khai nó.

## 13. Truy vết

| Mã PRD | Phần thiết kế đáp ứng | Test |
|---|---|---|
| PRDXX-FR-01 | mục 3, 4 | AC-01 |

## 14. Câu hỏi mở

| # | Câu hỏi | Đề xuất trả lời | Ảnh hưởng | Trạng thái |
|---|---|---|---|---|

## 15. Lịch sử thay đổi

| Ngày | Phiên bản | Thay đổi | Nguồn |
|---|---|---|---|
| YYYY-MM-DD | 0.1 | Bản nháp đầu | — |
