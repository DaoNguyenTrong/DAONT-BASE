# Thiết kế: Làm mới khung ứng dụng (layout, header, sidebar) và các kiểu menu

- **Ngày:** 2026-10-17
- **Trạng thái:** Đã duyệt (2026-10-17)
- **Nguồn tham chiếu:** [Apollo – PrimeNG](https://apollo.primeng.dev/) (mục Settings → Menu Type / Menu Theme). Mockup đã duyệt khi brainstorm nằm ở `.superpowers/brainstorm/*/content/{menu-theme,menu-modes,header}.html` (không commit).

Quy ước: **(người dùng chốt)** là điều chủ dự án đã xác nhận khi brainstorm; **(đề xuất)** là quyết định của người viết spec, chủ dự án có thể sửa khi duyệt.

## 1. Mục đích

Chủ dự án thấy sidebar và header của base "cũ". Sidebar nền tím đặc, chỉ có hai kiểu (full / minimal). Header phẳng, các nút điều khiển (sáng/tối, cỡ chữ, ngôn ngữ) nằm rời rạc, còn tài khoản, tổ chức và đăng xuất bị dồn xuống chân sidebar.

Bản thiết kế này làm mới khung ứng dụng theo tinh thần Apollo:

- sidebar chỉ còn lo **điều hướng**, có nhóm mục kèm tiêu đề;
- mọi thứ thuộc về người dùng chuyển lên **header** (menu avatar);
- **người dùng cuối tự chọn** kiểu menu và màu menu qua panel "Cài đặt giao diện" (người dùng chốt);
- thêm ba kiểu menu mới: **Slim+**, **Drawer**, **Horizontal** (người dùng chốt).

Base là starter kit được fork cho nhiều dự án, nên khung mới phải mở rộng được: bản fork chỉ cần khai báo route là có mục menu, nhóm menu và mục trong ô "Đi tới…", không phải sửa component layout.

### Tiêu chí thành công

1. Ở màn ≥ `lg` (1024px), người dùng chọn được 5 kiểu menu × 2 màu menu × sáng/tối, và cả 20 tổ hợp đều hiển thị đúng như mục 3–4.
2. Lựa chọn được giữ lại sau khi tải lại trang. Người dùng đang dùng `minimal` không bị đẩy về mặc định sau khi nâng cấp.
3. Ở màn < `lg`, mọi kiểu menu đều quay về menu trượt từ trái như hiện nay.
4. Trên header làm được: đổi tổ chức, mở hồ sơ, đổi ngôn ngữ, đăng xuất, mở cài đặt giao diện và mở ô "Đi tới…" (Ctrl/⌘ K).
5. `bun run --cwd frontend test:run`, `type-check` và job `test / guide` đều xanh. E2E (`test:e2e`) chạy lại được với helper đã cập nhật.

## 2. Phạm vi

### Mục tiêu

- 5 kiểu menu: Static, Slim, Slim+, Drawer, Horizontal (người dùng chốt).
- 2 màu menu: **Thương hiệu** và **Trong suốt** (người dùng chốt).
- Header mới: ☰ · tiêu đề trang + breadcrumb · ô "Đi tới…" · trợ giúp · chuông · ⚙ · avatar (người dùng chốt).
- Menu avatar: thông tin người dùng, đổi tổ chức, hồ sơ, ngôn ngữ, đăng xuất (người dùng chốt).
- Panel ⚙ Cài đặt giao diện: chế độ màu (Sáng / Tối / Theo hệ thống), kiểu menu, màu menu, cỡ chữ (người dùng chốt qua mockup panel).
- Nhóm menu có tiêu đề, khai báo qua route meta.
- Cập nhật test, helper E2E và HDSD theo vị trí mới.

### Không làm

- Không có các kiểu Overlay và Reveal của Apollo (người dùng chỉ yêu cầu Slim+, Drawer, Horizontal).
- Không có bảng chọn màu chủ đạo, preset (Aura/Lara/Nora) hay màu surface như Apollo. Màu thương hiệu giữ nguyên `#5e4c9e`.
- Không lưu tùy chọn giao diện lên server (đề xuất). Các tùy chọn lưu theo trình duyệt (`localStorage`), giống `theme-preference`, `font-size-preference` và `sidebar-mode` hiện nay. Đồng bộ theo tài khoản cần API mới.
- Ô "Đi tới…" không tìm dữ liệu nghiệp vụ (bản ghi, người dùng…), chỉ tìm trang và hành động.
- Không đổi giao diện các trang ngoài khung: đăng nhập, đăng ký, 404, lỗi máy chủ, cổng HDSD.

### Không được để xảy ra

- Bản fork đang khai báo `meta.sidebar` kiểu cũ bị hỏng menu. Trường mới phải là tùy chọn.
- Mất khả năng dùng bàn phím: mọi mục menu, menu avatar, panel và ô "Đi tới…" phải dùng được bằng Tab/Enter/Esc/phím mũi tên.
- Vùng chạm trên màn cảm ứng nhỏ hơn hiện nay (44px trên mobile, `pointer-coarse:size-10`).
- Chữ trên sidebar Trong suốt hoặc lỗi đăng xuất đỏ "chìm" do còn mang theo override vốn viết cho nền tím (xem 4.2).

## 3. Kiểu menu

### 3.1 Năm kiểu (màn ≥ `lg`)

| Kiểu         | Hiện tại  | Hành vi                                                                                                                                                                                                                                                                 |
| ------------ | --------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `static`     | `full`    | Sidebar đầy đủ (16rem), đẩy nội dung sang phải. Có tiêu đề nhóm; mục có con mở dạng accordion ngay trong sidebar. Nút ☰ ẩn/hiện hẳn sidebar (đề xuất; hiện tại ☰ chuyển full ↔ minimal).                                                                              |
| `slim`       | `minimal` | Rail chỉ có icon (4rem), mỗi mục cấp 1 (nhóm hoặc mục độc lập, xem 3.5) là một icon. Mục lá: rê chuột hiện tooltip, bấm thì điều hướng. Nhóm hoặc mục có con: menu bật ra bên phải.                                                                                     |
| `slim-plus`  | _(mới)_   | Rail rộng khoảng 6rem, mỗi mục cấp 1 là icon kèm nhãn chữ nhỏ bên dưới (tối đa 2 dòng, cắt "…"). Menu bật ra bên phải như `slim`.                                                                                                                                       |
| `drawer`     | _(mới)_   | Bình thường là rail icon như `slim`. Rê chuột vào rail thì sidebar bung ra đầy đủ, **đè lên** nội dung (không đẩy trang), có bóng đổ; rời chuột thì thu lại. Nút 📌 trên đầu sidebar đã bung dùng để **ghim**: khi ghim, sidebar đứng yên và đẩy nội dung như `static`. |
| `horizontal` | _(mới)_   | Không có sidebar. Menu thành thanh ngang trên cùng; mỗi mục cấp 1 (nhóm hoặc mục độc lập) là một mục trên thanh, nhóm và mục có con mở dropdown xuống dưới. Nội dung chiếm trọn chiều rộng.                                                                             |

Trên desktop, nút ☰ chỉ hiện ở `static` (đề xuất). Ở `slim`, `slim-plus`, `drawer` và `horizontal`, menu luôn trong tầm với nên ☰ không có việc gì để làm.

### 3.2 Drawer trên màn cảm ứng

Máy tính bảng nằm ngang có thể rộng ≥ 1024px nhưng không có hover (đề xuất):

- Trên thiết bị `(hover: none)`, chạm vào vùng trống của rail hoặc vào logo thì sidebar bung ra; chạm ra ngoài hoặc chọn một mục thì thu lại.
- Chạm vào icon của một mục lá thì điều hướng thẳng, không bung.
- Trạng thái ghim **được lưu** (`localStorage`). Ghim xong thì tải lại trang vẫn ghim.
- Esc thu sidebar đã bung (khi chưa ghim).

### 3.3 Horizontal: bố cục và mục tràn

Bố cục 2 hàng (đề xuất, giống mockup đã duyệt):

- **Hàng 1** (thanh menu, mang màu menu): logo · các mục menu cấp 1 · khoảng trống · cụm hành động của header (ô "Đi tới…", trợ giúp, chuông, ⚙, avatar).
- **Hàng 2** (nằm trong vùng nội dung, cuộn theo trang): tiêu đề trang + breadcrumb.

Khi các mục cấp 1 (nhóm hoặc mục độc lập) không đủ chỗ, những mục cuối dồn vào một mục **"Thêm ▾"** ở cuối thanh (đề xuất). Phải đo theo chiều rộng thật vì bản fork sẽ thêm mục. Một nhóm bị dồn vào "Thêm" trở thành một menu con bên trong dropdown "Thêm".

### 3.4 Màn < `lg`

Mọi kiểu đều quay về menu trượt từ trái (`n-drawer`, 280px) như hiện nay. ☰ luôn hiện và mở menu này. Trong menu trượt: logo, các nhóm có tiêu đề và mục accordion. **Không còn chân menu** (đề xuất): tổ chức, hồ sơ và đăng xuất đã chuyển lên avatar, và avatar vẫn hiện trên header ở mọi chiều rộng.

### 3.5 Nhóm menu

Nhóm cần cả nhãn lẫn icon, vì ở các kiểu rail và horizontal mỗi nhóm hiển thị như một mục cấp 1 (xem bảng dưới). Vì vậy (đề xuất):

- **Khai báo nhóm** một lần trong một danh sách nhóm (ví dụ `layouts/menu-groups.ts`). Mỗi nhóm có `id`, `labelKey` (i18n), `icon` và `order`. Bản fork thêm nhóm vào danh sách này.
- **Gán route vào nhóm** bằng trường tùy chọn `group` (id nhóm) trong `meta.sidebar`. Id không có trong danh sách thì báo lỗi lúc dev (`console.warn`) và coi như không có nhóm.
- **Mục không khai báo `group`** là mục cấp 1 độc lập, hiển thị y như trước. Nhờ vậy bản fork đang dùng meta cũ vẫn chạy y như cũ.
- **Thứ tự cấp 1**: mục độc lập và nhóm xếp chung theo `order` (`order` của nhóm lấy từ danh sách nhóm). Trong nhóm, các mục xếp theo `order` của mục.
- **Lọc theo quyền** giữ nguyên như `buildSidebarMenu` hiện nay. Nhóm nào rỗng sau khi lọc thì ẩn hẳn.
- **Base** hiện có 2 mục là Trang chủ và Tổ chức. Có gán nhóm cho chúng không là việc của người xây dựng; mục đích chính là để bản fork dùng.

Cách nhóm hiển thị theo từng kiểu (khớp với mockup đã duyệt và với Apollo, nơi "Dashboards", "Apps"… là nhóm):

| Kiểu                                                | Nhóm hiển thị thế nào                                                                                                                                                       |
| --------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `static`, `drawer` khi bung/ghim, menu trượt mobile | Tiêu đề nhóm (chữ in hoa nhỏ), các mục của nhóm nằm bên dưới. Mục có con vẫn là accordion.                                                                                  |
| `slim`, `slim-plus`, `drawer` khi là rail           | Mỗi nhóm là **một** icon trên rail (`slim-plus` có thêm nhãn nhóm). Rê chuột hoặc bấm thì một menu bật ra bên phải, gồm tiêu đề nhóm và các mục (mục có con lồng thêm cấp). |
| `horizontal`                                        | Mỗi nhóm là **một** mục trên thanh ngang (icon + nhãn + ▾), mở dropdown chứa các mục của nhóm.                                                                              |

Mục độc lập có con (khai báo bằng route con như hiện nay) hiển thị giống một nhóm ở các kiểu rail và horizontal. Mục độc lập không có con là một link thẳng.

### 3.6 Cải thiện đi kèm cho mục menu

- Nhóm cha có mục con đang được chọn: tự mở accordion khi vào trang, và tô trạng thái "đang chứa trang hiện tại" cho cha. Ở rail và horizontal, icon/mục cấp 1 của cha cũng được tô. Hiện tại accordion luôn đóng và cha không có trạng thái này.
- Dropdown menu con ở `slim`, `slim-plus` và `horizontal` tô mục đang chọn.

## 4. Màu menu

### 4.1 Hai kiểu (người dùng chốt) × sáng/tối

| Màu menu                   | Chế độ sáng                                                                                                                                                        | Chế độ tối                                                                           |
| -------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------ | ------------------------------------------------------------------------------------ |
| `brand` (Thương hiệu)      | Nền `primary-600` (`#5e4c9e`), chữ trắng/trắng mờ, mục đang chọn `bg-white/15`. Giống hiện tại nhưng có nhóm, bo góc, khoảng thở.                                  | Nền `surface-900`, như hiện tại (đề xuất: giữ nguyên quy ước dark mode đang có).     |
| `transparent` (Trong suốt) | Nền trùng nền trang (`surface-100`), không viền phải. Chữ `surface-600`, tiêu đề nhóm `surface-400`; mục đang chọn là "viên" trắng có bóng nhẹ, chữ `primary-600`. | Nền trùng nền trang (`surface-950`), mục đang chọn `surface-800`, chữ `primary-200`. |

Ở `horizontal`, thanh menu ngang dùng đúng màu menu đang chọn: `brand` là thanh tím, `transparent` là thanh hòa vào nền và có viền dưới mảnh.

**Mặc định** khi chưa có lựa chọn: `static` + `brand` (đề xuất; gần giao diện hiện tại nhất).

### 4.2 Gỡ những chỗ đang ngầm coi sidebar luôn nền tối

`AppSidebar.vue` hiện:

- dùng cứng wordmark chữ trắng (`/weatherplus-wordmark-dark.webp`);
- áp `sidebarThemeOverrides` (`errorColor` hồng nhạt, màu `Drawer` tím) ở chế độ sáng.

Cả hai chỉ đúng khi nền tím. Thiết kế mới:

- **Wordmark**: `brand`, hoặc bất kỳ màu menu nào ở chế độ tối, dùng wordmark chữ trắng. `transparent` ở chế độ sáng dùng `useBrandWordmark()`.
- **`errorColor` hồng nhạt**: chỉ tồn tại để nút đăng xuất đọc được trên nền tím. Đăng xuất đã chuyển lên menu avatar (nền trắng/surface), nên bỏ override này.
- **Màu `Drawer` tím** cho menu trượt mobile: chỉ áp khi `brand` + sáng. Với `transparent`, menu trượt dùng màu drawer mặc định của theme.

## 5. Header

### 5.1 Bố cục

Từ trái sang phải (người dùng chốt):

1. **☰**: theo 3.1 (desktop) và 3.4 (mobile).
2. **Tiêu đề trang + breadcrumb**: tiêu đề là nhãn breadcrumb cuối cùng (`meta.breadcrumbKey` của route khớp sâu nhất), chữ đậm. Phía trên là breadcrumb nhỏ, có link, gồm các cấp trước đó. Ở Trang chủ chỉ còn tiêu đề. Nhóm menu không phải route nên **không** xuất hiện trong breadcrumb (đề xuất; mockup có vẽ "Trang chủ / Quản trị" chỉ để minh họa). Giữ quy tắc cắt "…" cho nhãn dài như hiện nay. Màn < `lg`: chỉ tiêu đề, không breadcrumb.
3. **Ô "Đi tới…"**: ô bo tròn có gợi ý `Ctrl K` (macOS: `⌘ K`). Màn < `md`: thu thành nút icon 🔍.
4. **Trợ giúp (?)**: giữ nguyên link HDSD.
5. **Chuông thông báo**: giữ nguyên `NotificationBell`.
6. **⚙**: mở panel Cài đặt giao diện (mục 7).
7. **Avatar**: vòng tròn chữ viết tắt tên (API không có ảnh đại diện), kèm tên người dùng và tổ chức đang chọn. Màn < `xl`: chỉ còn vòng tròn. Bấm vào thì mở menu avatar (mục 6).

Header giữ hiệu ứng sticky + `backdrop-blur`, nền trong suốt hòa với nền trang (như Apollo), cao 4rem.

### 5.2 Ô "Đi tới…" (command palette)

- Mở bằng Ctrl/⌘ K ở bất kỳ đâu trong khung ứng dụng, hoặc bấm vào ô. Hộp thoại giữa màn hình: ô nhập, danh sách kết quả, ↑/↓ để chọn, Enter để thực hiện, Esc để đóng.
- **Nguồn "Trang"**: `useSidebarMenu()` làm phẳng, tức chỉ gồm mục lá có `routeName`. Nguồn này đã lọc theo quyền nên không bao giờ gợi ý trang người dùng không vào được. Mỗi kết quả hiện đường dẫn cha (nhóm / mục cha) ở bên phải.
- **Nguồn "Hành động"** của base: Mở cài đặt giao diện, Hồ sơ cá nhân, Đổi ngôn ngữ, Đăng xuất (qua hộp xác nhận hiện có).
- **Tìm kiếm**: không phân biệt hoa thường và **không dấu**. Gõ "to chuc" vẫn ra "Tổ chức". Dùng lại `lib/search-text.ts` nếu hàm trong đó phù hợp.
- **Mở rộng cho bản fork**: có một điểm đăng ký hành động (ví dụ một composable/registry trong `lib/`) để bản fork thêm hành động mà không sửa component palette. Mục kiểu "Trang" thì tự có khi khai báo route với `meta.sidebar`. Tên và hình dạng API là việc của người xây dựng.
- Khi palette đang mở thì không bắt Ctrl K lần nữa. Ctrl K không được chặn phím tắt của trình duyệt khi focus đang nằm trong ô soạn thảo `MarkdownEditor`. Người xây dựng kiểm tra xung đột phím đó.

### 5.3 Header ở chế độ `horizontal`

Cụm 3–7 chuyển lên thanh menu ngang (3.3). Tiêu đề trang + breadcrumb thành hàng 2 trong vùng nội dung. Không còn ☰.

## 6. Menu avatar

Dropdown neo vào avatar, căn phải (người dùng chốt):

1. **Đầu menu**: vòng tròn viết tắt · tên (`account.name || account.username`) · email.
2. **Tổ chức**: danh sách "Không gian cá nhân" + các tổ chức của tôi, có dấu ✓ ở tổ chức hiện tại và spinner khi đang chuyển, cùng dòng "Quản lý tổ chức". Hành vi giữ nguyên `OrganizationSwitcher`: tải danh sách khi mở lần đầu và **tải lại toàn trang** sau khi đổi (`useOrganizationSwitch`). Danh sách dài thì cuộn trong menu (`max-h`).
3. **Hồ sơ cá nhân**: mở `ProfileDialog` như hiện tại.
4. **Ngôn ngữ**: hai lựa chọn Tiếng Việt / English, có ✓ ở ngôn ngữ đang dùng, đổi qua `useLocaleStore`.
5. **Đăng xuất**: màu đỏ, qua hộp xác nhận hiện có (`requestConfirmationNaive` → `auth.logout()`).

Ghi chú thực hiện:

- Tách phần danh sách và logic chuyển tổ chức của `OrganizationSwitcher` thành một phần dùng lại được, đặt inline trong menu avatar. **Không** lồng popover trong popover.
- Prop `minimal`, logic `menuPlacement` theo `BELOW_LG_QUERY` và các lời gọi `sidebar.closeMobile()` trong switcher không còn cần thiết. `OrganizationSwitcher` không còn chỗ nào dùng thì xóa.
- Ở chế độ `transparent` hay `brand` thì menu avatar đều nền trắng/surface, dùng `errorColor` mặc định.

## 7. Panel ⚙ Cài đặt giao diện

`n-drawer` trượt từ phải, rộng khoảng 20rem (người dùng chốt):

| Mục        | Điều khiển                                | Lưu ở                                                                  |
| ---------- | ----------------------------------------- | ---------------------------------------------------------------------- |
| Chế độ màu | Nút phân đoạn: Sáng · Tối · Theo hệ thống | `theme-preference`, thêm giá trị `system` (xem dưới bảng)              |
| Kiểu menu  | Lưới 5 thẻ có hình minh họa nhỏ + tên     | khóa mới, xem 8.2                                                      |
| Màu menu   | Nút phân đoạn: Thương hiệu · Trong suốt   | khóa mới, xem 8.2                                                      |
| Cỡ chữ     | A− · giá trị px · A+ · Đặt lại            | `font-size-preference` (giữ nguyên `lib/font-size.ts`, giới hạn 14–20) |

- **Theo hệ thống**: `ThemePreference` mở rộng thành `'light' | 'dark' | 'system'`. Với `system`, giao diện theo `prefers-color-scheme` và đổi ngay khi hệ điều hành đổi (listener `matchMedia`). Mặc định khi chưa lưu gì vẫn là `light` như hiện nay (đề xuất). Nút sáng/tối của `AppControls` trên các trang đăng nhập vẫn chỉ bật/tắt hai trạng thái; bấm vào khi đang `system` thì chuyển sang trạng thái ngược với màu đang hiển thị. Hàm `initializeThemePreference` chạy trước khi app mount cũng phải hiểu `system`, để trang không nháy sai màu.
- Mọi thay đổi áp dụng ngay, không có nút Lưu.
- Màn < `lg`: mục "Kiểu menu" hiện mờ, kèm dòng ghi chú "Áp dụng trên màn hình rộng". Lựa chọn vẫn lưu được (đề xuất).
- Ngôn ngữ **không** nằm trong panel mà ở menu avatar.

`AppControls` **giữ nguyên** vì `LoginView`, `RegisterView`, `NotFoundView`, `GuideAuthView` và `ServerErrorScreen` vẫn dùng nó. Chỉ `AppHeader` thôi dùng `AppControls`.

## 8. Trạng thái và lưu trữ

### 8.1 Store

Mở rộng `useSidebarStore` hoặc thay bằng một store layout (đặt tên là việc của người xây dựng), giữ:

- `menuMode`: `'static' | 'slim' | 'slim-plus' | 'drawer' | 'horizontal'`
- `menuTheme`: `'brand' | 'transparent'`
- `staticInactive`: sidebar Static đang bị ☰ ẩn
- `drawerPinned`
- `drawerHovered` (chỉ trong phiên)
- `mobileOpen`
- `settingsOpen`, `paletteOpen` (chỉ trong phiên)

Store theo kiểu setup store (`frontend-conventions.md`).

### 8.2 `localStorage` và chuyển đổi dữ liệu cũ

- Khóa mới: `layout-menu-mode`, `layout-menu-theme`, `layout-drawer-pinned`, `layout-static-inactive` (đề xuất tên).
- **Chuyển đổi một lần**: nếu chưa có `layout-menu-mode` mà có `sidebar-mode`, thì `full` → `static` và `minimal` → `slim`, ghi khóa mới rồi xóa `sidebar-mode`.
- Giá trị lạ hoặc hỏng thì về mặc định (`static` / `brand` / `false`).
- Bọc mọi truy cập `localStorage` trong try/catch: trình duyệt chặn storage vẫn phải chạy được với mặc định.

## 9. Ảnh hưởng tới code hiện có

| Khu vực                                                                   | Thay đổi                                                                                                                                                                            |
| ------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `layouts/AppLayout.vue`                                                   | Bố cục theo `menuMode`: sidebar trái hoặc thanh ngang trên; với `drawer`, nội dung không bị đẩy khi sidebar bung.                                                                   |
| `layouts/AppSidebar.vue`, `AppSidebarItem.vue`                            | Viết lại theo 5 kiểu × 2 màu; bỏ khu tài khoản/tổ chức/đăng xuất; gỡ override nền tím (4.2). Có thể tách thành nhiều component (rail, panel, thanh ngang): việc của người xây dựng. |
| `layouts/AppHeader.vue`                                                   | Bố cục mới (mục 5), thay `AppControls` bằng ⚙ + avatar + ô "Đi tới…".                                                                                                               |
| `layouts/sidebar-menu.ts`, `composables/use-sidebar-menu.ts`, `RouteMeta` | Thêm danh sách nhóm và trường `group`; hàm dựng menu trả về danh sách mục cấp 1 gồm nhóm và mục độc lập (3.5); giữ tương thích meta cũ.                                             |
| `stores/sidebar-store.ts`                                                 | Theo mục 8.                                                                                                                                                                         |
| `lib/theme.ts`, `composables/use-theme-preference.ts`                     | Thêm `system` (mục 7). `isDark` vẫn là giá trị hiệu lực (đang hiển thị tối hay không), nên các chỗ đang đọc `isDark` không phải đổi.                                                |
| `components/OrganizationSwitcher.vue`                                     | Tách logic/danh sách cho menu avatar (mục 6).                                                                                                                                       |
| Component mới                                                             | Menu avatar, panel cài đặt, command palette (+ registry hành động).                                                                                                                 |
| `locales/{vi,en}.ts`                                                      | Khóa mới: tên kiểu menu, màu menu, tiêu đề panel, nhóm menu, palette, menu avatar. Cả hai ngôn ngữ.                                                                                 |

### 9.1 Test (`testing.md`)

- **Cập nhật**: `tests/layouts/{AppHeader,AppSidebar,AppSidebarItem,AppLayout}.test.ts`, `tests/unit/stores/sidebar-store.test.ts`, `tests/unit/composables/use-sidebar-menu.test.ts`, `tests/components/OrganizationSwitcher.test.ts`. Các testid `app-sidebar-account`, `app-sidebar-logout`, `organization-switcher` đổi chỗ.
- **Thêm mới**:
  - store: chuyển đổi `sidebar-mode`, giá trị hỏng, storage bị chặn;
  - dựng menu có nhóm: không nhóm, nhóm rỗng sau lọc quyền, thứ tự;
  - mỗi kiểu menu hiển thị đúng cấu trúc;
  - drawer bung/thu/ghim;
  - horizontal dồn mục vào "Thêm";
  - menu avatar: đổi tổ chức (gọi API qua MSW), đăng xuất qua xác nhận, đổi ngôn ngữ;
  - panel cài đặt: đổi từng mục và giá trị được lưu;
  - palette: Ctrl K mở, tìm không dấu, không gợi ý trang thiếu quyền, Enter điều hướng, hành động fork đăng ký được.
- **E2E**: `e2e/fixtures.ts` → `logout()` đang bấm `page.locator('aside').getByRole('button', { name: 'Logout' })`. Phải đổi sang mở menu avatar rồi bấm Đăng xuất. E2E không chạy trong CI nên chỗ này sẽ hỏng âm thầm; chạy lại `bun run --cwd frontend test:e2e` cục bộ trước khi review (`testing.md` § E2E).

### 9.2 HDSD (`frontend/user-guide/`)

Các trang mô tả vị trí cũ phải viết lại bằng cả tiếng Việt và tiếng Anh, chụp lại ảnh bằng `bun run --cwd frontend guide:capture`:

- `base/account/profile.md`
- `base/organizations/switch.md`
- và các trang khác có nhắc đường đi qua sidebar (người xây dựng rà bằng tìm kiếm "thanh bên", "góc dưới", "đăng xuất", "sidebar").

Công cụ chụp ảnh cũng phụ thuộc vào layout cũ, phải sửa trước khi chụp lại:

- `frontend/user-guide/tools/fixtures.ts` chờ `APP_SHELL = 'aside'`. Với `horizontal` thì không còn `aside`; nên chờ một selector ổn định của khung, ví dụ một `data-testid` trên `AppLayout`.
- Các script chụp đang bấm `app-sidebar-account`, `app-sidebar-logout`, `organization-switcher`; đổi sang luồng qua menu avatar.

Cân nhắc thêm một trang ngắn "Tùy chỉnh giao diện" cho panel ⚙. Job `test / guide` phải xanh.

## 10. Để lại cho người xây dựng

- Chia component, đặt tên store, khóa và component.
- Hiệu ứng chuyển động: độ dài, easing. Có tôn trọng `prefers-reduced-motion` không.
- Kích thước chính xác của rail (4rem / ~6rem), độ rộng sidebar Static (16rem), độ rộng panel.
- Hình minh họa nhỏ cho 5 thẻ "Kiểu menu" trong panel: SVG inline hay CSS.
- Cách đo tràn cho horizontal (`ResizeObserver`…).
- Chia giai đoạn triển khai, ví dụ (1) store + nhóm menu + header/avatar/panel, (2) các kiểu menu, (3) palette, (4) HDSD.

## 11. Quyết định do người viết spec đưa ra (cần chủ dự án soát)

1. Mặc định `static` + `brand`.
2. Chỉ lưu `localStorage`, không đồng bộ theo tài khoản.
3. Nhóm menu khai báo trong một danh sách riêng (có icon); route trỏ tới nhóm bằng `meta.sidebar.group`; nhóm không hiện trong breadcrumb.
4. ☰ ở Static ẩn/hiện hẳn sidebar, và chỉ hiện ở Static trên desktop.
5. Drawer trên màn cảm ứng: chạm để bung; trạng thái ghim được lưu.
6. Horizontal: 2 hàng; mục tràn dồn vào "Thêm ▾".
7. Menu trượt mobile không còn chân menu.
8. Dark mode của `brand` giữ nền `surface-900` như hiện tại.
9. Ngôn ngữ nằm ở menu avatar, không ở panel.
