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

Muốn mở HDSD từ nút "?" trên header của app khi dev, chạy `dev` của site song song với `bun run --cwd frontend dev`: dev server của app chuyển `/guide` sang cổng 5174. Khi dev không có bước kiểm phiên.

`dev` và `build` chạy `scripts/sync-brand.ts` trước. Script này chép logo từ `frontend/public/` và sinh token màu từ `frontend/src/assets/styles/tailwind.css`, nên màu và logo của site luôn khớp với ứng dụng.

## File nào của ai

Base (repo này) và mỗi dự án fork cùng sửa site này. Để fork kéo cập nhật HDSD từ base về mà không bị conflict, mỗi file có một chủ:

| File                                                    | Chủ  | Ghi chú                                              |
| ------------------------------------------------------- | ---- | ---------------------------------------------------- |
| `site/.vitepress/product.ts`                            | fork | tên sản phẩm, đơn vị phát hành, logo, chương base ẩn |
| `site/.vitepress/sidebar.app.ts`                        | fork | chương của fork                                      |
| `site/.vitepress/permission-labels.app.ts`              | fork | tên các quyền của fork                               |
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

Trang có `permission: <mã quyền>` trong frontmatter sẽ tự hiện khung "Cần quyền: …" dưới tiêu đề. Mã quyền phải có trong `frontend/src/lib/permissions.ts`; tên hiển thị của quyền base nằm trong `site/.vitepress/theme/permission-labels.ts`, của quyền fork trong `site/.vitepress/permission-labels.app.ts`. App có thêm quyền mà chưa trang nào dùng thì không cần đặt tên.

Callout dùng cú pháp chuẩn `::: info`, `::: tip`, `::: warning`. Tiêu đề tự đổi theo ngôn ngữ của trang (Lưu ý / Mẹo / Quan trọng, Note / Tip / Important). Viết `::: tip Tiêu đề riêng` để đặt tiêu đề khác.

## Ảnh

- Đặt ở `src/<base|app>/images/<vi|en>/<chương>/<tên>.<đuôi>`, mỗi ngôn ngữ giao diện một bộ ảnh.
- Tham chiếu bằng đường dẫn tuyệt đối tính từ `src/`, ví dụ `![Màn hình chính](/base/images/vi/getting-started/overview.jpg)`. Đường dẫn không phụ thuộc trang nằm sâu bao nhiêu cấp.
- Ảnh có chú thích viết bằng `<figure>`, ảnh markdown, rồi `<figcaption>`.

## Chụp ảnh

Ảnh trong HDSD được chụp từ app thật bằng `frontend/user-guide/tools/` (Playwright runner của frontend). Mỗi chương có một file `tools/sections/<base|app>/<chương>.capture.ts`, và mỗi ảnh là một test cho mỗi ngôn ngữ.

```bash
bun run --cwd frontend guide:capture                                   # tất cả, cả hai ngôn ngữ
GUIDE_LOCALE=vi bun run --cwd frontend guide:capture                   # một ngôn ngữ
bun run --cwd frontend guide:capture -- sections/base/getting-started  # một chương
bun run --cwd frontend guide:capture -- --grep overview                # một ảnh
```

**Trước khi chạy:**

1. App và API đang chạy (mặc định `http://localhost:5175` và `http://localhost:7100`).
2. Chép `tools/.env.example` thành `tools/.env` và điền tài khoản dùng để chụp.
3. Tài khoản có sẵn dữ liệu mà ảnh cần. Công cụ không tự tạo dữ liệu; thiếu điều kiện nào thì nó dừng và liệt kê đủ các điều kiện còn thiếu:
   - là chủ (Owner) một tổ chức có ít nhất 2 thành viên và 1 vai trò tuỳ chỉnh;
   - có ít nhất 1 thông báo chưa đọc (ví dụ: một tài khoản khác thêm tài khoản này vào tổ chức của họ);
   - có ít nhất 2 phiên đăng nhập.

Dùng dữ liệu mẫu, không dùng dữ liệu thật của khách hàng. Tốt nhất là một database riêng cho việc chụp.

**Công cụ không ghi gì vào hệ thống:** mọi request tới `/api/` khác `GET` đều bị huỷ, trừ đăng nhập và làm mới phiên. Section nào lỡ bấm lưu, xoá hay "đánh dấu đã đọc" sẽ đỏ kèm danh sách request bị chặn. Hộp thoại chỉ mở để chụp rồi đóng, không bấm nút xác nhận.

**Một phiên cho cả lần chạy:** backend đổi refresh token sau mỗi lần làm mới, và dùng lại token cũ sẽ thu hồi cả phiên. Vì vậy công cụ dùng một browser context duy nhất, lưu phiên vào `tools/.auth/state.json` (gitignored) và chỉ đăng nhập lại khi phiên đã hết. Nhờ vậy số phiên trong ảnh "Phiên đăng nhập" không tăng sau mỗi lần chụp. Đăng nhập thất bại hoặc thiếu dữ liệu thì cả lần chạy dừng sau test đầu, không thử đăng nhập lại ở từng test (sai mật khẩu nhiều lần sẽ khoá tài khoản).

Mỗi test tải app một lần, mỗi lần tải làm mới phiên một lần, và API chỉ cho 60 lần làm mới mỗi phút từ một địa chỉ IP (`RateLimiterSettings:RefreshPermitLimit`). Chụp nhiều hơn khoảng 50 ảnh mỗi phút thì nâng giới hạn này trên hệ thống dùng để chụp.

