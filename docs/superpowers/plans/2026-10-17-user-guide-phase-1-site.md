# HDSD — Giai đoạn 1: Khung site — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Dựng gói VitePress `frontend/user-guide/site` có đủ khung tái sử dụng: tách cây `base/`/`app/`, mục lục khai một lần cho hai ngôn ngữ, màu và logo lấy từ app, template, script tạo trang, test toàn vẹn nội dung, CI. Kèm hai trang mẫu cho mỗi ngôn ngữ.

**Architecture:** Gói VitePress độc lập (vitepress 1.6.4, `bun.lock` riêng) nằm trong `frontend/`. Logic thuần (đường dẫn, sidebar, sinh token, render markdown, tạo trang) nằm trong module TypeScript nhỏ và có test `bun test`. `config.mts` và theme chỉ ghép các module đó lại. Lớp đăng nhập (`auth-guard.ts`), công cụ chụp ảnh và nội dung thật thuộc giai đoạn 2–4, không nằm trong plan này.

**Tech Stack:** VitePress 1.6.4, Vue 3, Bun 1.3.14 (`bun test`), TypeScript, `@fontsource/roboto` 5.3.0, prettier 3.8.4.

**Spec:** `docs/superpowers/specs/2026-10-15-user-guide-design.md` (§3, §4, §5, §6, §7, §9.2 phần vite/Tailwind, §9.5, §9.6, §14 giai đoạn 1).

## Global Constraints

- Làm trên nhánh `feature/user-guide-site` cắt từ `dev`. Không commit `docs/superpowers/**` và `.claude/decisions.md` (đang có thay đổi từ trước, để nguyên không stage).
- Phiên bản ghim: `vitepress` `1.6.4`, `@fontsource/roboto` `5.3.0`, `prettier` `3.8.4`, `@types/bun` `1.3.14` (khớp Bun của CI), Bun `1.3.14` trong CI, Node `22` trong CI. Tailwind của app là `^4.3.1`, đủ mới cho `@source not` (cần ≥ 4.1).
- `base: '/guide/'`, `srcDir: 'src'`, `outDir: 'dist'`, `cleanUrls: true`, dev/preview port `5174` (`--strictPort`).
- Locale: `root` = `vi` (`lang: 'vi-VN'`, `label: 'Tiếng Việt'`), `en` (`lang: 'en-US'`, `label: 'English'`, `link: '/en/'`). Slug tiếng Anh, giống nhau ở hai locale.
- Cây nguồn: `src/base/…` và `src/app/…`; `src/{base,app}/en/…` cho tiếng Anh. `rewrites` bỏ tiền tố `base/` và `app/`.
- Ảnh: `src/{base,app}/images/<vi|en>/<chương>/<tên>.<ext>`, tham chiếu bằng đường dẫn tuyệt đối từ `srcDir` (`/base/images/vi/getting-started/overview.webp`).
- Token CSS sinh ra dùng prefix `--brand-`; class CSS dùng prefix `guide-`.
- Nhãn callout: `vi` = Lưu ý / Mẹo / Quan trọng (info / tip / warning), `en` = Note / Tip / Important.
- Comment trong code viết tiếng Anh (theo quy ước của base). Chuỗi người dùng nhìn thấy có đủ `vi` và `en`.
- Commit theo Conventional Commits, scope `guide`. Stage từng file theo đường dẫn, không `git add -A`. Mỗi message kết thúc bằng:
  ```
  Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
  Claude-Session: https://claude.ai/code/session_01NYrpuCpEweNouDcJhmpiAz
  ```
- Mã nguồn tham chiếu để chuyển sang: `/home/daont/workspaces/projects/sources/CIMP-VBI/frontend/user-guide/site/.vitepress/` (`config.mts`, `theme/index.ts`, `theme/custom.css`, `theme/sidebar-accordion.ts`).

**Ngoài phạm vi plan này** (giai đoạn sau): `auth-guard.ts`, `/api/auth/check`, nginx, Docker, `.dockerignore` (giai đoạn 2); công cụ chụp ảnh, stub section của `new-page`, glob lefthook cho `tools/` (giai đoạn 3); lưới `features`, `HomeQuickStart.vue`, các chương nội dung, Thuật ngữ, ảnh thật (giai đoạn 4).

**Quyết định kỹ thuật của plan** (spec không chốt, hoặc chốt theo cách VitePress không hỗ trợ):

1. VitePress không có nhãn callout theo locale (`markdown.container.*Label` là cấu hình chung). Nhãn `vi` đặt ở cấu hình chung; một core rule của markdown-it gắn tiêu đề `en` cho callout không có tiêu đề trong trang tiếng Anh (Task 4).
2. Link "Mở ứng dụng" của spec (`link: '../'`) sai ở trang sâu hơn một cấp và dưới `/en/`. VitePress cũng luôn gắn base cho link tuyệt đối. Vì vậy link này do component `AppLink.vue` tự render với `href="/"` (Task 6).
3. `product.ts` chỉ chứa dữ liệu. Version tính ở `version.ts` (chỉ chạy trên Node) và đưa vào client qua `vite.define` (`__GUIDE__`), để `execSync` không lọt vào bundle client.
4. Stub section chụp ảnh của `new-page` (spec §4.5) để sang giai đoạn 3, khi fixture chụp ảnh đã có. Tạo stub import một file chưa tồn tại sẽ làm type-check đỏ.

## Review Focus

1. **Nút chuyển ngôn ngữ ở trang lồng nhau** phải dẫn tới đúng trang tương ứng, không 404. Đã thêm bước kiểm HTML sau build vào Task 6.
2. **Fork ẩn một chương `hideable`** thì site vẫn build được, và trang của chương đó không có trong `dist/`. Đã thêm test glob vào Task 3, và `content-integrity` bỏ qua trang bị loại (Task 5).
3. **Token trong `tailwind.css` là chuỗi `var(...)` lồng nhau, hoặc bị đổi tên:** chuỗi phải được phân giải ra hex; token thiếu thì báo lỗi nêu tên token. Đã thêm test vào Task 2.
4. **Chạy lại `new-page` cho trang đã có**, kể cả khi chỉ có một ngôn ngữ, không được ghi đè hay ghi một nửa. Đã thêm test vào Task 7.
5. **Callout trong trang `en` có tiêu đề tự đặt** (`::: warning Heads up`) phải giữ nguyên tiêu đề đó. Đã thêm test vào Task 4.

---

### Task 1: Dựng gói VitePress và loại trừ khỏi app

**Files:**
- Create: `frontend/user-guide/site/package.json`, `frontend/user-guide/site/tsconfig.json`, `frontend/user-guide/site/.prettierignore`
- Create: `frontend/user-guide/site/.vitepress/config.mts`, `frontend/user-guide/site/.vitepress/product.ts`, `frontend/user-guide/site/.vitepress/version.ts`
- Create: `frontend/user-guide/site/src/index.md`, `frontend/user-guide/site/src/en/index.md`
- Modify: `.gitignore` (khối `# ---- frontend/` quanh dòng 49)
- Modify: `frontend/vite.config.ts:68-79` (`server.watch.ignored`)
- Modify: `frontend/src/assets/styles/tailwind.css:1`

**Interfaces:**
- Produces: `product` (`.vitepress/product.ts`):
  ```ts
  export type ProductBrand = { wordmarkLight: string; wordmarkDark: string; mark: string }
  export const product: {
    name: string
    publisher: string
    brand: ProductBrand
    hiddenBaseChapters: string[]
  }
  ```
  Giá trị trong base: `name: 'StarterKit'`, `publisher: 'WeatherPlus'`, `brand: { wordmarkLight: 'weatherplus-wordmark-light.webp', wordmarkDark: 'weatherplus-wordmark-dark.webp', mark: 'weatherplus-mark.webp' }`, `hiddenBaseChapters: []`.
- Produces: `resolveVersion(): string` (`.vitepress/version.ts`): `git describe --tags --always`, lỗi thì `'unknown'`, giống `resolveAppVersion()` trong `frontend/vite.config.ts:13-22`.
- Produces: các script của `package.json`: `dev`, `build`, `preview`, `test`, `new-page`, `sync-brand`, `format`, `format:check`. `dev`/`build` trong task này chạy thẳng `vitepress`; Task 2 chèn `sync-brand` vào trước.

