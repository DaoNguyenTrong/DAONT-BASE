# Tiến độ module

Trạng thái từng giai đoạn của mỗi module (xem `lifecycle.md`). Giá trị: `—` chưa bắt đầu, `Draft`, `In review`, `Approved`, `Đang làm`, `Xong`. Cột *Merged* dùng `—`, `Đang làm`, `Xong`; hai cột *UI/Tech theo REL* và *REL xong* ghi theo `lifecycle.md` § Tiến độ module.

Trạng thái chi tiết của PRD nằm ở `docs/prd/README.md` và của tài liệu kỹ thuật ở `docs/tech/README.md`; bảng này chỉ tổng hợp. Phiên chính cập nhật nó (`lifecycle.md` § Tiến độ module).

| Module | PRD | Kế hoạch phát hành | Nền kiến trúc | UI/Tech theo REL | appsec | REL xong | Merged |
|---|---|---|---|---|---|---|---|

## Sprint

Mỗi sprint một dòng, ghi ở sprint retro (`lifecycle.md` § Vòng sprint).

| Cycle | Mục tiêu | Điểm cam kết | Điểm Done | Story dời | Story bị từ chối |
|---|---|---|---|---|---|

## Số đo

Ghi ở retro cuối module (`lifecycle.md` § Lỗi lặp và retro cuối module). Số ngày tính từ bản nháp PRD đầu tiên tới khi PR cuối của module merge; tỉ lệ `fix` là số commit `fix` trên tổng commit của các nhánh module; cột theo hạng lấy từ `bun scripts/risk-tier.ts --fix-ratio`.

| Module | Số ngày | Số PR | Tỉ lệ commit `fix` | `fix` theo hạng (0/1/2) | Câu hỏi tới người dùng | Token agent (nghìn) | Số sprint | Velocity TB | Ghi chú |
|---|---|---|---|---|---|---|---|---|---|

Chưa đăng ký module hoặc số đo theo quy trình mới. Không suy ra trạng thái Approved từ mã nền tảng hay kế hoạch cũ.
