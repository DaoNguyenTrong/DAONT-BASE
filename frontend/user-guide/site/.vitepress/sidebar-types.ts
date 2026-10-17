export type Tree = 'base' | 'app'
export type Locale = 'vi' | 'en'
export type Localized = { vi: string; en: string }

/** A page of a chapter; slug `index` is the chapter's landing page. */
export type ChapterItem = { slug: string; text: Localized }

/**
 * A chapter with `items` is a folder of pages; one without is a single `<slug>.md` page.
 * `hideable` chapters may be listed in `product.hiddenBaseChapters` — and so must not be linked to
 * from other chapters (tests/content-integrity.test.ts enforces it).
 */
export type Chapter = { slug: string; text: Localized; items?: ChapterItem[]; hideable?: boolean }

export type ResolvedChapter = Chapter & { tree: Tree }