- [ ] **Step 1: Cắt nhánh**

```bash
git switch dev && git switch -c feature/user-guide-site
```

Expected: `git branch --show-current` in ra `feature/user-guide-site`. `docs/` và `.claude/decisions.md` vẫn ở trạng thái chưa commit.

- [ ] **Step 2: Tạo `package.json`, `tsconfig.json`, `.prettierignore`**

- `package.json`: `"name": "user-guide-site"`, `"private": true`, `"type": "module"`. Scripts:
  - `dev`: `vitepress dev . --port 5174 --strictPort`
  - `build`: `vitepress build .`
  - `preview`: `vitepress preview . --port 5174`
  - `test`: `bun test`
  - `new-page`: `bun scripts/new-page.ts`
  - `sync-brand`: `bun scripts/sync-brand.ts`
  - `format`: `prettier --write .`
  - `format:check`: `prettier --check .`

  `devDependencies`: `vitepress`, `@fontsource/roboto`, `prettier`, `@types/bun`, ghim đúng phiên bản ở Global Constraints.
- `tsconfig.json`: `strict`, `module: ESNext`, `moduleResolution: bundler`, `types: ["bun"]`, include `.vitepress/**/*`, `scripts/**/*`, `tests/**/*`. Chỉ phục vụ editor; không có bước type-check.
- `.prettierignore`: `node_modules/`, `dist/`, `.vitepress/cache/`, `src/public/brand/`, `*.generated.css`, `bun.lock`.

Prettier tự tìm `frontend/.prettierrc.json` khi đi ngược cây thư mục, không cần file cấu hình riêng.

- [ ] **Step 3: Tạo `product.ts`, `version.ts`, `config.mts` tối thiểu, hai trang chủ**

- `config.mts`: `base`, `srcDir`, `outDir`, `cleanUrls`, `title: product.name`, `lastUpdated: false`, `locales` hai ngôn ngữ (chỉ `label`/`lang`/`link`). Thêm `vite.define.__GUIDE__ = JSON.stringify({ ...product, version: resolveVersion() })`.
- `src/index.md` và `src/en/index.md`: `layout: home`, `hero.name` = "Hướng dẫn sử dụng" / "User guide", `hero.tagline` một câu. Chưa có `actions`; Task 6 thêm sau.

- [ ] **Step 4: Cài và build thử**

```bash
bun install --cwd frontend/user-guide/site && bun run --cwd frontend/user-guide/site build
```

Expected: build thành công; có `frontend/user-guide/site/dist/index.html` và `dist/en/index.html`; `frontend/user-guide/site/bun.lock` được tạo.

- [ ] **Step 5: Loại trừ site khỏi app và git**

- `.gitignore`: thêm `frontend/user-guide/site/.vitepress/cache/`, `frontend/user-guide/site/src/public/brand/`, `**/brand-tokens.generated.css`. (`dist` đã được luật `dist` ở dòng 49 xử lý.)
- `frontend/vite.config.ts`: thêm `'**/user-guide/**'` vào `server.watch.ignored`.
- `frontend/src/assets/styles/tailwind.css`: thêm `@source not "../../../user-guide";` ngay dưới `@import 'tailwindcss';`.

- [ ] **Step 6: Kiểm app không bị ảnh hưởng**

```bash
git check-ignore -v frontend/user-guide/site/dist frontend/user-guide/site/.vitepress/cache
bun run --cwd frontend build-only && bun run --cwd frontend test:run
```

Expected: hai đường dẫn đều bị ignore; app build xanh; toàn bộ test frontend PASS.

- [ ] **Step 7: Commit**

```bash
git add .gitignore frontend/vite.config.ts frontend/src/assets/styles/tailwind.css \
  frontend/user-guide/site/package.json frontend/user-guide/site/bun.lock \
  frontend/user-guide/site/tsconfig.json frontend/user-guide/site/.prettierignore \
  frontend/user-guide/site/.vitepress/config.mts frontend/user-guide/site/.vitepress/product.ts \
  frontend/user-guide/site/.vitepress/version.ts \
  frontend/user-guide/site/src/index.md frontend/user-guide/site/src/en/index.md
git commit -m "feat(guide): scaffold the VitePress user guide package"
```

---

### Task 2: Sinh token màu và chép logo từ app

**Files:**
- Create: `frontend/user-guide/site/scripts/brand-tokens.ts`
- Create: `frontend/user-guide/site/scripts/sync-brand.ts`
- Create: `frontend/user-guide/site/tests/brand-tokens.test.ts`
- Create: `frontend/user-guide/site/tests/fixtures/tailwind.css`
- Modify: `frontend/user-guide/site/package.json` (`dev`, `build`)

**Interfaces:**
- Consumes: `product.brand` (Task 1).
- Produces (`scripts/brand-tokens.ts`):
  - `parseThemeBlocks(css: string): { theme: Record<string, string>; dark: Record<string, string> }`: các khai báo `--color-*` trong khối `@theme { … }` và `.dark { … }`.
  - `generateBrandTokensCss(css: string): string`: một khối `:root { … }` gồm:
    - `--brand-violet-<n>` từ `--color-accent-<n>`;
    - `--brand-leaf-<n>` từ `--color-leaf-<n>`, với `n` ∈ 50, 100, 200, …, 900, 950;
    - `--brand-primary-200-dark` từ `--color-primary-200` của `.dark`;
    - `--brand-surface-900`, `--brand-surface-950` từ `--color-surface-900/950` của `.dark`.

    Giá trị `var(--x)` được phân giải theo chuỗi (tìm trong cùng khối trước, rồi tới `@theme`) cho tới khi ra hex. Thiếu token thì ném `Error` có message `Missing tokens in tailwind.css: <tên1>, <tên2>`.
- Produces (`scripts/sync-brand.ts`):
  - `copyLogos(brand: ProductBrand, fromDir: string, toDir: string): void`. Thiếu file nguồn thì ném `Error('Missing logo files in <fromDir>: <tên…>')` trước khi chép bất kỳ file nào.
  - Khi chạy trực tiếp (`import.meta.main`): chép logo từ `frontend/public/` vào `src/public/brand/`, rồi ghi `generateBrandTokensCss(<frontend/src/assets/styles/tailwind.css>)` vào `.vitepress/theme/brand-tokens.generated.css`. Đường dẫn tính từ `import.meta.dir`.

- [ ] **Step 1: Viết test (đỏ)**

`tests/fixtures/tailwind.css`: một khối `@theme` có đủ 11 bậc accent và leaf, trong đó `--color-accent-700: var(--color-x)` và `--color-x: var(--color-y)`, `--color-y: #123456`. Thêm một khối `.dark` có `--color-primary-200: #9c80dc`, `--color-surface-900: #1e2235`, `--color-surface-950: #121527`. Khối `@theme` cũng có `--color-surface-900: #2d293d` để kiểm rằng giá trị lấy từ `.dark`.

