# Hướng dẫn sử dụng (User guide)

Site hướng dẫn sử dụng cho người dùng cuối, dựng bằng [VitePress](https://vitepress.dev) và phục vụ tại `/guide/` cùng origin với ứng dụng. Nội dung có hai ngôn ngữ: tiếng Việt (gốc) và tiếng Anh (`/guide/en/`).

Thiết kế đầy đủ nằm trong spec `docs/superpowers/specs/2026-10-15-user-guide-design.md` (nếu repo của bạn còn giữ). README này là phần bạn cần khi viết và bảo trì nội dung.

## Chạy và build

```bash
bun install --cwd frontend/user-guide/site        # một lần
bun run --cwd frontend/user-guide/site dev        # http://localhost:5174/guide/
bun run --cwd frontend/user-guide/site test       # unit test + kiểm tra toàn vẹn nội dung
bun run --cwd frontend/user-guide/site build      # ra dist/, báo lỗi khi có link chết
bun run --cwd frontend/user-guide/site preview    # xem bản build
bun run --cwd frontend/user-guide/site format     # prettier
```

`dev` và `build` chạy `scripts/sync-brand.ts` trước. Script này chép logo từ `frontend/public/` và sinh token màu từ `frontend/src/assets/styles/tailwind.css`, nên màu và logo của site luôn khớp với ứng dụng.

## File nào của ai

Base (repo này) và mỗi dự án fork cùng sửa site này. Để fork kéo cập nhật HDSD từ base về mà không bị conflict, mỗi file có một chủ:

| File                                                    | Chủ  | Ghi chú                                              |
| ------------------------------------------------------- | ---- | ---------------------------------------------------- |
| `site/.vitepress/product.ts`                            | fork | tên sản phẩm, đơn vị phát hành, logo, chương base ẩn |
| `site/.vitepress/sidebar.app.ts`                        | fork | chương của fork                                      |
| `site/src/index.md`, `site/src/en/index.md`             | fork | trang chủ                                            |
| `site/src/app/**`                                       | fork | nội dung và ảnh của fork                             |
| `site/.vitepress/sidebar.base.ts`, `site/src/base/**`   | base | chương và nội dung của base                          |
| `site/.vitepress/theme/**`, `config.mts`, các file khác | base | theme, cấu hình, script, test                        |

Fork không sửa file của base. Một thay đổi ở đó đi về base trước, rồi fork kéo về.

## Thêm một trang

1. Tạo trang cho cả hai ngôn ngữ:

   ```bash
   bun run --cwd frontend/user-guide/site new-page organizations/add-member        # base
   bun run --cwd frontend/user-guide/site new-page orders/create --app             # fork
   ```

   Script tạo `src/<base|app>/<chương>/<trang>.md` và `src/<base|app>/en/<chương>/<trang>.md` từ `src/_template.md`, rồi in đoạn cần dán vào `sidebar.base.ts` (hoặc `sidebar.app.ts`). Trang đã tồn tại thì script dừng và không ghi gì.

2. Dán đoạn đó vào mục lục và điền nhãn `vi`/`en`. Slug `index` là trang đầu của chương. Chương không có `items` là một trang đơn (`<chương>.md`).
3. Viết nội dung theo template và style guide bên dưới, cho cả hai ngôn ngữ.
4. `bun run --cwd frontend/user-guide/site test`: test sẽ báo nếu thiếu một ngôn ngữ, trang chưa có trong mục lục, hay ảnh không tồn tại.

Hai ngôn ngữ dùng **cùng slug** (tiếng Anh). Nhờ vậy nút chọn ngôn ngữ đưa người đọc tới đúng trang tương ứng.

## Style guide

- Xưng "bạn" (`vi`) / "you" (`en`). Câu ngắn, thể mệnh lệnh ở phần các bước.
- Nhãn giao diện in đậm và **phải khớp đúng chữ** trong `frontend/src/locales/{vi,en}.ts`. Đổi nhãn trong app thì sửa HDSD trong cùng PR.
- Không nhắc tên component, route hay API. Đó là thông tin cho người viết, không cho người đọc.
- Thuật ngữ dùng đúng như trang Thuật ngữ; thuật ngữ mới thì thêm vào đó trước.
- Ảnh: alt mô tả nội dung ảnh; số chú thích trên ảnh khớp bảng "# / Thành phần"; không dùng dữ liệu thật của khách hàng.
- Sửa một ngôn ngữ thì sửa cả hai trong cùng commit.

Mỗi trang tác vụ theo khung của `src/_template.md`: mục đích, quyền cần có (frontmatter `permission`), trước khi bắt đầu, các bước, kết quả, sự cố thường gặp. Placeholder trong template là chữ thường hoặc comment HTML; đừng viết placeholder dạng `<chữ>`, vì markdown sẽ coi đó là thẻ HTML và trang không build được.

Trang có `permission: <mã quyền>` trong frontmatter sẽ tự hiện khung "Cần quyền: …" dưới tiêu đề. Mã quyền phải có trong `frontend/src/lib/permissions.ts`; tên hiển thị nằm trong `site/.vitepress/theme/permission-labels.ts`.

Callout dùng cú pháp chuẩn `::: info`, `::: tip`, `::: warning`. Tiêu đề tự đổi theo ngôn ngữ của trang (Lưu ý / Mẹo / Quan trọng, Note / Tip / Important). Viết `::: tip Tiêu đề riêng` để đặt tiêu đề khác.

## Ảnh

- Đặt ở `src/<base|app>/images/<vi|en>/<chương>/<tên>.<đuôi>`, mỗi ngôn ngữ giao diện một bộ ảnh.
- Tham chiếu bằng đường dẫn tuyệt đối tính từ `src/`, ví dụ `![Màn hình chính](/base/images/vi/getting-started/overview.webp)`. Đường dẫn không phụ thuộc trang nằm sâu bao nhiêu cấp.
- Ảnh có chú thích viết bằng `<figure>`, ảnh markdown, rồi `<figcaption>`.

## Ẩn một chương của base

Fork không cung cấp một tính năng của base thì liệt kê chương đó trong `product.hiddenBaseChapters`. Chương bị ẩn không được build và không có trong tìm kiếm.

- Chỉ ẩn được chương có `hideable: true` trong `sidebar.base.ts`. Chương nền tảng (Bắt đầu, Câu hỏi thường gặp, Thuật ngữ) không ẩn được; liệt kê chúng thì build báo lỗi.
- Vì vậy, một trang **không được** link sang chương `hideable` khác (link trong cùng chương thì được). Nếu không, ẩn chương đó sẽ để lại link chết. Test `content-integrity` chặn trường hợp này.

## Test làm gì

| Test                               | Kiểm tra                                                                                                                                                                    |
| ---------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `content-integrity.test.ts`        | đủ hai ngôn ngữ, mọi trang có trong mục lục và ngược lại, có `title`/`description`, ảnh tồn tại và có alt, mã quyền hợp lệ, không trùng URL, không link sang chương ẩn được |
| `brand-tokens.test.ts`             | sinh đủ token màu từ `tailwind.css`; báo tên token bị thiếu                                                                                                                 |
| `sidebar.test.ts`, `paths.test.ts` | ghép mục lục base/fork, ẩn chương, đường dẫn                                                                                                                                |
| `markdown.test.ts`                 | tiêu đề callout và nhãn permalink theo ngôn ngữ, bảng cuộn ngang                                                                                                            |
| `new-page.test.ts`                 | tạo đủ hai file, không ghi đè                                                                                                                                               |

Mỗi lỗi in ra danh sách đường dẫn vi phạm. Ảnh có trong `images/` mà không trang nào dùng chỉ in cảnh báo.

CI chạy cùng các lệnh này trong job `test / guide`.
