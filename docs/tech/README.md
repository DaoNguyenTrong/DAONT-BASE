# Thiết kế kỹ thuật

Mỗi module một tài liệu, viết từ `_template.md` bởi agent `tech-lead`, sau khi PRD của module đã *Approved* và thiết kế giao diện đã duyệt. Tài liệu này trả lời "làm theo kiến trúc nào"; việc chia thành task nhỏ là của kế hoạch triển khai (`docs/superpowers/plans/`).

## Quy trình

1. **Soạn**: `tech-lead` soạn từ PRD, thiết kế giao diện và code hiện có; trả về câu hỏi mở, câu hỏi cho PRD và các quyết định cần ghi.
2. **Câu hỏi nghiệp vụ** quay về `product-manager` để sửa PRD, không quyết ở đây.
3. **Review bảo mật**: module nhạy cảm (định danh, token, cookie, phiên, link riêng, khoá công khai, mọi thứ lộ ra ẩn danh) qua `appsec` trước khi duyệt.
4. **Duyệt**: hết câu hỏi mở thì `tech-lead` đặt trạng thái *In review*; người dùng duyệt; các quyết định trong §12 được duyệt cùng tài liệu, không cần duyệt riêng. Phiên chính commit bản duyệt lên nhánh tài liệu của module, cùng PR nháp với PRD (`docs/process/lifecycle.md` § Commit tài liệu). Sửa tài liệu do lệch lớn khi triển khai (`docs/process/lifecycle.md` § Lệch thiết kế khi triển khai) đi chung PR với code.
5. **Kế hoạch triển khai**: `writing-plans` chia thành task, dẫn chiếu mã FR/AC của PRD và các mục của tài liệu này.

Ngoại lệ về thứ tự: module nhạy cảm có thể viết tài liệu kỹ thuật song song với thiết kế giao diện khi người dùng cho phép và phạm vi ngoại lệ được ghi trong tài liệu module.

Mọi thay đổi trên tài liệu đã duyệt đưa nó về *In review*.

## Ràng buộc đã biết

`tech-lead` đối chiếu code và `.claude/rules/` ở đầu mỗi module, ghi ràng buộc và cách giải quyết trong tài liệu của module. Không kế thừa các ràng buộc widget hoặc kết luận TECH00 của FEEDBACK-HUB như tính năng đã có ở DAONT-BASE.

| Mục | Ràng buộc hiện tại | Vì sao va chạm | Module |
|---|---|---|---|

## Danh mục

Chưa có tài liệu kỹ thuật module theo quy trình mới.

| Mã | Module | PRD | Trạng thái | Phiên bản | appsec |
|---|---|---|---|---|---|
