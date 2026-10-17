import { describe, expect, it, vi } from 'vitest'
import { createSearchTextMatcher, matchesSearchText, normalizeSearchText } from '@/lib/search-text'

describe('normalizeSearchText', () => {
  it.each([
    ['strips Vietnamese diacritics and lowercases', 'Nghệ An', 'nghe an'],
    // đ has no combining-mark decomposition, so stripping marks alone would leave it in place.
    ['maps đ/Đ to d', 'Điện Biên', 'dien bien'],
    ['handles text stored decomposed (NFD)', 'Nguyễn', 'nguyen'],
    ['turns punctuation into single spaces', '  002.KHDN / HĐ  ', '002 khdn hd'],
    ['returns empty for blank input', '   ', ''],
    ['returns empty for null', null, ''],
  ])('%s', (_name, input, expected) => {
    expect(normalizeSearchText(input)).toBe(expected)
  })
})

describe('matchesSearchText', () => {
  it.each([
    ['accent-insensitive', 'Nguyễn Văn An', 'nguyen van', true],
    ['words need not be adjacent or in order', 'Nguyễn Văn An', 'an nguyen', true],
    ['each word may match a substring', 'Quản trị viên', 'tri vi', true],
    ['every word must match', 'Nguyễn Văn An', 'nguyen binh', false],
    ['typed accents still match', 'Nguyen Van An', 'Nguyễn', true],
  ])('%s', (_name, text, pattern, expected) => {
    expect(matchesSearchText(text, pattern)).toBe(expected)
  })

  it('treats an empty pattern as matching everything, like the cleared search box', () => {
    expect(matchesSearchText('Anything', '  ')).toBe(true)
  })
})

describe('createSearchTextMatcher', () => {
  const labels = [
    'Nguyễn Văn An',
    'Trần Bình',
    'Lê Thị Hoa',
    'Hà Nội',
    'Hải Phòng',
    'Hà Nam',
    'Nam Định',
    'Quản trị viên',
    'Thành viên',
  ]

  function search(pattern: string, items: readonly string[] = labels): string[] {
    const matches = createSearchTextMatcher(items, (label) => label)
    return items.filter((label) => matches(pattern, label))
  }

  // Fuzzy matching only ever runs when nothing matches word by word, so a correct query never
  // drags in look-alikes: short Vietnamese syllables (ha, hai, hoa, nam) are all near each other.
  it.each([
    ['hai', ['Hải Phòng']],
    ['hoa', ['Lê Thị Hoa']],
    ['ha noi', ['Hà Nội']],
    ['binh', ['Trần Bình']],
  ])('keeps exact word matching when %s matches something', (pattern, expected) => {
    expect(search(pattern)).toEqual(expected)
  })

  it.each([
    ['a typo in a long word', 'nguyn', ['Nguyễn Văn An']],
    ['a typo next to an exact short word', 'nguyn van', ['Nguyễn Văn An']],
    ['a transposition', 'quna tri', ['Quản trị viên']],
  ])('falls back to fuzzy matching for %s', (_name, pattern, expected) => {
    expect(search(pattern)).toEqual(expected)
  })

  // Pins the fuzzy threshold: at 0.4 each of these typos already pulls in 6–8 unrelated names.
  it.each([
    ['tuaan', ['Phạm Minh Tuấn']],
    ['hoagn', ['Hoàng Thị Lan']],
  ])('keeps the fallback for %s to the intended name', (pattern, expected) => {
    const names = [
      'Phạm Minh Tuấn',
      'Hoàng Thị Lan',
      'Trần Bình',
      'Thành viên',
      'Thanh Hóa',
      'Kế toán',
      'Quản trị viên',
      'Hà Nội',
      'Hải Phòng',
      'Hà Nam',
      'Lê Thị Hoa',
    ]
    expect(search(pattern, names)).toEqual(expected)
  })

  // A 3-letter syllable is too short to tell a typo from a different word.
  it('never fuzzy-matches words shorter than four letters', () => {
    expect(search('hia')).toEqual([])
    expect(search('nguyn xyz')).toEqual([])
  })

  // Options are fixed for a matcher's lifetime: normalizing every one again on each keystroke
  // (once to decide exact vs fuzzy, once per Naive filter call) lags a select of a few thousand.
  it('reads and normalizes each item once, however many patterns are typed', () => {
    const textOf = vi.fn((label: string) => label)
    const matches = createSearchTextMatcher(labels, textOf)

    for (const pattern of ['n', 'ng', 'ngu', 'nguyn']) {
      labels.forEach((label) => matches(pattern, label))
    }

    expect(textOf).toHaveBeenCalledTimes(labels.length)
  })

  it('matches everything for an empty pattern', () => {
    expect(search('  ')).toEqual(labels)
  })

  it('treats items without searchable text as never matching', () => {
    const matches = createSearchTextMatcher([{ label: null }], (item) => item.label)

    expect(matches('nguyn', { label: null })).toBe(false)
  })
})
