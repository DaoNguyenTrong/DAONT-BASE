/** First letter of the first and last words, uppercased (diacritics kept); `'?'` when there is no name. */
export function initialsOf(name: string | null | undefined): string {
  const words = (name ?? '').trim().split(/\s+/).filter(Boolean)
  if (words.length === 0) {
    return '?'
  }
  const letters = [words[0], words.length > 1 ? words[words.length - 1] : ''].map(
    (word) => Array.from(word ?? '')[0] ?? '',
  )
  return letters.join('').toLocaleUpperCase()
}