```ts
// tests/brand-tokens.test.ts
import { describe, expect, test } from 'bun:test'
import { mkdtempSync, writeFileSync, existsSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { generateBrandTokensCss } from '../scripts/brand-tokens'
import { copyLogos } from '../scripts/sync-brand'

const fixture = await Bun.file(new URL('./fixtures/tailwind.css', import.meta.url)).text()
const appCss = await Bun.file(new URL('../../../src/assets/styles/tailwind.css', import.meta.url)).text()

describe('generateBrandTokensCss', () => {
  test('maps every accent and leaf step', () => {
    const css = generateBrandTokensCss(fixture)
    for (const n of [50, 100, 200, 300, 400, 500, 600, 700, 800, 900, 950]) {
      expect(css).toContain(`--brand-violet-${n}:`)
      expect(css).toContain(`--brand-leaf-${n}:`)
    }
  })
  test('resolves chained var() to a hex value', () => {
    expect(generateBrandTokensCss(fixture)).toContain('--brand-violet-700: #123456;')
  })
  test('takes dark-only tokens from the .dark block', () => {
    const css = generateBrandTokensCss(fixture)
    expect(css).toContain('--brand-primary-200-dark: #9c80dc;')
    expect(css).toContain('--brand-surface-900: #1e2235;')
    expect(css).toContain('--brand-surface-950: #121527;')
  })
  test('names every missing token', () => {
    const broken = fixture.replace(/--color-leaf-400:[^;]+;/, '').replace(/--color-accent-950:[^;]+;/, '')
    expect(() => generateBrandTokensCss(broken)).toThrow(/--color-accent-950.*--color-leaf-400|--color-leaf-400.*--color-accent-950/)
  })
  test('the real app stylesheet yields the WeatherPlus values', () => {
    const css = generateBrandTokensCss(appCss)
    expect(css).toContain('--brand-violet-500: #4e3d8f;')
    expect(css).toContain('--brand-violet-600: #5e4c9e;')
    expect(css).toContain('--brand-leaf-400: #91bd44;')
    expect(css).toContain('--brand-surface-950: #121527;')
  })
})

describe('copyLogos', () => {
  test('refuses to copy anything when a source file is missing', () => {
    const from = mkdtempSync(join(tmpdir(), 'logos-'))
    const to = mkdtempSync(join(tmpdir(), 'brand-'))
    writeFileSync(join(from, 'light.webp'), 'x')
    const brand = { wordmarkLight: 'light.webp', wordmarkDark: 'dark.webp', mark: 'mark.webp' }
    expect(() => copyLogos(brand, from, to)).toThrow(/dark\.webp.*mark\.webp/)
    expect(existsSync(join(to, 'light.webp'))).toBe(false)
  })
})
```

- [ ] **Step 2: Chạy test, xác nhận đỏ**

Run: `bun run --cwd frontend/user-guide/site test tests/brand-tokens.test.ts`
Expected: FAIL, không resolve được `../scripts/brand-tokens`.

- [ ] **Step 3: Implement `brand-tokens.ts` và `sync-brand.ts`**

Tách khối bằng cách tìm `@theme {` và `.dark {`, rồi đếm ngoặc tới `}` khớp. Trong khối, khai báo là các dòng `--color-<name>: <value>;`.

- [ ] **Step 4: Chạy test, xác nhận xanh**

Run: `bun run --cwd frontend/user-guide/site test tests/brand-tokens.test.ts`
Expected: PASS 6/6.

- [ ] **Step 5: Nối vào dev/build và chạy thật**

Đổi script: `"dev": "bun scripts/sync-brand.ts && vitepress dev . --port 5174 --strictPort"`, `"build": "bun scripts/sync-brand.ts && vitepress build ."`.

Run: `bun run --cwd frontend/user-guide/site build && ls frontend/user-guide/site/src/public/brand && git status --short frontend/user-guide`
Expected:
- build xanh;
- có 3 file `weatherplus-*.webp`;
- `git status` không liệt kê `src/public/brand/` hay `brand-tokens.generated.css` (đã ignore).

- [ ] **Step 6: Commit**

```bash
git add frontend/user-guide/site/package.json frontend/user-guide/site/scripts/brand-tokens.ts \
  frontend/user-guide/site/scripts/sync-brand.ts frontend/user-guide/site/tests/brand-tokens.test.ts \
  frontend/user-guide/site/tests/fixtures/tailwind.css
git commit -m "feat(guide): generate brand tokens and copy logos from the app"
```

---

### Task 3: Mô hình đường dẫn và mục lục base/app

**Files:**
- Create: `frontend/user-guide/site/.vitepress/paths.ts`
- Create: `frontend/user-guide/site/.vitepress/sidebar-types.ts`
- Create: `frontend/user-guide/site/.vitepress/sidebar.base.ts`, `frontend/user-guide/site/.vitepress/sidebar.app.ts`, `frontend/user-guide/site/.vitepress/sidebar.ts`
- Create: `frontend/user-guide/site/tests/paths.test.ts`, `frontend/user-guide/site/tests/sidebar.test.ts`

**Interfaces:**
- Consumes: `product.hiddenBaseChapters` (Task 1).
- Produces (`sidebar-types.ts`):
  ```ts
  export type Tree = 'base' | 'app'
  export type Locale = 'vi' | 'en'
  export type Localized = { vi: string; en: string }
  export type ChapterItem = { slug: string; text: Localized } // slug 'index' = trang đầu của chương
  export type Chapter = { slug: string; text: Localized; items?: ChapterItem[]; hideable?: boolean }
  export type ResolvedChapter = Chapter & { tree: Tree }
  ```
  Chương có `items` là một thư mục; chương không có `items` là một file `<slug>.md`.
- Produces (`paths.ts`):
  - `pageSourcePath(tree: Tree, locale: Locale, chapter: string, item?: string): string`, ví dụ:
    - `('base','vi','getting-started','index')` → `base/getting-started/index.md`;
    - `('base','en','getting-started','sign-in')` → `base/en/getting-started/sign-in.md`;
    - `('app','vi','faq')` → `app/faq.md`.
  - `pageLink(locale: Locale, chapter: string, item?: string): string`, ví dụ:
    - `('vi','getting-started','index')` → `/getting-started/`;
    - `('en','getting-started','sign-in')` → `/en/getting-started/sign-in`;
    - `('vi','faq')` → `/faq`.
  - `rewritePath(source: string): string`: bỏ `^(base|app)/`. Ví dụ `base/en/faq.md` → `en/faq.md`.
  - `localeOf(path: string | undefined): Locale`: bỏ tiền tố cây, nếu bắt đầu bằng `en/` thì `'en'`, ngược lại `'vi'`.
  - `export const rewrites = { 'base/:slug*': ':slug*', 'app/:slug*': ':slug*' }`.
- Produces (`sidebar.ts`):
  - `resolveChapters(base: Chapter[], app: Chapter[], hidden: string[]): ResolvedChapter[]`: bỏ chương base có trong `hidden`. Ném `Error`:
    - `Unknown base chapter "<slug>" in hiddenBaseChapters` khi slug không có trong base;
    - `Base chapter "<slug>" is not hideable` khi `hideable` không bằng `true`;
    - `Duplicate chapter slug "<slug>"` khi base và app trùng slug.
  - `buildSidebar(chapters: ResolvedChapter[], locale: Locale): DefaultTheme.SidebarItem[]`:
    - chương có `items` → `{ text, collapsed: true, items: [{ text, link }] }`;
    - chương một trang → `{ text, link }`.
  - `hiddenSourceGlobs(base: Chapter[], hidden: string[]): string[]`:
    - chương thư mục → `base/<slug>/**`, `base/en/<slug>/**`;
    - chương một trang → `base/<slug>.md`, `base/en/<slug>.md`.
  - Xuất sẵn: `chapters`, `sidebar: { vi, en }`, `srcExclude: string[]` (= `['_template.md', ...hiddenSourceGlobs(baseChapters, product.hiddenBaseChapters)]`).
- Produces: `baseChapters: Chapter[]` (`sidebar.base.ts`). Ở giai đoạn 1:
  - `getting-started`, `text { vi: 'Bắt đầu', en: 'Getting started' }`, `items: [{ slug: 'index', text: { vi: 'Giới thiệu', en: 'Introduction' } }]`;
  - `faq`, `text { vi: 'Câu hỏi thường gặp', en: 'FAQ' }`.

  Cả hai không ẩn được.
- Produces: `appChapters: Chapter[] = []` (`sidebar.app.ts`, kèm comment ngắn hướng dẫn fork).

- [ ] **Step 1: Viết test (đỏ)**

