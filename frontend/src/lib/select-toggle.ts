/**
 * Picking the already-selected option of a single select clears it (`null`), so a filter can be
 * switched off the way it was switched on. A multiple select already toggles inside Naive UI.
 */
export function resolveSelectUpdate<T extends string | number>(
  current: T | T[] | null | undefined,
  next: T | T[] | null,
  multiple = false,
): T | T[] | null {
  if (!multiple && next !== null && !Array.isArray(next) && next === current) {
    return null
  }

  return next
}
