# Tiến độ module

Trạng thái từng giai đoạn của mỗi module (xem `lifecycle.md`). Giá trị: `—` chưa bắt đầu, `Draft`, `In review`, `Approved`, `Đang làm`, `Xong`. Cột *Code* và *Merged* dùng `—`, `Đang làm`, `Xong`.

Trạng thái chi tiết của PRD nằm ở `docs/prd/README.md` và của tài liệu kỹ thuật ở `docs/tech/README.md`; bảng này chỉ tổng hợp. Phiên chính cập nhật nó (`lifecycle.md` § Tiến độ module).

| Module | PRD | UI | Tech | appsec | Plan | Code | Merged |
|---|---|---|---|---|---|---|---|

## Số đo

Ghi ở retro cuối module (`lifecycle.md` § Lỗi lặp và retro cuối module). Số ngày tính từ bản nháp PRD đầu tiên tới khi PR cuối của module merge; tỉ lệ `fix` là số commit `fix` trên tổng commit của các nhánh module; cột theo hạng lấy từ `bun scripts/risk-tier.ts --fix-ratio`.

| Module | Số ngày | Số PR | Tỉ lệ commit `fix` | `fix` theo hạng (0/1/2) | Câu hỏi tới người dùng | Token agent (nghìn) | Ghi chú |
|---|---|---|---|---|---|---|---|

Chưa đăng ký module hoặc số đo theo quy trình mới. Không suy ra trạng thái Approved từ mã nền tảng hay kế hoạch cũ.
