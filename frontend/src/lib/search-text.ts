import Fuse, { type IFuseOptions } from 'fuse.js'

/**
 * The matching rule for free-text pickers: case- and Vietnamese-accent-insensitive, words in any
 * order. Typing `nguyen an` finds `Nguyễn Văn An` — on real keyboards people often skip the
 * accents, so one rule everywhere spares them guessing which box insists on exact diacritics.
 */

const COMBINING_MARKS = /\p{M}/gu
const NOT_LETTER_OR_DIGIT = /[^\p{L}\p{N}]+/gu

/**
 * Lowercase, strip diacritics, and collapse everything that isn't a letter or digit into one
 * space. `đ` is mapped separately: it has no combining-mark decomposition, so stripping marks
 * alone would leave `đien bien`, which `dien bien` never matches.
 */
export function normalizeSearchText(value: string | null | undefined): string {
  if (value == null) {
    return ''
  }

  return value
    .normalize('NFD')
    .replace(COMBINING_MARKS, '')
    .toLowerCase()
    .replaceAll('đ', 'd')
    .replace(NOT_LETTER_OR_DIGIT, ' ')
    .trim()
}

/**
 * Every word of `pattern` occurs somewhere in `text`, both normalized. An empty pattern matches
 * everything — the same as clearing the search box.
 */
export function matchesSearchText(text: string | null | undefined, pattern: string): boolean {
  const words = searchWords(pattern)
  return words.length === 0 || containsAllWords(normalizeSearchText(text), words)
}

function searchWords(pattern: string): string[] {
  return normalizeSearchText(pattern).split(' ').filter(Boolean)
}

function containsAllWords(normalizedText: string, words: readonly string[]): boolean {
  return words.every((word) => normalizedText.includes(word))
}

/** Words shorter than this must match exactly: a 3-letter syllable can't tell a typo from another word. */
const MIN_FUZZY_WORD_LENGTH = 4

const FUSE_OPTIONS = {
  keys: ['text'],
  threshold: 0.35,
  ignoreLocation: true,
  useExtendedSearch: true,
} satisfies IFuseOptions<{ text: string }>

/**
 * {@link matchesSearchText} over a fixed list, falling back to fuzzy matching (words of 4+ letters)
 * only when nothing matches: unaccented Vietnamese syllables are short and close (`ha`, `hai`).
 * Matches are computed once per pattern, so each `(pattern, item)` call is a set lookup.
 */
export function createSearchTextMatcher<T>(
  items: readonly T[],
  textOf: (item: T) => string | null | undefined,
): (pattern: string, item: T) => boolean {
  type Entry = { item: T; text: string }
  let entries: Entry[] | null = null
  let fuse: Fuse<Entry> | null = null
  let lastPattern: string | null = null
  let lastWords: string[] = []
  let matches = new Set<T>()
  const members = new Set(items)

  // Items without searchable text never match, so they are left out here.
  function listEntries(): Entry[] {
    entries ??= items.flatMap((item) => {
      const text = textOf(item)
      return text == null ? [] : [{ item, text: normalizeSearchText(text) }]
    })
    return entries
  }

  function fuzzySearch(words: readonly string[]): T[] {
    if (!words.some((word) => word.length >= MIN_FUZZY_WORD_LENGTH)) {
      return []
    }

    fuse ??= new Fuse(listEntries(), FUSE_OPTIONS)
    // Extended search ANDs space-separated terms; a leading ' makes a term an exact include-match.
    const query = words
      .map((word) => (word.length >= MIN_FUZZY_WORD_LENGTH ? word : `'${word}`))
      .join(' ')

    return fuse.search(query).map((result) => result.item.item)
  }

  return (pattern, item) => {
    if (pattern !== lastPattern) {
      lastPattern = pattern
      lastWords = searchWords(pattern)
      const exact = listEntries()
        .filter((entry) => containsAllWords(entry.text, lastWords))
        .map((entry) => entry.item)
      matches = new Set(exact.length > 0 ? exact : fuzzySearch(lastWords))
    }

    if (matches.has(item)) {
      return true
    }
    if (members.has(item)) {
      return false
    }

    // An item outside the list (a select's created tag) still matches exactly.
    return lastWords.length === 0 ? textOf(item) != null : matchesSearchText(textOf(item), pattern)
  }
}
