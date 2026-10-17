# Thiết kế: Hướng dẫn sử dụng trực tuyến (HDSD) cho DAONT-BASE

- **Ngày:** 2026-10-15 · **Sửa lần 1:** 2026-10-17
- **Trạng thái:** Đã duyệt (2026-10-17)
- **Nguồn tham chiếu:** `CIMP-VBI/frontend/user-guide/`, bản triển khai HDSD đang chạy production của dự án fork CIMP-VBI (chỉ có tiếng Việt).

**Thay đổi ở lần sửa 1** (sau khi đối chiếu với code hiện tại):

- Sửa các chỗ mâu thuẫn với code: scheme xác thực của `/api/auth/check`, vị trí `map` của nginx, D3/D6 đã xong, vị trí file trong image, `.dockerignore` loại `*.md`, slug khác nhau giữa hai locale làm hỏng nút chuyển ngôn ngữ, hành vi khi API sập.
- Bổ sung CI, lefthook, phạm vi loại trừ cho Tailwind/vite, giới hạn 15 phút của cookie, công cụ chụp ảnh chạy trên Playwright test runner và chặn mọi request ghi.
- Thêm phần "chuẩn tái sử dụng": tách cây nội dung base/fork, token màu sinh tự động, nội dung viết theo tác vụ, template trang, style guide, thuật ngữ, script tạo trang, chia giai đoạn.

## 1. Mục tiêu

Base chưa có tài liệu cho người dùng cuối. Bản thiết kế này thêm một **site HDSD dựng bằng VitePress**, phục vụ tại `/guide/` cùng origin với ứng dụng, đồng thời đặt ra **chuẩn viết HDSD** để mọi dự án fork từ base dùng lại.

Site gồm:

- trang chủ kiểu vuejs.org: hero, lưới thẻ tính năng, dải "bắt đầu nhanh";
- nội dung **tiếng Việt và tiếng Anh**, viết theo tác vụ người dùng, cho các tính năng sẵn có của base;
- màu và logo lấy tự động từ ứng dụng;
- công cụ chụp ảnh minh hoạ tự động bằng Playwright.

**Tiêu chí thành công**

1. `bun run build` trong `frontend/user-guide/site` thành công, không có link chết ở cả hai ngôn ngữ. Test của site xanh: mọi trang có đủ hai ngôn ngữ, mọi trang nằm trong mục lục, mọi ảnh được tham chiếu đều tồn tại.
2. CI chạy build và test của site trên mỗi PR (job `test / guide`).
3. Image frontend phục vụ `/guide/`:
   - Chế độ mặc định (`GUIDE_PUBLIC=false`): người chưa đăng nhập bị đưa qua luồng đăng nhập của app rồi quay về đúng trang đang xem. API sập thì hiện trang báo lỗi tĩnh, không lộ nội dung.
   - Chế độ `GUIDE_PUBLIC=true`: ai cũng đọc được, kể cả khi API sập.
4. Màu của site được **sinh** từ `frontend/src/assets/styles/tailwind.css` lúc build, nên không thể lệch màu với app.
5. `bun run --cwd frontend guide:capture` chụp lại toàn bộ ảnh của cả hai ngôn ngữ. Mọi request ghi (khác `GET`) tới `/api/` đều bị chặn ở tầng mạng, trừ đăng nhập và làm mới phiên (§10).
6. Một fork thêm chương mới mà không phải sửa file nào thuộc base (§4).

**Ngoài phạm vi**

- Bản Word/PDF. Fork nào cần thì thêm pipeline docx sau, theo mẫu `build_docx.py` của CIMP.
- Seed dữ liệu demo tự động.
- Chụp ảnh trong CI.
- Nội dung của các tính năng chưa có trong base.

## 2. Các quyết định đã chốt

| Câu hỏi | Quyết định | Phương án bị loại |
|---|---|---|
| Nguồn nội dung | Markdown nhiều trang viết thẳng cho VitePress | Một file `.md` gốc sinh cả site lẫn docx như CIMP: cần generator khoảng 285 dòng và cú pháp riêng `{%…%}` |
| Kiến trúc | Gói VitePress riêng, chuyển từ CIMP sang và bỏ phần đặc thù CIMP | Nhúng vào SPA thành route Vue: mất SSG và tìm kiếm local, bundle app nặng thêm, và ở chế độ public HDSD sẽ phụ thuộc vào việc app tải được. (Ở chế độ private, HDSD vẫn cần API để kiểm phiên; xem §9.3.) |
| Vị trí | `frontend/user-guide/`, đi cùng image `web` | Thư mục top-level `user-guide/`: tách được cache Docker (xem §12), nhưng thêm một cây thứ tư vào monorepo và một build context riêng |
| Quyền truy cập | Mặc định phải đăng nhập (nginx `auth_request`), tắt được bằng `GUIDE_PUBLIC=true`. API lỗi thì từ chối (fail-closed) | Luôn công khai; hoặc luôn bắt đăng nhập mà không có cờ; hoặc cho qua khi API lỗi (fail-open) |
| Ngôn ngữ | `vi` (locale gốc) + `en` (`/en/`) ngay từ đầu, **dùng chung slug tiếng Anh** | Slug riêng theo ngôn ngữ (`bat-dau` / `getting-started`): nút chuyển ngôn ngữ của VitePress giữ nguyên path nên sẽ dẫn tới 404, và test parity phải có bảng ánh xạ |
| Cấu trúc nội dung | Theo **tác vụ người dùng** (Diátaxis how-to): mỗi trang là một việc cần làm | Theo màn hình/component: người đọc phải biết tên màn hình trước mới tìm được |
| Mở rộng cho fork | Hai cây nguồn `base/` và `app/`, gộp URL bằng `rewrites`; mục lục ghép từ hai file | Fork sửa thẳng nội dung của base: conflict mỗi lần kéo cập nhật từ base |
| Màu thương hiệu | Script sinh token từ `tailwind.css` lúc dev/build | Chép hex vào `brand.css` và dùng test để chặn lệch: trùng lặp, và fork phải sửa hai nơi |
| Ảnh minh hoạ | Chụp tự động bằng Playwright test runner, theo từng ngôn ngữ giao diện, có chặn request ghi | Chụp tay; hoặc chưa có ảnh; hoặc CLI tự viết như `capture.mjs` của CIMP |