```ts
// tests/paths.test.ts
import { expect, test } from 'bun:test'
import { localeOf, pageLink, pageSourcePath, rewritePath } from '../.vitepress/paths'

test('pageSourcePath', () => {
  expect(pageSourcePath('base', 'vi', 'getting-started', 'index')).toBe('base/getting-started/index.md')
  expect(pageSourcePath('base', 'en', 'getting-started', 'sign-in')).toBe('base/en/getting-started/sign-in.md')
  expect(pageSourcePath('app', 'vi', 'faq')).toBe('app/faq.md')
})
test('pageLink', () => {
  expect(pageLink('vi', 'getting-started', 'index')).toBe('/getting-started/')
  expect(pageLink('en', 'getting-started', 'sign-in')).toBe('/en/getting-started/sign-in')
  expect(pageLink('vi', 'faq')).toBe('/faq')
})
test('rewritePath strips only the tree prefix', () => {
  expect(rewritePath('base/en/faq.md')).toBe('en/faq.md')
  expect(rewritePath('app/orders/index.md')).toBe('orders/index.md')
})
test('localeOf accepts source and rewritten paths', () => {
  expect(localeOf('base/en/faq.md')).toBe('en')
  expect(localeOf('en/faq.md')).toBe('en')
  expect(localeOf('app/faq.md')).toBe('vi')
  expect(localeOf(undefined)).toBe('vi')
})
```

```ts
// tests/sidebar.test.ts
import { describe, expect, test } from 'bun:test'
import type { Chapter } from '../.vitepress/sidebar-types'
import { buildSidebar, hiddenSourceGlobs, resolveChapters } from '../.vitepress/sidebar'

const t = (vi: string, en: string) => ({ vi, en })
const base: Chapter[] = [
  { slug: 'getting-started', text: t('Bắt đầu', 'Getting started'), items: [{ slug: 'index', text: t('Giới thiệu', 'Introduction') }] },
  { slug: 'organizations', text: t('Tổ chức', 'Organizations'), hideable: true, items: [{ slug: 'add-member', text: t('Thêm thành viên', 'Add a member') }] },
  { slug: 'faq', text: t('Câu hỏi thường gặp', 'FAQ') },
]
const app: Chapter[] = [{ slug: 'orders', text: t('Đơn hàng', 'Orders'), items: [{ slug: 'index', text: t('Tổng quan', 'Overview') }] }]

describe('resolveChapters', () => {
  test('appends app chapters after base and tags the tree', () => {
    expect(resolveChapters(base, app, []).map((c) => `${c.tree}:${c.slug}`)).toEqual([
      'base:getting-started', 'base:organizations', 'base:faq', 'app:orders',
    ])
  })
  test('drops a hidden hideable chapter', () => {
    expect(resolveChapters(base, app, ['organizations']).map((c) => c.slug)).not.toContain('organizations')
  })
  test('rejects hiding a non-hideable chapter', () => {
    expect(() => resolveChapters(base, app, ['faq'])).toThrow('Base chapter "faq" is not hideable')
  })
  test('rejects an unknown hidden slug', () => {
    expect(() => resolveChapters(base, app, ['nope'])).toThrow('Unknown base chapter "nope" in hiddenBaseChapters')
  })
  test('rejects a slug used by both trees', () => {
    expect(() => resolveChapters(base, [{ slug: 'faq', text: t('a', 'b') }], [])).toThrow('Duplicate chapter slug "faq"')
  })
})

describe('buildSidebar', () => {
  test('builds the same shape in both locales with locale links', () => {
    const chapters = resolveChapters(base, app, [])
    const vi = buildSidebar(chapters, 'vi')
    const en = buildSidebar(chapters, 'en')
    expect(vi[0]).toEqual({ text: 'Bắt đầu', collapsed: true, items: [{ text: 'Giới thiệu', link: '/getting-started/' }] })
    expect(en[1].items?.[0]).toEqual({ text: 'Add a member', link: '/en/organizations/add-member' })
    expect(vi[2]).toEqual({ text: 'Câu hỏi thường gặp', link: '/faq' })
    expect(en.length).toBe(vi.length)
  })
})

describe('hiddenSourceGlobs', () => {
  test('covers both locales of a hidden chapter and nothing else', () => {
    const globs = hiddenSourceGlobs(base, ['organizations']).map((g) => new Bun.Glob(g))
    const hit = (p: string) => globs.some((g) => g.match(p))
    expect(hit('base/organizations/add-member.md')).toBe(true)
    expect(hit('base/en/organizations/add-member.md')).toBe(true)
    expect(hit('base/getting-started/index.md')).toBe(false)
    expect(hit('app/organizations/x.md')).toBe(false)
  })
})
```

- [ ] **Step 2: Chạy test, xác nhận đỏ**

Run: `bun run --cwd frontend/user-guide/site test tests/paths.test.ts tests/sidebar.test.ts`
Expected: FAIL, không resolve được module.

- [ ] **Step 3: Implement `sidebar-types.ts`, `paths.ts`, `sidebar.base.ts`, `sidebar.app.ts`, `sidebar.ts`**

- [ ] **Step 4: Chạy test, xác nhận xanh**

Run: `bun run --cwd frontend/user-guide/site test tests/paths.test.ts tests/sidebar.test.ts`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add frontend/user-guide/site/.vitepress/paths.ts frontend/user-guide/site/.vitepress/sidebar-types.ts \
  frontend/user-guide/site/.vitepress/sidebar.base.ts frontend/user-guide/site/.vitepress/sidebar.app.ts \
  frontend/user-guide/site/.vitepress/sidebar.ts \
  frontend/user-guide/site/tests/paths.test.ts frontend/user-guide/site/tests/sidebar.test.ts
git commit -m "feat(guide): compose the sidebar from base and fork chapters"
```

---

### Task 4: Markdown và tìm kiếm theo ngôn ngữ

**Files:**
- Create: `frontend/user-guide/site/.vitepress/markdown.ts`
- Create: `frontend/user-guide/site/.vitepress/search.ts`
- Create: `frontend/user-guide/site/tests/markdown.test.ts`, `frontend/user-guide/site/tests/search.test.ts`

**Interfaces:**
- Consumes: `localeOf` (Task 3).
- Produces (`markdown.ts`): `export const markdown: MarkdownOptions` gồm:
  - `image: { lazyLoading: true }`;
  - `container: { infoLabel: 'Lưu ý', tipLabel: 'Mẹo', warningLabel: 'Quan trọng', dangerLabel: 'Cảnh báo', detailsLabel: 'Chi tiết' }`;
  - `anchor.permalink`: chuyển từ CIMP (`config.mts` dòng `permalink:`). `aria-label` lấy chữ đã render của tiêu đề: `vi` = `Liên kết tới mục "<title>"`, `en` = `Link to section "<title>"`, chọn theo `localeOf(state.env.relativePath)`;
  - `config(md)`:
    - bọc `<table>` trong `<div class="guide-table">` (renderer `table_open`/`table_close` như CIMP);
    - core rule `guide-container-labels`: với trang `en`, token `container_<info|tip|warning|danger|details>_open` có `info.trim()` đúng bằng tên loại thì đổi `info` thành `<loại> <nhãn en>`. Nhãn `en`: Note / Tip / Important / Warning / Details.
  - Không dùng `attrs` delimiter `{%…%}` của CIMP (base không có generator).
- Produces (`search.ts`):
  - `processTerm(term: string): string`: lowercase → NFD → bỏ `[̀-ͯ]` → `đ`→`d`. Thân hàm tự chứa: VitePress chuyển hàm thành chuỗi để chạy trên trình duyệt.
  - `export const search: DefaultTheme.Config['search']`: `provider: 'local'`, `options.miniSearch.options.processTerm`, `options.locales.root.translations` (chuyển nguyên khối `translations` tiếng Việt của CIMP). `en` dùng mặc định.

- [ ] **Step 1: Viết test (đỏ)**

```ts
// tests/markdown.test.ts
import { describe, expect, test } from 'bun:test'
import { createMarkdownRenderer } from 'vitepress'
import { markdown } from '../.vitepress/markdown'

const md = await createMarkdownRenderer('src', markdown, '/guide/')
const render = (src: string, relativePath: string) => md.render(src, { relativePath })

describe('callout titles', () => {
  test('Vietnamese pages get Vietnamese titles', () => {
    expect(render('::: tip\nx\n:::', 'base/getting-started/index.md')).toContain('<p class="custom-block-title">Mẹo</p>')
  })
  test('English pages get English titles', () => {
    const html = render('::: warning\nx\n:::', 'base/en/getting-started/index.md')
    expect(html).toContain('<p class="custom-block-title">Important</p>')
  })
  test('an explicit title is kept', () => {
    expect(render('::: warning Heads up\nx\n:::', 'base/en/faq.md')).toContain('<p class="custom-block-title">Heads up</p>')
  })
})

