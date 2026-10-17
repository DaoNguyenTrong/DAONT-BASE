/**
 * Whether a CSS media query matches, kept current as the viewport changes. For logic that has to branch on
 * the screen (a popover's placement); pure styling uses Tailwind variants (`max-lg:`, `pointer-coarse:`).
 * False where `matchMedia` doesn't exist (jsdom).
 */
export function useMediaQuery(query: string): Readonly<Ref<boolean>> {
  const matches = ref(false)

  if (typeof window.matchMedia !== 'function') {
    return readonly(matches)
  }

  const list = window.matchMedia(query)
  matches.value = list.matches

  const update = (event: { matches: boolean }) => {
    matches.value = event.matches
  }
  list.addEventListener('change', update)

  if (getCurrentScope()) {
    onScopeDispose(() => list.removeEventListener('change', update))
  }

  return readonly(matches)
}
