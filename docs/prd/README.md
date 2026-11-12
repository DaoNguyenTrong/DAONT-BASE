# Tài liệu yêu cầu sản phẩm (PRD)

Mỗi module một PRD, viết từ `_template.md` bởi agent `product-manager`. Thuật ngữ, vai trò, ma trận quyền và vòng đời trạng thái chỉ định nghĩa ở PRD-00; các PRD khác dẫn chiếu về đó.

## Quy trình

1. **Soạn**: `product-manager` soạn bản nháp đầu từ các quyết định đã có. Mỗi PRD chỉ quyết phần của module mình; điều thuộc module khác ghi vào §12 *Chuyển cho PRD khác*. Chỗ hở nào người dùng phải quyết thì thành câu hỏi mở (tối đa khoảng 8 câu mỗi lượt); chỗ còn lại agent viết theo đề xuất và liệt kê ở bảng *Mặc định đề xuất* (`.claude/agents/product-manager.md` quy tắc 5, 7, 8).
2. **Review sớm**: `business-analyst` review ngay bản nháp đầu, trước khi người dùng nhận lượt câu hỏi đầu tiên, để người dùng nhận một danh sách câu hỏi gộp thay vì nhiều lượt nhỏ. Mỗi báo cáo review được phiên chính đăng thành comment trên PR nháp tài liệu của module (`docs/process/lifecycle.md` § Commit tài liệu) và đưa lại cho BA ở lần review sau.
3. **Trả lời câu hỏi**: phiên chính luôn chuyển các câu BA đánh dấu là mặc định thành *Mặc định đề xuất* (không hỏi), rồi gửi phần còn lại của `product-manager` và BA cho `solution-architect` trả lời thay người dùng. Câu trả lời của agent này ghi nguồn "SA (thay người dùng)". Chỉ những câu nó chuyển lên mới thành một lượt hỏi người dùng, tối đa khoảng 8 câu; nếu vượt mức đó, giữ các câu quan trọng nhất và để phần còn lại cho lượt sau. Câu trả lời nào chạm bảo mật, tách dữ liệu giữa tổ chức, hoặc làm phạm vi phình lớn thì phiên chính trình ngay hệ quả và phương án khác **trước khi** chuyển cho agent viết. Lặp đến khi hết câu hỏi mở.
4. **Review cuối và duyệt**: hết câu hỏi mở thì `product-manager` đặt trạng thái *In review*; `business-analyst` review lại; người dùng duyệt PRD cùng bảng *Mặc định đề xuất* (gồm cả mức ưu tiên). Trạng thái thành *Approved*; phiên chính commit bản duyệt lên nhánh tài liệu của module; PR nháp của module merge một lần khi cả PRD và tài liệu kỹ thuật đã duyệt (`docs/process/lifecycle.md` § Commit tài liệu). Sau dòng người dùng duyệt, lịch sử thay đổi của phiên bản đó chỉ được thêm dòng sửa liên kết hoặc metadata, với cột *Nguồn* ghi đúng `metadata`; `scripts/check-prd-approval.ts` kiểm thứ tự này khi merge.
5. **Thiết kế**: `ui-designer` thiết kế theo PRD đã duyệt, ghi mã yêu cầu lên từng bảng. Câu hỏi nghiệp vụ phát sinh khi thiết kế quay lại sửa PRD.
6. **Thiết kế kỹ thuật**: `tech-lead` viết tài liệu trong `docs/tech/` từ PRD và thiết kế đã duyệt. Câu hỏi nghiệp vụ phát sinh quay lại sửa PRD.
7. **Kế hoạch và triển khai**: kế hoạch triển khai dẫn chiếu mã FR/AC của PRD và các mục của tài liệu kỹ thuật; feature-map cập nhật cùng PR; `pr-reviewer` kiểm tra rồi merge vào `dev`.

Mọi thay đổi trên PRD đã duyệt đưa PRD về *In review*.

## Danh mục

Chưa đăng ký module theo quy trình mới. DAONT-BASE là nền tảng khởi đầu; danh mục và thứ tự module của sản phẩm sử dụng nền tảng được chốt ở giai đoạn 0, không kế thừa các module hoặc trạng thái duyệt của FEEDBACK-HUB.

| Mã | PRD | Trạng thái | Phiên bản | Phụ thuộc | Thiết kế |
|---|---|---|---|---|---|

Canvas thiết kế chưa được thiết lập cho DAONT-BASE. Khi có thiết kế đã duyệt, ghi liên kết ở cột *Thiết kế* và trong PRD tương ứng.