test('tables are wrapped for horizontal scrolling', () => {
  expect(render('| a |\n|---|\n| b |', 'base/faq.md')).toMatch(/<div class="guide-table"><table>[\s\S]*<\/table><\/div>/)
})

test('heading permalink label uses the rendered title in the page language', () => {
  expect(render('## Đổi `mật khẩu`', 'base/faq.md')).toContain('aria-label="Liên kết tới mục &quot;Đổi mật khẩu&quot;"')
  expect(render('## Sessions', 'base/en/faq.md')).toContain('aria-label="Link to section &quot;Sessions&quot;"')
})
```

```ts
// tests/search.test.ts
import { expect, test } from 'bun:test'
import { processTerm } from '../.vitepress/search'

test('processTerm strips Vietnamese diacritics', () => {
  expect(processTerm('Hà Nội')).toBe('ha noi')
  expect(processTerm('ĐĂNG NHẬP')).toBe('dang nhap')
  expect(processTerm('Thành viên')).toBe('thanh vien')
})
test('processTerm is self-contained', () => {
  // VitePress ships the function as source text; it must not close over module scope.
  const rebuilt = new Function(`return (${processTerm.toString()})`)()
  expect(rebuilt('Tổ chức')).toBe('to chuc')
})
```

Nếu chuỗi `aria-label` thực tế escape khác (`&quot;` hay `"`), sửa assertion cho khớp với cách markdown-it escape thuộc tính. Đừng nới lỏng phần chữ tiêu đề.

- [ ] **Step 2: Chạy test, xác nhận đỏ**

Run: `bun run --cwd frontend/user-guide/site test tests/markdown.test.ts tests/search.test.ts`
Expected: FAIL, không resolve được module.

- [ ] **Step 3: Implement `markdown.ts` và `search.ts`**

- [ ] **Step 4: Chạy test, xác nhận xanh**

Run: `bun run --cwd frontend/user-guide/site test tests/markdown.test.ts tests/search.test.ts`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add frontend/user-guide/site/.vitepress/markdown.ts frontend/user-guide/site/.vitepress/search.ts \
  frontend/user-guide/site/tests/markdown.test.ts frontend/user-guide/site/tests/search.test.ts
git commit -m "feat(guide): localize callout titles, permalinks and search"
```

---

### Task 5: Ghép config, trang mẫu, test toàn vẹn nội dung

**Files:**
- Modify: `frontend/user-guide/site/.vitepress/config.mts`
- Create: `frontend/user-guide/site/.vitepress/theme/permission-labels.ts`
- Create: `frontend/user-guide/site/tests/lib/content.ts`
- Create: `frontend/user-guide/site/tests/content.test.ts`, `frontend/user-guide/site/tests/content-integrity.test.ts`
- Create: `frontend/user-guide/site/src/base/getting-started/index.md`, `frontend/user-guide/site/src/base/en/getting-started/index.md`
- Create: `frontend/user-guide/site/src/base/faq.md`, `frontend/user-guide/site/src/base/en/faq.md`
- Create: `frontend/user-guide/site/src/base/images/vi/getting-started/overview.webp`, `frontend/user-guide/site/src/base/images/en/getting-started/overview.webp`

**Interfaces:**
- Consumes: `rewrites`, `pageLink`, `rewritePath`, `pageSourcePath` (Task 3); `chapters`, `sidebar`, `srcExclude` (Task 3); `markdown` (Task 4); `search` (Task 4); `product`, `resolveVersion` (Task 1).
- Produces (`permission-labels.ts`): `export const permissionLabels: Record<string, Localized>` cho đúng ba mã trong `frontend/src/lib/permissions.ts`:
  - `organizations.manage` → Quản lý tổ chức / Manage organizations;
  - `organizations.members.manage` → Quản lý thành viên / Manage members;
  - `organizations.roles.manage` → Quản lý vai trò / Manage roles.
- Produces (`tests/lib/content.ts`):
  - `listPages(srcDir: string): string[]`: đường dẫn tương đối của mọi `.md` dưới `base/` và `app/`, bỏ thư mục `images/`, sắp xếp.
  - `counterpart(page: string): string`: `base/x.md` ↔ `base/en/x.md`.
  - `parseFrontmatter(source: string): Record<string, string>`: các dòng `key: value` giữa hai dòng `---` đầu file, bỏ nháy bao quanh.
  - `extractImages(source: string): { alt: string; src: string }[]`: ảnh markdown `![alt](src)`.
  - `extractLinks(source: string): string[]`: link markdown không phải ảnh, bỏ link ngoài (`scheme:` hoặc `//`) và phần `#anchor`. Link rỗng sau khi bỏ anchor thì bỏ qua.
  - `resolveLink(page: string, link: string): string`: URL sau rewrite, không đuôi, dạng `/en/organizations/add-member` hoặc `/getting-started/`. Link tương đối tính từ `rewritePath(page)`.
  - `chapterOfUrl(url: string): string`: segment đầu sau khi bỏ `/en`.

- [ ] **Step 1: Viết test helper (đỏ)**

```ts
// tests/content.test.ts
import { expect, test } from 'bun:test'
import { chapterOfUrl, counterpart, extractImages, extractLinks, parseFrontmatter, resolveLink } from './lib/content'

test('counterpart', () => {
  expect(counterpart('base/faq.md')).toBe('base/en/faq.md')
  expect(counterpart('app/en/orders/index.md')).toBe('app/orders/index.md')
})
test('parseFrontmatter', () => {
  expect(parseFrontmatter('---\ntitle: "Thêm thành viên"\npermission: organizations.members.manage\n---\n# x')).toEqual({
    title: 'Thêm thành viên',
    permission: 'organizations.members.manage',
  })
  expect(parseFrontmatter('# no frontmatter')).toEqual({})
})
test('extractImages and extractLinks', () => {
  const src = '![Màn hình](/base/images/vi/a/b.jpg) [x](../organizations/add-member#step) [y](https://e.com) [z](#top)'
  expect(extractImages(src)).toEqual([{ alt: 'Màn hình', src: '/base/images/vi/a/b.jpg' }])
  expect(extractLinks(src)).toEqual(['../organizations/add-member'])
})
test('resolveLink uses the rewritten location', () => {
  expect(resolveLink('base/en/getting-started/index.md', '../organizations/add-member')).toBe('/en/organizations/add-member')
  expect(resolveLink('base/faq.md', './getting-started/')).toBe('/getting-started/')
  expect(resolveLink('base/faq.md', '/en/faq')).toBe('/en/faq')
})
test('chapterOfUrl', () => {
  expect(chapterOfUrl('/en/organizations/add-member')).toBe('organizations')
  expect(chapterOfUrl('/faq')).toBe('faq')
})
```

- [ ] **Step 2: Chạy test, xác nhận đỏ**

Run: `bun run --cwd frontend/user-guide/site test tests/content.test.ts`
Expected: FAIL, không resolve được `./lib/content`.

- [ ] **Step 3: Implement `tests/lib/content.ts`**

- [ ] **Step 4: Chạy test, xác nhận xanh**

Run: `bun run --cwd frontend/user-guide/site test tests/content.test.ts`
Expected: PASS.

- [ ] **Step 5: Viết `content-integrity.test.ts` (chạy trên `src/` thật)**

Trang bị loại là trang khớp một glob trong `srcExclude` (dùng `Bun.Glob`). Trang bị loại bỏ qua mọi kiểm tra. Các test, đặt tên đúng như sau:

- `every page exists in both languages`: mỗi trang còn lại có `counterpart` tồn tại.
- `every page is in the sidebar and every sidebar link has both files`: tập `'/' + rewritePath(page)` (bỏ `.md`, `index` → `/`) bằng tập link của `sidebar.vi` ∪ `sidebar.en`.
- `every page has a title and a description`: `parseFrontmatter` có `title` và `description` khác rỗng.
- `every image exists and has alt text`: ảnh `/x` resolve từ `src/`, ảnh tương đối resolve từ thư mục của file nguồn. Alt sau khi `trim()` khác rỗng.
- `every permission is a known code with labels`: `permission` (nếu có) thuộc `Object.values(Permissions)`, import từ `../../../src/lib/permissions`. Test cũng kiểm `Object.keys(permissionLabels).sort()` bằng `Object.values(Permissions).sort()`.
- `no two pages share a URL`: không có hai trang cùng `rewritePath`.
- `no cross-chapter link into a hideable chapter`: với mỗi link nội bộ, nếu `chapterOfUrl(target)` khác chương của trang và chương đích có `hideable: true` thì lỗi.
- Ảnh có trong `images/` mà không trang nào tham chiếu: `console.warn`, không làm test đỏ.