## 3. Cấu trúc

```
frontend/user-guide/
├── README.md                       cách viết (style guide), thêm trang, chụp ảnh, build, triển khai
├── site/                           gói VitePress riêng: package.json + bun.lock riêng
│   ├── package.json                vitepress 1.6.x, @fontsource/roboto; scripts: dev/build/preview/test/new-page
│   ├── .vitepress/
│   │   ├── config.mts              base '/guide/', srcDir 'src', cleanUrls, rewrites, locales, search, markdown
│   │   ├── product.ts              [fork] tên sản phẩm, đơn vị phát hành, tên file logo, chương base bị ẩn
│   │   ├── sidebar.base.ts         [base] mục lục các chương của base
│   │   ├── sidebar.app.ts          [fork] mục lục các chương của fork (rỗng trong base)
│   │   ├── sidebar.ts              [base] ghép hai file trên, sinh sidebar cho vi và en
│   │   └── theme/
│   │       ├── index.ts            [base] extends theme-without-fonts; slot thương hiệu, badge version, auth guard
│   │       ├── auth-guard.ts       [base] kiểm phiên khi chuyển trang phía client; chỉ bật ở chế độ private
│   │       ├── brand.css           [base] ánh xạ --brand-* sang --vp-c-*
│   │       ├── brand-tokens.generated.css   sinh bởi sync-brand (gitignore)
│   │       ├── custom.css          [base] bố cục: thanh bên, bảng, hình, callout, trang chủ
│   │       └── components/         [base] HomeQuickStart.vue, PermissionNote.vue
│   ├── scripts/
│   │   ├── sync-brand.ts           [base] chép logo và sinh token màu trước dev/build
│   │   └── new-page.ts             [base] tạo trang mới cho cả hai ngôn ngữ từ template
│   ├── tests/                      [base] bun test: content-integrity, brand-tokens
│   └── src/
│       ├── index.md, en/index.md   [fork] trang chủ (base có sẵn bản mặc định)
│       ├── _template.md            [base] template trang, loại khỏi build (srcExclude)
│       ├── base/                   [base] nội dung của base
│       │   ├── getting-started/ account/ organizations/ notifications/ faq.md glossary.md
│       │   ├── en/                 cùng cấu trúc, cùng slug
│       │   └── images/{vi,en}/<chương>/<ảnh>.jpg
│       └── app/                    [fork] nội dung của fork, cùng bố cục với base/ (rỗng trong base)
└── tools/                          công cụ chụp ảnh (§10), chạy bằng Playwright của frontend
    ├── playwright.capture.config.ts
    ├── auth.setup.ts               fixture phiên phạm vi worker: một context, lưu storageState
    ├── fixtures.ts                 đổi locale, chặn request ghi, annotate, blur, shot
    ├── preconditions.ts            globalSetup: kiểm dữ liệu mẫu
    └── sections/
        ├── base/                   getting-started.capture.ts · account.capture.ts · …
        └── app/                    [fork]
```

**Quy ước sở hữu:** file đánh dấu `[fork]` là nơi fork được sửa. File `[base]` fork không sửa; mọi thay đổi ở đó đi về base trước rồi fork kéo về. Quy ước này được ghi trong README và `RENAMING.md`.

## 4. Mở rộng cho fork

### 4.1 Hai cây nguồn, một cây URL

`config.mts` khai `rewrites` để bỏ tiền tố `base/` và `app/`:

```ts
rewrites: {
  'base/:slug*': ':slug*',
  'app/:slug*': ':slug*',
}
```

Ví dụ: `src/base/en/organizations/add-member.md` được phục vụ tại `/guide/en/organizations/add-member` (`cleanUrls: true`, nên URL không có `.html`; nginx phục vụ bằng `try_files $uri $uri.html …`).

- Link giữa các trang viết theo đường dẫn **sau rewrite** (`../organizations/add-member`). Vì rewrite chỉ bỏ tiền tố, link trong cùng một cây hay giữa hai cây đều viết như nhau.
- Ảnh viết theo đường dẫn tuyệt đối tính từ `srcDir`: `/base/images/vi/organizations/add-member.jpg`. Ảnh không bị rewrite, và đường dẫn không phụ thuộc độ sâu của trang. Phải xác nhận bằng build ở giai đoạn 1 (§14); nếu VitePress không xử lý được đường dẫn tuyệt đối thì chuyển sang đường dẫn tương đối và để `new-page` sinh sẵn.
- Test `content-integrity` báo lỗi khi hai trang ở `base/` và `app/` ra cùng một URL.

### 4.2 Mục lục

`sidebar.base.ts` và `sidebar.app.ts` khai mục lục một lần cho cả hai ngôn ngữ:

```ts
export const baseChapters: Chapter[] = [
  {
    slug: 'organizations',
    text: { vi: 'Tổ chức', en: 'Organizations' },
    items: [{ slug: 'add-member', text: { vi: 'Thêm thành viên', en: 'Add a member' } }],
  },
]
```

`sidebar.ts` ghép `baseChapters` (đã bỏ các chương trong `product.hiddenBaseChapters`) với `appChapters`, rồi sinh `sidebar.vi` và `sidebar.en`. Hai ngôn ngữ có cùng cấu trúc ngay từ cách khai báo, nên không cần test đối chiếu số chương/số mục. Nhãn thiếu một ngôn ngữ là lỗi kiểu TypeScript.

Chương bị ẩn cũng bị đưa vào `srcExclude`, nên không bị build và không lọt vào chỉ mục tìm kiếm.

