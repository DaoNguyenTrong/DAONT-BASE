// The one file a fork edits to rebrand the guide (see frontend/user-guide/README.md).
export type ProductBrand = { wordmarkLight: string; wordmarkDark: string; mark: string }

export const product: {
  name: string
  publisher: string
  /** File names in frontend/public/ — scripts/sync-brand.ts copies them into the site. */
  brand: ProductBrand
  /** Base chapters this product doesn't ship; only chapters marked `hideable` may be listed. */
  hiddenBaseChapters: string[]
} = {
  name: 'StarterKit',
  publisher: 'WeatherPlus',
  brand: {
    wordmarkLight: 'weatherplus-wordmark-light.webp',
    wordmarkDark: 'weatherplus-wordmark-dark.webp',
    mark: 'weatherplus-mark.webp',
  },
  hiddenBaseChapters: [],
}