Message lỗi của mỗi test liệt kê đường dẫn vi phạm (ví dụ `expect(missing).toEqual([])`).

- [ ] **Step 6: Chạy test, xác nhận đỏ trên `src/` hiện tại**

Run: `bun run --cwd frontend/user-guide/site test tests/content-integrity.test.ts`
Expected: FAIL ở test sidebar: link `/getting-started/` và `/faq` chưa có file.

- [ ] **Step 7: Viết trang mẫu và ghép config**

- `src/base/getting-started/index.md` (vi) và `src/base/en/getting-started/index.md` (en):
  - `title`, `description`;
  - một đoạn giới thiệu sản phẩm;
  - một `<figure>` dùng `![…](/base/images/<locale>/getting-started/overview.webp)` kèm `<figcaption>`;
  - một callout `::: tip`;
  - một link tới `../faq`.
- Ảnh: chép `frontend/public/weatherplus-wordmark-light.webp` thành `overview.webp` cho cả hai locale. Đây là ảnh tạm, giai đoạn 4 thay bằng ảnh chụp thật.
- `src/base/faq.md` / `src/base/en/faq.md`: `title`, `description`, hai câu hỏi, một link về `./getting-started/`.
- `config.mts`: thêm `rewrites`, `srcExclude`, `markdown`. `locales.root.themeConfig` và `locales.en.themeConfig` gồm:
  - `sidebar` (từ Task 3);
  - `outline: { level: [2, 3], label }`, `docFooter`, `darkModeSwitchLabel`, `lightModeSwitchTitle`, `darkModeSwitchTitle`, `sidebarMenuLabel`, `returnToTopLabel`, `skipToContentLabel`, `notFound`. Bản `vi` lấy nguyên chữ của CIMP; bản `en` dùng chữ tiếng Anh tương đương.

  `themeConfig` chung gồm `search` (Task 4) và `externalLinkIcon: false`.

- [ ] **Step 8: Chạy toàn bộ test và build**

Run: `bun run --cwd frontend/user-guide/site test && bun run --cwd frontend/user-guide/site build`
Expected: mọi test PASS. Build xanh, không có cảnh báo dead link.

- [ ] **Step 9: Xác nhận đường dẫn ảnh tuyệt đối (rủi ro §12 của spec)**

Run: `ls frontend/user-guide/site/dist/assets | grep overview && grep -o '/guide/assets/overview[^"]*' frontend/user-guide/site/dist/en/getting-started/index.html`
Expected: có file `overview.<hash>.webp` trong `dist/assets/`, và trang `en` trỏ tới nó.

Nếu không đạt: đổi quy ước sang đường dẫn tương đối từ file nguồn, cập nhật Global Constraints của plan này và §4.1 của spec, rồi báo người dùng trước khi làm tiếp.

- [ ] **Step 10: Commit**

```bash
git add frontend/user-guide/site/.vitepress/config.mts frontend/user-guide/site/.vitepress/theme/permission-labels.ts \
  frontend/user-guide/site/tests/lib/content.ts frontend/user-guide/site/tests/content.test.ts \
  frontend/user-guide/site/tests/content-integrity.test.ts \
  frontend/user-guide/site/src/base/getting-started/index.md frontend/user-guide/site/src/base/en/getting-started/index.md \
  frontend/user-guide/site/src/base/faq.md frontend/user-guide/site/src/base/en/faq.md \
  frontend/user-guide/site/src/base/images/vi/getting-started/overview.webp \
  frontend/user-guide/site/src/base/images/en/getting-started/overview.webp
git commit -m "feat(guide): wire locales and sample pages behind a content integrity test"
```

---

### Task 6: Theme: thương hiệu, phiên bản, mở ứng dụng, quyền, accordion

**Files:**
- Create: `frontend/user-guide/site/.vitepress/theme/index.ts`
- Create: `frontend/user-guide/site/.vitepress/theme/env.d.ts`
- Create: `frontend/user-guide/site/.vitepress/theme/strings.ts`
- Create: `frontend/user-guide/site/.vitepress/theme/brand.css`, `frontend/user-guide/site/.vitepress/theme/custom.css`
- Create: `frontend/user-guide/site/.vitepress/theme/sidebar-accordion.ts`
- Create: `frontend/user-guide/site/.vitepress/theme/components/AppLink.vue`, `frontend/user-guide/site/.vitepress/theme/components/PermissionNote.vue`, `frontend/user-guide/site/.vitepress/theme/components/HeroTitle.vue`
- Create: `frontend/user-guide/site/tests/strings.test.ts`
- Modify: `frontend/user-guide/site/.vitepress/config.mts` (`themeConfig.siteTitle: false`, `footer` theo locale)
- Modify: `frontend/user-guide/site/src/index.md`, `frontend/user-guide/site/src/en/index.md` (`hero.actions`)

**Interfaces:**
- Consumes: `__GUIDE__` (Task 1); `permissionLabels` (Task 5); `localeOf`, `Localized`, `Locale` (Task 3); `brand-tokens.generated.css` (Task 2).
- Produces (`env.d.ts`): `declare const __GUIDE__: { name: string; publisher: string; version: string; brand: ProductBrand; hiddenBaseChapters: string[] }`.
- Produces (`strings.ts`): `export const strings: Record<Locale, { guide: string; openApp: string; requiresPermission: string; versionTitle: string }>`:
  - `vi`: Hướng dẫn sử dụng / Mở ứng dụng / Cần quyền / Áp dụng cho phiên bản;
  - `en`: User guide / Open app / Requires permission / Applies to version.
- Produces (`theme/index.ts`): `extends: DefaultTheme` từ `vitepress/theme-without-fonts`; import Roboto 400/500/700, `brand-tokens.generated.css`, `brand.css`, `custom.css`. `Layout` gọi `useSidebarAccordion()` và điền các slot:
  - `nav-bar-title-before` → khối thương hiệu: hai ảnh `withBase('/brand/' + __GUIDE__.brand.wordmarkLight|wordmarkDark)` kèm dòng `strings[lang].guide + ' ' + __GUIDE__.name`;
  - `nav-bar-title-after` → badge phiên bản (`class="guide-version guide-version--title"`, `title` = `versionTitle + ' ' + version`);
  - `nav-bar-content-before` → `<AppLink />`;
  - `nav-bar-content-after` → badge phiên bản (`guide-version--nav`);
  - `doc-before` → `<PermissionNote />`;
  - `home-hero-info` → `<HeroTitle />`;
  - `home-hero-actions-after` → `<AppLink variant="button" />`.

  Chưa có `enhanceApp`; auth guard thuộc giai đoạn 2.
- Produces: `AppLink.vue`: thẻ `<a href="/" target="_self">` với chữ `strings[locale].openApp`; prop `variant?: 'nav' | 'button'`.
- Produces: `PermissionNote.vue`: render khi `frontmatter.permission` có giá trị, dạng `<div class="guide-permission">{requiresPermission}: <strong>{label}</strong></div>`. `label` lấy từ `permissionLabels[code][locale]`, không có thì hiện chính mã quyền.
- Produces: `HeroTitle.vue`: `<h1 class="guide-hero-title">{frontmatter.hero.name} <span class="guide-hero-product">{__GUIDE__.name}</span></h1>` cộng `<p class="tagline">`.
- Locale của component: `localeOf(page.value.relativePath)` từ `useData()`.

- [ ] **Step 1: Viết test cho `strings.ts` (đỏ)**