**Link vào chương có thể bị ẩn:** nếu một trang khác của base (FAQ, Thuật ngữ, Bắt đầu…) link vào chương bị ẩn, bước kiểm dead link của VitePress sẽ làm build đỏ. Quy tắc:

- `sidebar.base.ts` đánh dấu chương nào ẩn được (`hideable: true`). Chương nền tảng như `getting-started`, `faq`, `glossary` không ẩn được; `hiddenBaseChapters` chứa chương không ẩn được thì `sidebar.ts` ném lỗi.
- Test `content-integrity` cấm link từ chương này sang một chương `hideable` khác. Link trong cùng một chương thì được phép.

### 4.3 `product.ts`

```ts
export const product = {
  name: 'StarterKit',
  publisher: '…',
  brand: {
    wordmarkLight: 'weatherplus-wordmark-light.webp',
    wordmarkDark: 'weatherplus-wordmark-dark.webp',
    mark: 'weatherplus-mark.webp',
  },
  hiddenBaseChapters: [] as string[],
}
```

- Tên file logo là tên trong `frontend/public/`. Fork đổi logo của app là đổi luôn logo của HDSD, chỉ cần sửa tên ở đây nếu tên file khác.
- Version lấy bằng `git describe --tags --always`, cùng cách `resolveAppVersion()` trong `frontend/vite.config.ts`. Không đọc được thì là `'unknown'`.
- `config.mts`, theme và trang chủ đều đọc từ đây; không còn hằng `CIMP` hay `WeatherPlus` hardcode trong file `[base]`.

### 4.4 Việc của một fork

1. Sửa `product.ts`.
2. Sửa trang chủ `src/index.md`, `src/en/index.md` (thẻ tính năng, bắt đầu nhanh).
3. Với mỗi trang mới: `bun run new-page <chương>/<slug> --app`, viết nội dung, thêm mục vào `sidebar.app.ts`, viết section chụp ảnh trong `tools/sections/app/`.

Màu không phải sửa (sinh từ `tailwind.css`), logo không phải chép (lấy từ `frontend/public/`).

### 4.5 `new-page.ts`

`bun run new-page organizations/add-member [--app]`:

- tạo `src/{base|app}/organizations/add-member.md` và `src/{base|app}/en/organizations/add-member.md` từ `_template.md`;
- tạo `tools/sections/{base|app}/organizations.capture.ts` nếu chưa có;
- in ra đoạn cần dán vào `sidebar.{base|app}.ts`. Script không tự sửa file TypeScript.

Trang đã tồn tại thì dừng, không ghi đè.

## 5. Thương hiệu và trang chủ

### 5.1 Token màu

`scripts/sync-brand.ts` chạy trước `dev` và `build`, làm hai việc:

1. Chép ba file logo khai trong `product.brand` từ `frontend/public/` vào `src/public/brand/` (gitignore). Thiếu file nguồn thì dừng kèm thông báo.
2. Đọc `frontend/src/assets/styles/tailwind.css` và sinh `theme/brand-tokens.generated.css` (gitignore):
   - `--brand-violet-50…950` từ `--color-accent-*`;
   - `--brand-leaf-50…950` từ `--color-leaf-*`;
   - `--brand-surface-900/950` từ `--color-surface-*`;
   - `--brand-primary-200-dark` từ `--color-primary-200` trong khối dark. Giá trị là `var(...)` thì phân giải về hex.

   Thiếu bậc nào thì script dừng và in tên token còn thiếu.

`brand.css` chỉ ánh xạ, không chứa hex:

| Chế độ | Biến | Giá trị |
|---|---|---|
| Sáng | `--vp-c-brand-1/2/3` | violet-600 / 700 / 500 |
| Sáng | `--vp-sidebar-bg-color` | violet-900, chữ sáng như sidebar của app |
| Tối | `--vp-c-brand-1` | `--brand-primary-200-dark` |
| Tối | Nền | `--brand-surface-950` / `--brand-surface-900`, như `naive-theme.ts` |

- Nút hành động chính trên trang chủ dùng màu lá: nền leaf-400, chữ violet-950.
- Font: Roboto tự host (`@fontsource/roboto` 400/500/700), dùng `vitepress/theme-without-fonts`, không gọi CDN.
- **Test `brand-tokens`:** chạy generator trên một `tailwind.css` mẫu và trên file thật. File thật phải sinh đủ mọi bậc, nên app đổi tên token là test đỏ.

### 5.2 Trang chủ (`src/index.md`, `src/en/index.md`)

Dùng `layout: home` của VitePress, tuỳ biến bằng CSS và slot:

1. **Hero** căn giữa, kiểu vuejs.org.
   - **Nền:** `radial-gradient` sáng ở góc phải trên, chồng lên `linear-gradient(135deg, violet-950 → violet-800 → violet-700)`. Hero ở chế độ sáng cũng dùng nền tối này.
   - **Thương hiệu:** wordmark trắng, rồi dòng `name` "Hướng dẫn sử dụng ‹product.name›" (chữ lớn, phần tên sản phẩm tô gradient lá → tím nhạt).
   - **Chữ:** `tagline` một câu.
   - **Nút `actions`:**
     - **Bắt đầu**: kiểu brand, màu lá, dẫn tới `/getting-started/` (hoặc `/en/getting-started/`).
     - **Mở ứng dụng**: kiểu alt có viền, `link: '../'`, `target: '_self'` để thoát khỏi base `/guide/`.
2. **`features`**: 6 thẻ, lưới 3 cột trên desktop, 1 cột trên điện thoại.
   - **Thẻ:** Đăng nhập & tài khoản · Hồ sơ & bảo mật · Tổ chức · Thành viên & vai trò · Thông báo · Giao diện & ngôn ngữ.
   - **Nội dung mỗi thẻ:** icon SVG inline một màu (`currentColor` = violet-600), `title`, `details` 1–2 câu, `link`, `linkText: 'Xem hướng dẫn'`.
   - **Kiểu dáng:** viền violet-100, khi hover nâng nhẹ và viền chuyển sang violet-300.