Version và giờ build ở footer được ẩn khi chụp, để ảnh không đổi sau mỗi lần build.

Viết một section:

```ts
import { locales, test } from '../../fixtures'

for (const locale of locales) {
  test(`overview [${locale}]`, async ({ capture }) => {
    await capture.open(locale)
    await capture.annotate([
      { selector: 'aside nav', n: 1, at: 'top-right' },
      { selector: 'header .n-breadcrumb', n: 2, at: 'after' },
    ])
    await capture.blur(['[data-testid="app-header-user-menu"]'])
    await capture.shot({ locale, chapter: 'getting-started', name: 'overview' })
  })
}
```

- `open(locale, path?)`: mở app ở ngôn ngữ cần chụp, mỗi test chỉ tải app một lần.
- `annotate`: số tròn tím ở góc trên trái phần tử. `at: 'top-right'` đặt số ở góc trên phải, `at: 'after'` đặt ngay bên phải phần tử. Selector không tìm thấy, hoặc số rơi ra ngoài ảnh, thì test đỏ, để ảnh không lặng lẽ thiếu chú thích khi giao diện đổi. Số phải khớp bảng `# / Thành phần` của trang.
- `blur`: selector CSS của dữ liệu riêng tư (tên, email, IP, thiết bị) cần làm mờ trong ảnh kế tiếp.
- `shot`: ghi `site/src/<base|app>/images/<locale>/<chương>/<tên>.jpg`. Thêm `tree: 'app'` cho chương của fork, `clip: '<selector>'` để chỉ chụp một vùng.

## Ẩn một chương của base

Fork không cung cấp một tính năng của base thì liệt kê chương đó trong `product.hiddenBaseChapters`. Chương bị ẩn không được build và không có trong tìm kiếm.

- Chỉ ẩn được chương có `hideable: true` trong `sidebar.base.ts`. Chương nền tảng (Bắt đầu, Câu hỏi thường gặp, Thuật ngữ) không ẩn được; liệt kê chúng thì build báo lỗi.
- Vì vậy, một trang **không được** link sang chương `hideable` khác (link trong cùng chương thì được). Nếu không, ẩn chương đó sẽ để lại link chết. Test `content-integrity` chặn trường hợp này.

## Triển khai và quyền truy cập

Image `web` (`frontend/Dockerfile`) build site và phục vụ nó tại `/guide/` bằng chính nginx của app. Biến `GUIDE_PUBLIC` của service `web` (`docker-compose.prod.yml`, `.env.prod`) chọn chế độ:

| `GUIDE_PUBLIC`        | Chế độ  | Ai đọc được                                             |
| --------------------- | ------- | ------------------------------------------------------- |
| rỗng hoặc `false`     | private | người đã đăng nhập vào app (mặc định)                   |
| `true`                | public  | mọi người                                               |
| giá trị khác (`True`) | private | như mặc định, kèm một dòng cảnh báo trong log container |

`frontend/docker/41-guide-mode.sh` chọn snippet nginx (`guide-private.conf` hoặc `guide-public.conf`) lúc container khởi động. Đổi chế độ chỉ cần `docker compose ... up -d`, không phải build lại.

### Chế độ private hoạt động thế nào

- Với mỗi request vào `/guide/`, nginx hỏi API `GET /api/auth/check` bằng cookie `access_token` của trình duyệt (`auth_request`). API key không mở được HDSD.
- Chưa có phiên hoặc phiên đã hết: nginx chuyển tới `/#/guide-auth?next=<trang>`. App bắt đăng nhập nếu cần, kiểm lại phiên, rồi đưa người đọc về đúng trang. Trang đích chỉ được nằm trong `/guide/`; giá trị lạ thì về trang chủ HDSD.
- Cookie `access_token` sống 15 phút (`AccessTokenExpiryMinutes`). Đọc lâu hơn thế thì lần chuyển trang kế tiếp sẽ đi một vòng qua app: app làm mới phiên bằng refresh token và quay lại đúng trang, người đọc không phải đăng nhập lại khi refresh token còn hạn.
- Site cũng kiểm lại phiên trước mỗi lần chuyển trang phía client (`.vitepress/theme/auth-guard.ts`), vì VitePress không tải lại trang qua nginx sau lần đầu.
- API không trả lời: nginx trả trang lỗi tĩnh (`frontend/docker/guide-unavailable.html`, mã 500) thay vì nội dung.

**Ràng buộc:** chế độ private cần API **cùng origin** với app (`VITE_API_BASE_URL` để trống, mặc định của compose). API ở origin khác thì cookie không đi theo request `/guide/`, và không ai đọc được HDSD; khi đó dùng `GUIDE_PUBLIC=true`.

### Kiểm tra trước khi phát hành

`frontend/docker/verify-guide-access.sh` build image, chạy nó với một API giả và kiểm bằng `curl`:

- private: không phiên → 302 tới `/guide-auth`; có phiên → 200; `/guide` → 301; `next` bỏ query; CRLF mã hoá không chèn được header;
- API sập → trang lỗi, không lộ nội dung;
- public: không phiên → 200;
- `GUIDE_PUBLIC` sai → vẫn private và có log;
- app ở `/` vẫn chạy.

Script cần Docker, không chạy trong CI. Thêm hai bước kiểm tay trên một bản deploy thật:

1. Đăng xuất, mở `/guide/faq`, đăng nhập: bạn quay lại đúng `/guide/faq`.
2. Đổi app sang English rồi bấm nút "?": HDSD mở `/guide/en/`.

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
