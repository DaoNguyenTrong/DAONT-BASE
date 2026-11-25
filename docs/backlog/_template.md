# REL-XX — Kế hoạch phát hành <Tên module>

| Mục | Giá trị |
|---|---|
| Mã | `PRDXX-REL` |
| PRD | `PRDXX` v<phiên bản đã duyệt> |
| Trạng thái | Draft / In review / Approved |
| Phiên bản | 0.1 |
| Người duyệt | <người duyệt> |
| Cập nhật | YYYY-MM-DD |

## 1. Story map

Backbone là các hoạt động của persona chính, xếp theo thứ tự trải nghiệm; dưới mỗi hoạt động là các story của PRD.

1. <Hoạt động 1>
   - PRDXX-US-01 — <tên ngắn>
2. <Hoạt động 2>
   - PRDXX-US-02 — <tên ngắn>

## 2. Bản giao

- `REL-1` là bộ khung đi xuyên suốt (walking skeleton): luồng chính của persona chính chạy hết từ đầu đến cuối, dù còn mỏng.
- Bản giao nào cũng demo được cho người dùng thấy; không có bản giao chỉ gồm việc backend.
- Mỗi story Must nằm trong đúng một bản giao, hoặc được chia qua nhiều bản giao theo các tập AC không trùng nhau.
- Thứ tự bản giao là quyết định của người dùng.
- Cột *Story*: các mục cách nhau bằng `;`, mỗi mục là cả story (`PRDXX-US-01`) hoặc một phần theo tập AC (`PRDXX-US-02 (AC-03, AC-04)`). Cột *Cỡ áo*: `S`, `M`, `L` hoặc `XL`.

| REL | Mục tiêu | Story | Persona | Phụ thuộc | Cỡ áo |
|---|---|---|---|---|---|
| PRDXX-REL-1 | <kết quả demo được, nói theo người dùng> | PRDXX-US-01; PRDXX-US-02 (AC-03, AC-04) | P-01 | — | M |

## 3. Chưa xếp

Các story Could chưa vào bản giao nào.

- <PRDXX-US-nn — tên ngắn, hoặc "Không có">

## 4. Lịch sử thay đổi

Đổi thứ tự hay dời story tăng phiên bản của kế hoạch này và người dùng duyệt lại; PRD không về *In review*. Thêm hay đổi hành vi thì sửa PRD trước.

| Ngày | Phiên bản | Thay đổi | Nguồn |
|---|---|---|---|
| YYYY-MM-DD | 0.1 | Bản nháp đầu | — |
