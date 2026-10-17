// Runs before `dev` and `build`: copies the product's logos from the app and generates the colour
// tokens, so the guide never keeps its own copy of either.
import { copyFileSync, existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs'
import { join, resolve } from 'node:path'
import { product, type ProductBrand } from '../.vitepress/product'
import { generateBrandTokensCss } from './brand-tokens'

export function copyLogos(brand: ProductBrand, fromDir: string, toDir: string): void {
  const files = [brand.wordmarkLight, brand.wordmarkDark, brand.mark]
  const missing = files.filter((file) => !existsSync(join(fromDir, file)))
  if (missing.length > 0) {
    throw new Error(`Missing logo files in ${fromDir}: ${missing.join(', ')}`)
  }
  mkdirSync(toDir, { recursive: true })
  for (const file of files) copyFileSync(join(fromDir, file), join(toDir, file))
}

if (import.meta.main) {
  const siteDir = resolve(import.meta.dir, '..')
  const frontendDir = resolve(siteDir, '../..')
  try {
    copyLogos(product.brand, join(frontendDir, 'public'), join(siteDir, 'src/public/brand'))
    const css = readFileSync(join(frontendDir, 'src/assets/styles/tailwind.css'), 'utf8')
    mkdirSync(join(siteDir, '.vitepress/theme'), { recursive: true })
    writeFileSync(
      join(siteDir, '.vitepress/theme/brand-tokens.generated.css'),
      generateBrandTokensCss(css),
    )
  } catch (error) {
    console.error(`sync-brand: ${(error as Error).message}`)
    process.exit(1)
  }
}
