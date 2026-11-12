# PRD-XX — <Tên module>

| Mục | Giá trị |
|---|---|
| Mã | `PRDXX` (tiền tố cho mọi mã trong tài liệu này) |
| Trạng thái | Draft / In review / Approved |
| Phiên bản | 0.1 |
| Người duyệt | <người duyệt> |
| Cập nhật | YYYY-MM-DD |
| Phụ thuộc | PRD-00, … |
| Thiết kế | <link canvas và tên các bảng, khi đã thiết kế> |
| Kế hoạch | <đường dẫn kế hoạch triển khai, khi đã có> |

## 1. Bối cảnh và vấn đề

Hiện đang khó khăn ở đâu, với ai, bằng chứng là gì. Một đến hai đoạn ngắn.

## 2. Mục tiêu, không phải mục tiêu, phản mục tiêu

- **Mục tiêu**: kết quả module phải mang lại.
- **Không phải mục tiêu**: những gì cố ý không làm ở phiên bản này (và sẽ làm ở đâu).
- **Phản mục tiêu**: kết quả bị coi là thất bại dù mọi yêu cầu đều đạt.
- **Đo thành công**: biết bằng cách nào là đã thành công; chỉ số dùng chung thì dẫn về PRD-00 §2; định nghĩa và cách tính nằm ở PRD của module sở hữu chỉ số trong danh mục.

## 3. Người dùng và vai trò

Persona nào trong `00-personas.md` dùng module này (ghi mã `P-nn`, đánh dấu persona chính của module), vai trò nào trong PRD-00 §Vai trò họ giữ, và điều gì là riêng ở module này. Không định nghĩa lại persona hay vai trò.

## 4. User story

Cột *Persona* bắt buộc, chứa một hoặc nhiều mã `P-nn` có trong `00-personas.md` (`scripts/check-personas.ts` kiểm).

| Mã | Persona | Là… | Tôi muốn… | Để… | Ưu tiên |
|---|---|---|---|---|---|
| PRDXX-US-01 | P-01 | | | | Must / Should / Could |

## 5. Yêu cầu chức năng

| Mã | Yêu cầu | Story | Ưu tiên |
|---|---|---|---|
| PRDXX-FR-01 | Hệ thống phải … | US-01 | Must / Should / Could |

## 6. Quy tắc nghiệp vụ

| Mã | Quy tắc | Áp dụng cho |
|---|---|---|
| PRDXX-BR-01 | | FR-01 |

## 7. Tiêu chí nghiệm thu

Mỗi tiêu chí là một hành vi quan sát được. Bao gồm trường hợp chuẩn, trống, lỗi, giới hạn và không có quyền. Mỗi FR có ít nhất một tiêu chí.

| Mã | Yêu cầu | Cho trước (Given) | Khi (When) | Thì (Then) |
|---|---|---|---|---|
| PRDXX-AC-01 | FR-01 | | | |

## 8. Trạng thái và trường hợp biên

Trạng thái trống, giới hạn và khối lượng lớn (ví dụ 20 sản phẩm, nội dung dài), sửa đồng thời, quyền truy cập hết hạn hoặc bị thu hồi, bản ghi liên quan bị xoá hoặc vô hiệu.

## 9. Dữ liệu (mức nghiệp vụ)

Các trường người dùng thấy hoặc nhập: tên, ý nghĩa, bắt buộc, giới hạn, ai được sửa. Không mô tả cách lưu trữ.

| Trường | Ý nghĩa | Bắt buộc | Giới hạn | Ai được sửa |
|---|---|---|---|---|

## 10. Thông báo và sự kiện

| Sự kiện | Ai nhận | Kênh | Ghi chú |
|---|---|---|---|

## 11. Yêu cầu phi chức năng

Bảo mật và quyền riêng tư, kỳ vọng hiệu năng, trợ năng, ngôn ngữ (vi/en), khả năng truy vết — diễn đạt theo góc nhìn sản phẩm.

## 12. Phụ thuộc

PRD khác, tính năng nền tảng có sẵn, dịch vụ bên ngoài.

### Chuyển cho PRD khác

Quy tắc hoặc trường hợp biên thuộc module khác, phát hiện khi soạn PRD này. PRD đích quy định chi tiết; ở đây chỉ ghi để không bị quên.

| PRD đích | Điều cần quy định | Phát sinh từ |
|---|---|---|
| PRDXX | | |

## 13. Câu hỏi mở

Chỉ những điều người dùng phải quyết: làm đổi thứ được xây, chạm bảo mật, tách dữ liệu, quyền, hoặc tốn công đáng kể.

| # | Câu hỏi | Đề xuất trả lời | Vì sao quan trọng | Trạng thái |
|---|---|---|---|---|
| 1 | | | | Mở / Đã trả lời (ngày) |

### Mặc định đề xuất

Các điểm `product-manager` đã viết vào PRD theo đề xuất, kể cả mức ưu tiên. Người dùng soát cả bảng một lần khi duyệt; điểm nào bị bác thì thành câu hỏi mở.

| # | Mặc định | Mã bị ảnh hưởng | Lý do |
|---|---|---|---|
| D1 | | | |

## 14. Lịch sử thay đổi

| Ngày | Phiên bản | Thay đổi | Nguồn |
|---|---|---|---|
| YYYY-MM-DD | 0.1 | Bản nháp đầu | — |