3. **`HomeQuickStart.vue`**, chèn qua slot `home-features-after`.
   - **Bố cục:** dải 3 bước đánh số (1 → 2 → 3) nối bằng đường kẻ: Đăng nhập → Chọn tổ chức → Cập nhật hồ sơ.
   - **Mỗi bước:** ảnh thu nhỏ, lấy lại từ ảnh của trang tương ứng, và link.
   - **Ngôn ngữ:** chữ lấy từ `frontmatter.quickStart` của từng `index.md`, nên component không giữ chuỗi nào.
4. Footer: "© ‹publisher› · Phiên bản ‹version›".

Thanh điều hướng của mọi trang:

- khối thương hiệu (wordmark màu ở chế độ sáng, trắng ở chế độ tối, kèm dòng phụ "Hướng dẫn sử dụng ‹name›");
- badge version;
- link "Mở ứng dụng";
- nút chuyển `vi`/`en` (locale switcher mặc định của VitePress, giữ nguyên trang đang xem nhờ dùng chung slug);
- ô tìm kiếm;
- nút sáng/tối.

### 5.3 Trang nội dung

Lấy lại phần CSS đã ổn định của CIMP, đổi prefix `cimp-` thành `guide-`:

- Bảng bọc trong `<div class="guide-table">`, rộng 100% và cuộn ngang khi cần. Dùng renderer `table_open`/`table_close` trong `config.mts`.
- Hình có chú thích đặt dưới hình, viết bằng `<figure>` HTML trong Markdown (không dùng cú pháp riêng): `![alt](/base/images/vi/…/x.jpg)` + `<figcaption>`.
- Callout dùng cú pháp chuẩn `::: info` / `::: tip` / `::: warning`. Tiêu đề lấy từ `locales[*].themeConfig`: `vi` là Lưu ý / Mẹo / Quan trọng, `en` là Note / Tip / Important.
- Accordion cho sidebar: chuyển `sidebar-accordion.ts` từ CIMP sang, chỉ mở một chương mỗi lần.
- Permalink tiêu đề dùng chữ đã render (renderer `anchor.permalink` của CIMP), để `aria-label` không lộ cú pháp nguồn.
- **`PermissionNote.vue`**: trang có frontmatter `permission: organizations.members.manage` thì theme tự chèn khung "Cần quyền: Quản lý thành viên" dưới tiêu đề. Tên quyền theo từng ngôn ngữ nằm trong một bảng của theme.

## 6. Ngôn ngữ và tìm kiếm

- `locales.root` (`lang: 'vi-VN'`, `label: 'Tiếng Việt'`) và `locales.en` (`lang: 'en-US'`, `link: '/en/'`). Mỗi locale có `themeConfig` riêng gồm `nav`, `sidebar`, `outline.label`, `docFooter`, nhãn sáng/tối, `notFound` và nhãn callout.
- **Slug:** tiếng Anh, giống nhau ở hai locale. Trang `vi` và trang `en` tương ứng chỉ khác tiền tố `en/`.
- Tìm kiếm `provider: 'local'`:
  - `processTerm` bỏ dấu tiếng Việt (NFD + bỏ dấu kết hợp + `đ`→`d`); hàm phải tự chứa, không tham chiếu biến ngoài.
  - `locales.root.translations` tiếng Việt.
  - `locales.en` giữ mặc định.
- **Test `content-integrity`:**
  - Mỗi file `.md` dưới `src/base/` và `src/app/` (trừ `en/`) phải có file cùng đường dẫn dưới `en/`, và ngược lại.
  - Mỗi trang phải nằm trong `sidebar.ts` (sau khi ẩn chương), và mỗi mục sidebar phải có file ở cả hai ngôn ngữ. Không có trang mồ côi.
  - Mọi ảnh được tham chiếu phải tồn tại. Ảnh có trong `images/` mà không trang nào dùng thì in cảnh báo.
  - Mọi ảnh phải có alt khác rỗng.
  - Giá trị `permission` trong frontmatter phải có trong `frontend/src/lib/permissions.ts`.
  - Không có hai trang trùng URL sau rewrite.

## 7. Chuẩn nội dung

### 7.1 Template trang (`src/_template.md`)

```markdown
---
title: 'Động từ + đối tượng, ví dụ: Thêm thành viên vào tổ chức'
description: 'Một câu: trang này giúp làm gì. Dùng cho tìm kiếm và thẻ meta.'
# permission: organizations.members.manage  (bỏ dấu # nếu cần quyền; xoá dòng nếu ai cũng làm được)
---

# Động từ + đối tượng

Một đoạn: khi nào cần làm việc này và kết quả là gì.

## Trước khi bắt đầu

- Điều kiện: đã đăng nhập, đã chọn đúng tổ chức, …

## Các bước

1. Một hành động mỗi bước. Nhãn trên giao diện in đậm, ví dụ bấm **Thêm thành viên**.
2. …

<!-- Ảnh: <figure> + ![alt](/base/images/vi/<chương>/<tên>.jpg) + <figcaption> -->

| #   | Thành phần |
| --- | ---------- |
| 1   | …          |

## Kết quả

Người dùng thấy gì khi làm xong.

## Sự cố thường gặp

- **Triệu chứng**: nguyên nhân và cách xử lý.
```

Placeholder phải là chữ thường hoặc nằm trong comment HTML, không viết dạng `<chữ>`. Lý do: một trang mới tạo từ template phải build được ngay. YAML không chấp nhận giá trị chứa `: ` mà không có nháy, còn `<title>` trong markdown sẽ thành thẻ HTML thật và làm trình biên dịch Vue báo lỗi.

- `_template.md` nằm trong `srcExclude` và được test `content-integrity` bỏ qua.
- Trang khái niệm (FAQ, Thuật ngữ, Giới thiệu) không bắt buộc theo template nhưng vẫn phải có `title` và `description`.

### 7.2 Style guide (ghi trong README)