```ts
// tests/strings.test.ts
import { expect, test } from 'bun:test'
import { strings } from '../.vitepress/theme/strings'

test('every UI string exists in both languages and is not blank', () => {
  expect(Object.keys(strings.vi).sort()).toEqual(Object.keys(strings.en).sort())
  for (const locale of ['vi', 'en'] as const)
    for (const value of Object.values(strings[locale])) expect(value.trim()).not.toBe('')
})
```

- [ ] **Step 2: Chạy test, xác nhận đỏ**

Run: `bun run --cwd frontend/user-guide/site test tests/strings.test.ts`
Expected: FAIL, không resolve được module.

- [ ] **Step 3: Implement `strings.ts`, chạy lại test**

Run: `bun run --cwd frontend/user-guide/site test tests/strings.test.ts`
Expected: PASS.

- [ ] **Step 4: Chuyển CSS và accordion từ CIMP**

- `sidebar-accordion.ts`: chép nguyên từ CIMP, dịch comment sang tiếng Anh.
- `custom.css`: chép từ CIMP rồi:
  - bỏ mọi luật `.cimp-pdf*` và `.cimp-pdf-screen*`;
  - đổi prefix `cimp-` thành `guide-`;
  - thay mọi hex màu bằng biến `--brand-*` hoặc `--vp-c-*`;
  - thêm `.guide-permission`, `.guide-hero-title`, `.guide-hero-product` (gradient lá → tím nhạt), và nền hero theo §5.2 của spec.
- `brand.css`: bảng ánh xạ §5.1 của spec (sáng: `--vp-c-brand-1/2/3` = violet-600/700/500, `--vp-sidebar-bg-color` = violet-900; tối: `--vp-c-brand-1` = `--brand-primary-200-dark`, nền `--vp-c-bg` = `--brand-surface-950`, `--vp-c-bg-alt`/`--vp-c-bg-elv` = `--brand-surface-900`). Nút `.VPButton.brand` trên trang chủ: nền leaf-400, chữ violet-950. Kiểm: `grep -E '#[0-9a-fA-F]{3,8}' frontend/user-guide/site/.vitepress/theme/brand.css` không ra dòng nào.

- [ ] **Step 5: Implement `env.d.ts`, ba component, `theme/index.ts`; cập nhật `config.mts` và hai trang chủ**

- `config.mts`: `themeConfig.siteTitle: false`. `footer.message` theo locale: `© <publisher> · Phiên bản <version>` / `© <publisher> · Version <version>`.
- `hero.actions`: một nút `theme: brand`, chữ "Bắt đầu" / "Get started", link `/getting-started/` / `/en/getting-started/`. Nút "Mở ứng dụng" do slot render.

- [ ] **Step 6: Build và kiểm HTML (Review Focus 1)**

Run:
```bash
bun run --cwd frontend/user-guide/site test && bun run --cwd frontend/user-guide/site build
grep -o 'href="/guide/en/getting-started/"' frontend/user-guide/site/dist/getting-started/index.html | head -1
grep -o 'href="/guide/getting-started/"' frontend/user-guide/site/dist/en/getting-started/index.html | head -1
grep -c 'href="/" target="_self"' frontend/user-guide/site/dist/en/getting-started/index.html
```
Expected:
- test và build xanh;
- lệnh grep thứ nhất và thứ hai đều in ra đúng một dòng (link chuyển ngôn ngữ trỏ tới trang tương ứng);
- lệnh grep thứ ba ≥ 1.

- [ ] **Step 7: Kiểm bằng mắt trên dev server**

Run: `bun run --cwd frontend/user-guide/site dev`, mở `http://localhost:5174/guide/`. Kiểm từng mục:
- [ ] wordmark màu ở chế độ sáng, wordmark trắng ở chế độ tối;
- [ ] badge phiên bản hiện;
- [ ] sidebar màu violet-900;
- [ ] chuyển `vi` ↔ `en` ở `/guide/getting-started/` giữ đúng trang;
- [ ] callout có tiêu đề đúng ngôn ngữ;
- [ ] gõ "bat dau" tìm ra trang "Bắt đầu";
- [ ] mở/đóng chương trong sidebar theo kiểu accordion.

Ghi lại mục nào không đạt và sửa trước khi commit.

- [ ] **Step 8: Commit**

```bash
git add frontend/user-guide/site/.vitepress/theme frontend/user-guide/site/tests/strings.test.ts \
  frontend/user-guide/site/.vitepress/config.mts frontend/user-guide/site/src/index.md frontend/user-guide/site/src/en/index.md
git status --short frontend/user-guide/site/.vitepress/theme   # brand-tokens.generated.css must not be staged
git commit -m "feat(guide): brand the theme and add version, app link and permission note"
```

---

### Task 7: Template trang và script `new-page`

**Files:**
- Create: `frontend/user-guide/site/src/_template.md`
- Create: `frontend/user-guide/site/scripts/new-page.ts`
- Create: `frontend/user-guide/site/tests/new-page.test.ts`

**Interfaces:**
- Consumes: `pageSourcePath`, `Tree` (Task 3).
- Produces (`scripts/new-page.ts`):
  - `parseTarget(target: string): { chapter: string; item?: string }`: `'organizations/add-member'` → `{ chapter: 'organizations', item: 'add-member' }`; `'faq'` → `{ chapter: 'faq' }`. Mỗi segment phải khớp `/^[a-z0-9]+(-[a-z0-9]+)*$/` và có tối đa hai segment; sai thì ném `Error('Invalid page path "<target>": use kebab-case "chapter" or "chapter/page"')`.
  - `createPage(opts: { srcDir: string; target: string; tree: Tree }): { created: string[]; sidebarSnippet: string }`:
    - đọc `<srcDir>/_template.md`, ghi ra `pageSourcePath(tree,'vi',…)` và `pageSourcePath(tree,'en',…)`, tạo thư mục khi cần;
    - kiểm cả hai đích trước khi ghi: có một file đã tồn tại thì ném `Error('Page already exists: <đường dẫn>')` và không ghi gì;
    - `created` là đường dẫn tương đối từ `srcDir`;
    - `sidebarSnippet` là `{ slug: '<item>', text: { vi: '', en: '' } },` cho trang trong chương, hoặc `{ slug: '<chapter>', text: { vi: '', en: '' } },` cho chương một trang.
  - CLI (`import.meta.main`): `bun run new-page <target> [--app]`. In các file đã tạo và đoạn sidebar cần dán vào `sidebar.base.ts` (hoặc `sidebar.app.ts` khi có `--app`). Lỗi thì in message và `process.exit(1)`.
- Produces: `src/_template.md`: nguyên văn khối template ở §7.1 của spec (bản đã sửa: placeholder có nháy, `permission` bị comment, không có placeholder dạng `<chữ>`). Đã nằm trong `srcExclude` (Task 3), và `listPages` bỏ qua vì file không nằm dưới `base/` hay `app/`.

- [ ] **Step 1: Viết test (đỏ)**

```ts
// tests/new-page.test.ts
import { beforeEach, describe, expect, test } from 'bun:test'
import { existsSync, mkdirSync, mkdtempSync, readFileSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { createPage, parseTarget } from '../scripts/new-page'

let srcDir: string
beforeEach(() => {
  srcDir = mkdtempSync(join(tmpdir(), 'guide-src-'))
  writeFileSync(join(srcDir, '_template.md'), '---\ntitle: <t>\n---\n')
})

describe('parseTarget', () => {
  test('accepts chapter and chapter/page', () => {
    expect(parseTarget('organizations/add-member')).toEqual({ chapter: 'organizations', item: 'add-member' })
    expect(parseTarget('faq')).toEqual({ chapter: 'faq' })
  })
  test.each(['Organizations/x', 'tổ-chức', 'a/b/c', 'a//b', '', 'a_b'])('rejects %p', (bad) => {
    expect(() => parseTarget(bad)).toThrow('Invalid page path')
  })
})

describe('createPage', () => {
  test('writes both languages from the template', () => {
    const { created, sidebarSnippet } = createPage({ srcDir, target: 'orders/create', tree: 'app' })
    expect(created).toEqual(['app/orders/create.md', 'app/en/orders/create.md'])
    expect(readFileSync(join(srcDir, 'app/en/orders/create.md'), 'utf8')).toBe('---\ntitle: <t>\n---\n')
    expect(sidebarSnippet).toBe("{ slug: 'create', text: { vi: '', en: '' } },")
  })
  test('refuses when only one language already exists and writes nothing', () => {
    mkdirSync(join(srcDir, 'base/en'), { recursive: true })
    writeFileSync(join(srcDir, 'base/en/faq.md'), 'mine')
    expect(() => createPage({ srcDir, target: 'faq', tree: 'base' })).toThrow('Page already exists: base/en/faq.md')
    expect(existsSync(join(srcDir, 'base/faq.md'))).toBe(false)
    expect(readFileSync(join(srcDir, 'base/en/faq.md'), 'utf8')).toBe('mine')
  })
})
```

