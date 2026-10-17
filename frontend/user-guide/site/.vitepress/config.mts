import { defineConfig } from 'vitepress'
import { product } from './product'
import { resolveVersion } from './version'

// Served by nginx at /guide/ on the app's origin; changing `base` means changing the nginx
// location and the Dockerfile COPY too.
export default defineConfig({
  title: product.name,
  base: '/guide/',
  srcDir: 'src',
  outDir: 'dist',
  cleanUrls: true,
  lastUpdated: false,
  locales: {
    root: { label: 'Tiếng Việt', lang: 'vi-VN' },
    en: { label: 'English', lang: 'en-US', link: '/en/' },
  },
  vite: {
    define: {
      __GUIDE__: JSON.stringify({ ...product, version: resolveVersion() }),
    },
  },
})