- Xưng "bạn" (`vi`) / "you" (`en`). Câu ngắn, thể mệnh lệnh ở phần các bước.
- Nhãn giao diện in đậm và **phải khớp đúng chữ** trong `frontend/src/locales/{vi,en}.ts`. Đổi nhãn trong app thì sửa HDSD trong cùng PR.
- Không nhắc tên component, route hay API. Đó là thông tin cho người viết, không cho người đọc.
- Thuật ngữ dùng đúng như trang Thuật ngữ; thuật ngữ mới thì thêm vào đó trước.
- Ảnh: alt mô tả nội dung ảnh; số chú thích trên ảnh khớp bảng "# / Thành phần"; không dùng dữ liệu thật của khách hàng.
- Sửa một ngôn ngữ thì sửa cả hai trong cùng commit.

### 7.3 Trang Thuật ngữ (`glossary.md`)

Bảng song ngữ các khái niệm của base: Tổ chức / Organization, Thành viên / Member, Vai trò / Role, Quyền / Permission, Chủ sở hữu / Owner, Quản trị / Admin, Phiên đăng nhập / Session, Thông báo đẩy / Push notification. Mỗi khái niệm một câu định nghĩa. Thuật ngữ phải trùng với chữ đang dùng trong `locales/{vi,en}.ts`.

## 8. Nội dung ban đầu

Mỗi dòng con là một trang theo template §7.1. Cột "Quyền" lấy từ `frontend/src/lib/permissions.ts`; tác vụ không ghi quyền thì mọi thành viên làm được. Quyền của tác vụ "Tạo tổ chức" phải xác minh với backend khi viết.

| Chương (`slug`) | Trang (`slug`) | Quyền | Màn hình (ghi chú cho người viết) |
|---|---|---|---|
| Bắt đầu (`getting-started`) | Giới thiệu (`index`) · Đăng ký tài khoản (`register`) · Xác minh email (`verify-email`) · Đăng nhập (`sign-in`; mật khẩu, Google, Microsoft) · Đăng xuất (`sign-out`) · Làm quen giao diện (`interface`; sidebar, header, sáng/tối, ngôn ngữ, cỡ chữ) | — | `RegisterView`, `VerifyEmailView`, `LoginView`, `AppLayout`, `AppControls` |
| Tài khoản (`account`) | Cập nhật thông tin cá nhân (`profile`) · Đổi mật khẩu (`change-password`) · Đăng xuất khỏi thiết bị khác (`sessions`) · Bật thông báo đẩy (`push-notifications`) | — | `ProfileDialog` |
| Tổ chức (`organizations`) | Chuyển tổ chức (`switch`) · Tạo tổ chức (`create`) · Sửa thông tin tổ chức (`edit`) · Thêm thành viên (`add-member`) · Tạm khoá hoặc xoá thành viên (`manage-members`) · Tạo và sửa vai trò (`roles`) | `organizations.manage` (sửa) · `organizations.members.manage` (thành viên) · `organizations.roles.manage` (vai trò) | `OrganizationSwitcher`, `OrganizationsView`, `OrganizationMembersDialog`, `OrganizationRolesDialog` |
| Thông báo (`notifications`) | Xem và đánh dấu đã đọc (`index`) | — | `NotificationBell` |
| Câu hỏi thường gặp (`faq`) | — | — | — |
| Thuật ngữ (`glossary`) | — | — | — |

- Mục Đăng nhập Google/Microsoft ghi rõ là "chỉ hiện khi quản trị đã cấu hình". Điều kiện này khớp với cờ client-id trong runtime config.
- Trang "Tạo và sửa vai trò" ghi rõ: vai trò hệ thống (Owner, Admin, Member) không sửa và không xoá được; Owner luôn có mọi quyền.

## 9. Lớp đăng nhập và triển khai

### 9.1 Backend

- Thêm `GET /api/auth/check` vào `AuthController`, trả `204` hoặc `401`:

  ```csharp
  [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
  [HttpGet("check")]
  public IActionResult Check() => NoContent();
  ```

  - **Phải chỉ định scheme.** Default policy của base (`AuthExtensions.cs`) chấp nhận cả JwtBearer lẫn `ApiKey`; `[Authorize]` trần như bản CIMP sẽ trả 204 cho request chỉ có `X-Api-Key` hợp lệ. Endpoint này chỉ chấp nhận phiên người dùng.
  - Chỉ xác thực JWT (đọc từ cookie `access_token` qua `OnMessageReceived` sẵn có), không truy vấn DB.
  - `UserTimeZoneMiddleware` miễn `/api/auth/check`: middleware này trả 400 cho mọi `/api/*` thiếu `X-TimeZone`, mà subrequest của nginx chỉ mang `Cookie`.
- Regen `shared/openapi/openapi.json` rồi chạy `bun run --cwd frontend codegen`.
- Test API:
  - Có cookie hợp lệ → 204.
  - Thiếu cookie → 401.
  - Chỉ có `X-Api-Key` hợp lệ → 401.
  - Cookie hợp lệ nhưng không có `X-TimeZone` → 204.

### 9.2 Ứng dụng (`frontend/src`)