- [ ] **Step 2: Chạy test, xác nhận đỏ**

Run: `bun run --cwd frontend/user-guide/site test tests/new-page.test.ts`
Expected: FAIL, không resolve được `../scripts/new-page`.

- [ ] **Step 3: Implement `scripts/new-page.ts`, viết `src/_template.md`**

- [ ] **Step 4: Chạy test, xác nhận xanh; kiểm template không lên site**

Run: `bun run --cwd frontend/user-guide/site test && bun run --cwd frontend/user-guide/site build && test ! -e frontend/user-guide/site/dist/_template.html && echo OK`
Expected: test PASS, build xanh, in ra `OK`.

- [ ] **Step 5: Trang tạo từ template thật phải build được**

Run:
```bash
cd frontend/user-guide/site
bun run new-page scratch-check
bun run build; status=$?
rm -f src/base/scratch-check.md src/base/en/scratch-check.md
cd - && test $status -eq 0 && echo BUILD-OK
```
Expected: `new-page` in ra hai file và đoạn sidebar; build xanh (trang chưa có trong sidebar không làm build đỏ); in ra `BUILD-OK`. Không chạy `test` ở bước này: `content-integrity` sẽ đỏ đúng như thiết kế vì trang chưa có trong sidebar. Sau khi dọn, `git status --short frontend/user-guide/site/src` không còn file `scratch-check`.

- [ ] **Step 6: Commit**

```bash
git add frontend/user-guide/site/src/_template.md frontend/user-guide/site/scripts/new-page.ts \
  frontend/user-guide/site/tests/new-page.test.ts
git commit -m "feat(guide): add the page template and the new-page scaffold script"
```

---

### Task 8: CI, hook và tài liệu

**Files:**
- Modify: `.github/workflows/test.yml` (thêm job `guide` sau job `frontend`)
- Modify: `.github/workflows/ci.yml:3` (comment status check bắt buộc)
- Modify: `.gitlab-ci.yml` (job `test`, sau các lệnh frontend ở dòng 54–58)
- Modify: `scripts/test-gate.sh` (cuối file)
- Modify: `lefthook.yml` (khối `pre-commit.commands`)
- Modify: `CONTRIBUTING.md:62` (danh sách status check)
- Modify: `.claude/rules/commands.md` (thêm mục `## User guide (frontend/user-guide/site/)` sau mục Frontend; cập nhật đoạn pre-commit ở `## Git Hooks`)
- Modify: `RENAMING.md`, `README.md`
- Create: `frontend/user-guide/README.md`

**Interfaces:**
- Consumes: các script `test`, `build`, `format:check`, `new-page` (Task 1, Task 7).

- [ ] **Step 1: Thêm job CI và lệnh vào test gate**

- `test.yml`, job `guide`, `runs-on: ubuntu-latest`. Các bước:
  - `actions/checkout@v4` với `fetch-depth: 0` (comment: version cần tag);
  - `actions/setup-node@v4` `node-version: "22"`;
  - `oven-sh/setup-bun@v2` `bun-version: "1.3.14"`;
  - `bun install --cwd frontend/user-guide/site --frozen-lockfile`;
  - `bun run --cwd frontend/user-guide/site format:check`;
  - `bun run --cwd frontend/user-guide/site test`;
  - `bun run --cwd frontend/user-guide/site build`.
- `.gitlab-ci.yml` và `scripts/test-gate.sh`: thêm bốn lệnh bun ở trên, cùng thứ tự. Comment đầu `test.yml` yêu cầu đổi cả ba nơi cùng lúc.
- `ci.yml` và `CONTRIBUTING.md`: thêm `test / guide` vào danh sách status check bắt buộc.

- [ ] **Step 2: Thêm lệnh prettier cho site vào lefthook**

```yaml
    prettier-guide:
      priority: 5
      root: "frontend/user-guide/site/"
      glob:
        - "frontend/user-guide/site/*.{ts,mts,vue,css,md,json}"
        - "frontend/user-guide/site/**/*.{ts,mts,vue,css,md,json}"
      run: bunx prettier --write {staged_files}
      stage_fixed: true
```

`root` cho lệnh chạy trong thư mục site, nên `bunx` dùng prettier 3.8.4 của site. Dù dùng `root`, glob vẫn khớp theo đường dẫn tính từ gốc repo; bước 3 sẽ xác nhận điều này.

- [ ] **Step 3: Kiểm hook và gate**

Run:
```bash
bun run --cwd frontend/user-guide/site format && git diff --stat frontend/user-guide/site
printf '# x\n\n\n\nmore\n' >> frontend/user-guide/site/src/base/faq.md && git add frontend/user-guide/site/src/base/faq.md
bunx lefthook run pre-commit
git diff --cached frontend/user-guide/site/src/base/faq.md
```

Expected:
- `format` không đổi file nào (code của các task trước đã đúng định dạng); nếu có thay đổi, commit riêng với message `style(guide): apply prettier`;
- sau `lefthook run`, ba dòng trống liên tiếp đã được prettier gộp lại trong bản staged.

Nếu hook không chạy cho file này: đổi glob thành tương đối với `root` (`"*.{…}"`, `"**/*.{…}"`) rồi chạy lại.

Dọn: `git restore --staged frontend/user-guide/site/src/base/faq.md && git checkout frontend/user-guide/site/src/base/faq.md`.

Rồi chạy: `bun run --cwd frontend/user-guide/site format:check && bun run --cwd frontend/user-guide/site test && bun run --cwd frontend/user-guide/site build`
Expected: cả ba xanh.

- [ ] **Step 4: Viết tài liệu**

- `frontend/user-guide/README.md`, các mục:
  1. Chạy và build (`dev`, `build`, `preview`, `test`; dev qua app tại `/guide/` thuộc giai đoạn 2).
  2. Quy ước sở hữu `[base]`/`[fork]` (bảng file từ §3 của spec).
  3. Thêm trang: `new-page`, dán sidebar, viết theo template.
  4. Style guide: nguyên văn các gạch đầu dòng §7.2 của spec.
  5. Ảnh: thư mục, đường dẫn tuyệt đối, alt.
  6. Ẩn chương base: `hiddenBaseChapters`, `hideable`, quy tắc link giữa các chương.
  7. Test làm gì và đọc lỗi thế nào.
- `RENAMING.md`: thêm `frontend/user-guide/site/.vitepress/product.ts` (`name`, `publisher`, `brand`), `src/index.md`, `src/en/index.md`, kèm một câu về quy ước `[base]`/`[fork]`.
- `README.md` (gốc): mục ngắn "Hướng dẫn sử dụng / User guide" trỏ tới `frontend/user-guide/README.md`.
- `.claude/rules/commands.md`: mục mới gồm các lệnh `install`, `dev`, `build`, `test`, `format`, `new-page`; đoạn pre-commit nhắc thêm `prettier-guide`.

- [ ] **Step 5: Chạy lại toàn bộ kiểm tra của cả hai phía**

Run: `bun run --cwd frontend/user-guide/site test && bun run --cwd frontend/user-guide/site build && bun run --cwd frontend test:run`
Expected: tất cả PASS.

- [ ] **Step 6: Commit**

```bash
git add .github/workflows/test.yml .github/workflows/ci.yml .gitlab-ci.yml scripts/test-gate.sh lefthook.yml \
  CONTRIBUTING.md .claude/rules/commands.md RENAMING.md README.md frontend/user-guide/README.md
git commit -m "ci(guide): build and test the user guide in the gate and document it"
```
