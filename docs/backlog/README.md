# Kế hoạch phát hành

Mỗi module một kế hoạch phát hành `PRDXX-<tên>.md`, viết từ `_template.md` sau khi PRD của module đã *Approved*. Nó cắt các story của PRD thành những bản giao `REL-n` demo được, theo thứ tự làm, kèm phụ thuộc và cỡ áo. Quy tắc chia bản giao và ước lượng: `docs/process/lifecycle.md § Kế hoạch phát hành`.

## Quy trình

1. **Soạn**: `product-manager` viết story map và cắt bản giao.
2. **Cỡ áo**: `tech-lead` ghi cỡ áo cho từng bản giao.
3. **Review**: `business-analyst` review.
4. **Duyệt**: người dùng (PO) duyệt, kể cả thứ tự bản giao. Dòng review và dòng duyệt trong *Lịch sử thay đổi* theo cùng quy tắc thứ tự như PRD (`docs/prd/README.md` § Quy trình, bước 4).

Sửa kế hoạch phát hành (đổi thứ tự, dời story) tăng phiên bản của chính nó và người dùng duyệt lại; PRD **không** về *In review*. Thêm hay đổi hành vi thì vẫn phải sửa PRD.

## Kiểm tra tự động

`scripts/check-release-plan.ts` chạy trong `scripts/test-gate.sh` và CI. Nó đối chiếu mỗi kế hoạch phát hành với PRD của nó (mã story và AC, tập AC trùng, cỡ áo, story Must chưa xếp khi cả hai đã *Approved*) và chặn kế hoạch sprint khi kế hoạch phát hành hoặc PRD chưa được duyệt.

## Danh mục

Chưa có kế hoạch phát hành nào.

| Mã | PRD | Trạng thái | Phiên bản | REL xong |
|---|---|---|---|---|