- **`safeGuideTarget(next)`** đặt trong `lib/safe-redirect.ts`, cạnh `safeRedirectTarget`, cùng một mẫu kiểm tra. Hàm chỉ chấp nhận `/guide` hoặc `/guide/…`, và từ chối `//`, `\`, `..` cùng ký tự điều khiển. Mọi giá trị khác trả về `/guide/`.
- **Route `/guide-auth`** (`GuideAuthView`, `meta.requiresAuth`):
  - Chưa đăng nhập: router guard sẵn có chuyển tới `login` với `redirect` trỏ ngược lại `/guide-auth?next=…`.
  - Đã có phiên (khôi phục lúc khởi động hoặc vừa đăng nhập): hỏi lại `GET /api/auth/check` **cùng origin** (đường dẫn tương đối, chỉ cookie) — đúng điều nginx sẽ kiểm. 204 thì `window.location.replace(safeGuideTarget(next))`; mọi kết quả khác hiện thông báo lỗi kèm nút về trang chủ, thay vì chuyển tiếp, để không lặp nginx → màn này → nginx khi cookie không dùng được.
  - Route này không nằm trong sidebar.
- **Ràng buộc triển khai:** chế độ private cần API cùng origin với app (`VITE_API_BASE_URL` để trống, mặc định của compose). API ở origin khác thì cookie `access_token` không đi theo yêu cầu `/guide/`; khi đó dùng `GUIDE_PUBLIC=true`. README ghi rõ.
- **Lối vào HDSD:** nút icon "?" (tooltip "Hướng dẫn sử dụng", có `aria-label`) trong `AppHeader`, mở `/guide/` ở tab mới (`rel="noopener"`). Locale `en` mở `/guide/en/`. Chuỗi i18n được thêm vào cả `locales/vi.ts` và `en.ts`.
- **`vite.config.ts`:**
  - khi chạy dev, `server.proxy['/guide']` trỏ tới `http://localhost:5174`. Dev không có bước kiểm phiên;
  - thêm `'**/user-guide/**'` vào `server.watch.ignored`. Pattern hiện có `**/docs/.vitepress/**` không khớp thư mục này.
- **`tailwind.css`:** thêm `@source not "../../../user-guide";`. Hiện file chỉ có `@import 'tailwindcss'` nên Tailwind tự quét cả `frontend/user-guide/`.

### 9.3 nginx và chế độ truy cập

File mới nằm cạnh entrypoint hiện có trong `frontend/docker/`.

- **`frontend/nginx.conf`:**
  - Thêm ở đầu file (ngữ cảnh `http`, cạnh `map $http_upgrade`):

    ```nginx
    map $request_uri $guide_next {
        default /guide/;
        ~^(?<guide_path>[^?]*) $guide_path;
    }
    ```

    `map` không được phép đặt trong `server` hay trong snippet được include vào `server`; CIMP cũng đặt nó ở đầu `nginx.conf`. Dùng `$request_uri` thô và bỏ query để chặn CRLF injection.
  - Trong khối `server`: `include /etc/nginx/snippets/guide.conf;`.
- **`frontend/docker/guide-private.conf`**, lấy từ CIMP:
  - `location = /guide` → 301 về `/guide/`.
  - `location /guide/`:
    - `auth_request /_guide_auth`;
    - `error_page 401 = @guide_login`;
    - `error_page 500 502 503 504 /_guide_unavailable.html`: khi API không trả lời, `auth_request` thành 500, người dùng thấy trang báo lỗi tĩnh thay vì nội dung (fail-closed);
    - `error_page 404 /guide/404.html`;
    - HTML: `Cache-Control: private, no-cache`;
    - `try_files $uri $uri.html $uri/ =404`.
  - `location /guide/assets/` **lồng bên trong** `location /guide/`, nên kế thừa `auth_request` và các `error_page`: `Cache-Control: private, max-age=31536000, immutable` (tên file có hash). Nếu đặt thành location ngang hàng thì phải khai lại `auth_request` và `error_page 401`, vì location anh em không kế thừa nhau.
  - `location = /_guide_auth` (internal):
    - `proxy_pass http://api:8080/api/auth/check`;
    - bỏ body;
    - chỉ chuyển `Cookie`;
    - `X-Api-Key ""`.
  - `location = /_guide_unavailable.html` (internal): trang tĩnh song ngữ, nằm ngoài `/guide/` nên không vòng lại `auth_request`.
  - `@guide_login` → `302 /#/guide-auth?next=$guide_next`.
  - `location = /guide/_mode` → `return 200 'private'`.
- **`frontend/docker/guide-public.conf`:** giống bản private nhưng không có `auth_request`, `@guide_login` và trang lỗi. HTML `no-cache`, `/guide/assets/` `public, max-age=31536000, immutable`, `_mode` trả `public`.
- **`frontend/docker/41-guide-mode.sh`** (chạy sau `40-write-runtime-config.sh`): nếu `GUIDE_PUBLIC=true` thì chép snippet public đè lên `/etc/nginx/snippets/guide.conf`. Giá trị khác `true`/`false`/rỗng thì ghi log và giữ private.
- **Giới hạn 15 phút của cookie:** cookie `access_token` hết hạn cùng token (`AccessTokenExpiryMinutes` = 15). Đọc HDSD quá 15 phút thì lần tải trang kế tiếp sẽ đi vòng `/#/guide-auth` → app làm mới phiên bằng refresh token → quay lại đúng trang. Người dùng thấy một lần chuyển trang, không phải đăng nhập lại khi refresh token còn hạn. README ghi rõ hành vi này.
- **`auth-guard.ts`** (phía client, chỉ chạy ở bản build):
  - Khi khởi động, `fetch('/guide/_mode')` một lần. Kết quả khác `private` thì không cài guard.
  - Kết quả `private` thì cài guard theo cách của CIMP:
    - bỏ qua lần `router.go()` đầu tiên;
    - trước mỗi lần chuyển trang gọi `/api/auth/check`;
    - nhận `401` thì tải hẳn trang đích để nginx đưa người dùng qua luồng đăng nhập;
    - lỗi mạng thì cho qua (trang đích tải lại vẫn qua `auth_request`).
- `docker-compose.prod.yml`: thêm `GUIDE_PUBLIC: ${GUIDE_PUBLIC:-false}` cho service `web`. `.env.prod.example` ghi chú biến này.

### 9.4 Build image

- `frontend/Dockerfile` thêm stage `guide` **kế thừa stage `build`**, để dùng lại git, `.git` và version như footer của app:

  ```dockerfile
  FROM build AS guide
  WORKDIR /build/frontend/user-guide/site
  RUN bun install --frozen-lockfile && bun run build
  ```

  Không dùng `FROM oven/bun:1` trần: thiếu git, version sẽ là `unknown`.
