// The wordmark's text color is baked into the image (not styleable via CSS),
// so light/dark needs two separate source files rather than one recolorable asset.
export function useBrandWordmark() {
  const { isDark } = useThemePreference()

  const wordmarkSrc = computed(() =>
    isDark.value ? '/weatherplus-wordmark-dark.webp' : '/weatherplus-wordmark-light.webp',
  )

  return { wordmarkSrc }
}
