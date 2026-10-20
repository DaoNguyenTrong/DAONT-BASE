/**
 * How many leading items of a row stay visible. When they all fit, all of them; otherwise as many as fit
 * beside a "More" entry of `moreWidth`, which may be none.
 */
export function splitOverflow(itemWidths: number[], available: number, moreWidth: number): number {
  const total = itemWidths.reduce((sum, width) => sum + width, 0)
  if (total <= available) return itemWidths.length

  let used = moreWidth
  let count = 0
  for (const width of itemWidths) {
    if (used + width > available) break
    used += width
    count += 1
  }
  return count
}