- Stage `final`:
  - `COPY --from=guide /build/frontend/user-guide/site/dist /usr/share/nginx/html/guide`;
  - chép hai snippet vào `/etc/nginx/snippets-available/`, **và** chép snippet private vào `/etc/nginx/snippets/guide.conf`. Image mặc định đã private, không phụ thuộc vào việc script entrypoint có chạy;
  - chép `guide-unavailable.html` vào `/usr/share/nginx/html/_guide_unavailable.html`;
  - chép `41-guide-mode.sh` vào `/docker-entrypoint.d/`.
- `.dockerignore`:
  - hiện có `**/*.md`, sẽ loại toàn bộ nội dung HDSD. Thêm ngoại lệ `!frontend/user-guide/site/src/**/*.md` **ngay sau** dòng `**/*.md` (dòng sau thắng dòng trước);
  - loại `frontend/user-guide/tools/`, `**/user-guide/site/src/public/brand/`, `**/.vitepress/cache/`, `**/brand-tokens.generated.css`.
- `.gitignore`: thêm `frontend/user-guide/site/{.vitepress/cache,src/public/brand}/`, `**/brand-tokens.generated.css`, `frontend/user-guide/tools/.auth/`. (`dist/` và `bun.lock` đã được xử lý bởi luật hiện có.)

### 9.5 CI và hook

- **`.github/workflows/test.yml`**, job mới `guide`:
  - checkout `fetch-depth: 0` (version cần tag);
  - setup Bun cùng phiên bản với job `frontend`;
  - `bun install --cwd frontend/user-guide/site --frozen-lockfile`;
  - kiểm định dạng bằng prettier: site khai `prettier` `3.8.4` trong `devDependencies` (cùng bản với frontend) và dùng `.prettierrc.json` của frontend, để CI và lefthook không lấy một bản `bunx` khác;
  - `bun run --cwd frontend/user-guide/site test`;
  - `bun run --cwd frontend/user-guide/site build`.
- `ci.yml` và `CONTRIBUTING.md`: thêm `test / guide` vào danh sách status check bắt buộc trên `main`.
- `scripts/test-gate.sh`: chạy thêm test và build của site.
- `lefthook.yml`: thêm glob prettier cho `frontend/user-guide/site/**/*.{ts,mts,vue,css,md}` và `frontend/user-guide/tools/**/*.ts`.

### 9.6 Tài liệu repo

- `RENAMING.md`: thêm `frontend/user-guide/site/.vitepress/product.ts`, trang chủ `src/index.md`, `src/en/index.md`, và quy ước sở hữu `[base]`/`[fork]`.
- `README.md` (gốc): thêm mục "Hướng dẫn sử dụng" trỏ tới `frontend/user-guide/README.md`.
- `.claude/rules/commands.md`: thêm lệnh dev/build/test/new-page của site và lệnh chụp ảnh.

## 10. Công cụ chụp ảnh (`tools/`)

Chạy bằng Playwright test runner của frontend (`@playwright/test` 1.61.1 đã có), không có `package.json` riêng:

```
bun run --cwd frontend guide:capture                       # tất cả, cả hai ngôn ngữ
GUIDE_LOCALE=vi bun run --cwd frontend guide:capture       # một ngôn ngữ
bun run --cwd frontend guide:capture -- sections/base/organizations   # một chương
bun run --cwd frontend guide:capture -- --grep add-member  # một ảnh
```

`frontend/package.json` thêm script `"guide:capture": "playwright test -c user-guide/tools/playwright.capture.config.ts"`.

- **Ràng buộc về phiên:** backend xoay vòng refresh token ở mỗi lần `POST /api/auth/refresh` (app gọi khi khởi động qua `restoreSession`). Token cũ bị dùng lại ngoài khoảng ân hạn `RefreshTokenReuseGraceSeconds` thì **cả họ token bị thu hồi** (`AuthService.ResolveInactiveRefreshTokenAsync`). Vì vậy không được để nhiều browser context cùng nạp một file `storageState`: context đầu tiên tiêu token, các context sau dùng lại token đã thu hồi và làm mất phiên.
- **`playwright.capture.config.ts`:**
  - một project duy nhất, `workers: 1`, `fullyParallel: false`; viewport 1440×900, `deviceScaleFactor: 1`;
  - `globalSetup: preconditions.ts`;
  - `baseURL` từ `GUIDE_BASE_URL` (mặc định `http://localhost:5173`).
- **`auth.setup.ts` → fixture `session` phạm vi worker:** tạo **một** browser context cho cả lần chạy, nạp `tools/.auth/state.json` nếu có. Mở app không thấy app shell thì đăng nhập bằng `login()` của `frontend/e2e/fixtures.ts`. Hết lần chạy thì ghi lại `context.storageState()` để lần sau dùng refresh token mới nhất. Mỗi lần đăng nhập tạo một phiên mới trong DB và các phiên này hiện trong ảnh "Phiên đăng nhập", nên chỉ đăng nhập khi state hết hạn.
- **Ngôn ngữ:** mỗi section lặp qua `GUIDE_LOCALE` (`vi`, `en` hoặc cả hai; mặc định cả hai) **trong cùng context**: đặt `localStorage['app-locale']` rồi tải lại trang (`useLocaleStore` chỉ đọc localStorage, không cần thao tác UI). Tên test có hậu tố `[vi]`/`[en]` để `--grep` chọn được.
- **`fixtures.ts`:**
  - **chặn ghi:** `page.route('**/api/**')` huỷ mọi request khác `GET`, trừ `POST /api/auth/login` và `POST /api/auth/refresh`. Fixture ghi lại từng request bị huỷ (method + URL) và ở teardown `expect(blocked).toEqual([])`, nên section nào lỡ bấm lưu, xoá, khoá hay đánh dấu đã đọc thì test đỏ kèm danh sách request. Đây là cơ chế bảo đảm tiêu chí 5, thay cho danh sách selector cấm của CIMP;
  - `annotate(items)`: vẽ overlay số tròn tím (violet-600), chữ trắng, theo selector. Selector không tìm thấy phần tử thì **test đỏ**, để ảnh không lặng lẽ thiếu chú thích khi giao diện đổi;
  - `blur(selectors)`: làm mờ email, IP, tên thiết bị;
  - `shot(name, { chapter, clip? })`: ghi `site/src/{base|app}/images/<locale>/<chapter>/<name>.jpg` (JPEG q82). Ảnh xuất thẳng JPEG nên không cần `sharp`.
