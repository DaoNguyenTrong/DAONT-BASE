import type { ProductBrand } from '../product'

declare global {
  /** product.ts plus the git version, injected by config.mts (vite.define). */
  const __GUIDE__: {
    name: string
    publisher: string
    version: string
    brand: ProductBrand
    hiddenBaseChapters: string[]
  }
}

export {}