- **Quy ước cho section:** hộp thoại chỉ mở để chụp rồi đóng; không bấm nút xác nhận.
- **`preconditions.ts`:** kiểm bằng request `GET`:
  - tài khoản là Owner của ít nhất một tổ chức;
  - tổ chức có ≥2 thành viên và ≥1 vai trò tuỳ chỉnh;
  - có ≥1 thông báo chưa đọc;
  - có ≥2 phiên đăng nhập.

  Thiếu điều kiện nào thì dừng và in ra điều kiện còn thiếu. Script không tự tạo dữ liệu.
- **Biến môi trường:** `GUIDE_BASE_URL`, `GUIDE_USERNAME`, `GUIDE_PASSWORD`, `GUIDE_LOCALE=vi|en|all` (mặc định `all`).
- `tsconfig.e2e.json` thêm `user-guide/tools/**/*.ts` để type-check cùng e2e.

## 11. Kiểm chứng

| Lớp | Kiểm tra | Lệnh |
|---|---|---|
| Site | Build xanh, VitePress báo dead link ở cả hai locale | `bun run --cwd frontend/user-guide/site build` |
| Site | `content-integrity` (§6), `brand-tokens` (§5.1), `new-page` (tạo đủ hai file, không ghi đè) | `bun run --cwd frontend/user-guide/site test` |
| CI | Job `test / guide` chạy các lệnh trên | `.github/workflows/test.yml` |
| Backend | Test API cho `/api/auth/check` (3 ca, gồm ca chỉ có `X-Api-Key`) | `backend/scripts/test.sh` |
| Frontend | `safeGuideTarget` (đường dẫn hợp lệ, `//evil`, `/guide/../x`, `https://…`, CRLF, giá trị không phải chuỗi) · `GuideAuthView` (đã đăng nhập → replace; chưa → login có redirect) · nút "?" trong `AppHeader` mở đúng URL theo locale | `bun run --cwd frontend test:run` |
| Image | `docker build` thành công; `/usr/share/nginx/html/guide/` có đủ trang của cả hai ngôn ngữ (kiểm `.dockerignore`) | checklist trong README |
| Tích hợp (thủ công) | `docker compose -f docker-compose.prod.yml up --build`: (1) chưa đăng nhập mở `/guide/organizations/` → về login → đăng nhập bằng mật khẩu, và lặp lại với Google/Microsoft nếu đã cấu hình → quay lại đúng trang; (2) `GUIDE_PUBLIC=true` → 200 không cần đăng nhập, chuyển trang không reload; (3) `/guide/_mode` trả đúng chế độ; (4) `GUIDE_PUBLIC=abc` → private kèm log; (5) dừng `api` ở chế độ private → trang báo lỗi, không lộ nội dung; (6) nút chuyển ngôn ngữ giữ nguyên trang | checklist trong README |

## 12. Rủi ro và cách xử lý

- **VitePress 1.x cần vite 5, app dùng vite 8:** gói riêng có `bun.lock` riêng. Dev server và Tailwind của app bỏ qua `frontend/user-guide/` (§9.2).
- **Cache Docker:** stage `build` đang `COPY frontend/ frontend/` trước `bun install`, nên sửa nội dung HDSD làm mất cache layer cài dependency của app. Chấp nhận ở bản này. Nếu build chậm đáng kể thì chuyển `user-guide/` lên top-level (phương án bị loại ở §2).
- **Bản `en` cũ hơn bản `vi`:** test `content-integrity` chặn thiếu trang. Độ cập nhật của nội dung vẫn phải soát lúc review; style guide yêu cầu sửa cả hai ngôn ngữ trong cùng commit.
- **Nhãn UI đổi mà HDSD không đổi:** style guide yêu cầu sửa trong cùng PR; ảnh chụp lại sẽ lộ chỗ lệch.
- **Ảnh cũ so với giao diện:** chạy lại `guide:capture`. Selector chú thích hỏng làm test đỏ.
- **Open redirect qua `next`:** `safeGuideTarget` kèm test; nginx dùng `$request_uri` thô cho `next`.
- **Đường dẫn ảnh tuyệt đối sau rewrite (§4.1):** xác nhận ở giai đoạn 1 trước khi viết nội dung.

## 13. Sau khi duyệt

Spec thêm một endpoint mới và cơ chế `auth_request` (thay đổi tư thế bảo mật), nên cần một entry trong `.claude/decisions.md`. Entry được soạn và trình người dùng xác nhận trước khi ghi.

## 14. Giai đoạn triển khai

Mỗi giai đoạn tự đứng được và có thể merge riêng.

1. **Khung site:** gói VitePress, `product.ts`, sidebar ghép base/app, rewrites, theme, `sync-brand`, template, `new-page`, test `content-integrity`/`brand-tokens`, job CI, lefthook, loại trừ trong vite/Tailwind. Kèm một trang mẫu cho mỗi ngôn ngữ để xác nhận đường dẫn ảnh và nút chuyển ngôn ngữ. Đây là phần fork dùng lại, nên làm trước.
2. **Truy cập và triển khai:** `/api/auth/check`, `safeGuideTarget`, `GuideAuthView`, nút "?", nginx, entrypoint, Dockerfile, `.dockerignore`, compose.
3. **Công cụ chụp ảnh:** config, setup, fixtures, preconditions.
4. **Nội dung:** các trang ở §8, thuật ngữ, FAQ, trang chủ, ảnh.
