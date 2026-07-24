import { fileURLToPath, URL } from 'node:url'

import { defineConfig, loadEnv } from 'vite'
import pkg from './package.json' with { type: 'json' }
import vue from '@vitejs/plugin-vue'
import vueJsx from '@vitejs/plugin-vue-jsx'
import vueDevTools from 'vite-plugin-vue-devtools'
import tailwindcss from '@tailwindcss/vite'
import Components from 'unplugin-vue-components/vite'
import AutoImport from 'unplugin-auto-import/vite'
import { NaiveUiResolver } from 'unplugin-vue-components/resolvers'
import { createSvgIconsPlugin } from 'vite-plugin-svg-icons'
import path from 'node:path'

// https://vite.dev/config/
export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '')
  return {
    base: env.BASE_URL || '/',
    plugins: [
      vue(),
      vueJsx(),
      ...(mode !== 'test' ? [vueDevTools()] : []),
      tailwindcss(),
      AutoImport({
        imports: ['vue', 'vue-router', 'pinia', { 'vue-i18n': ['useI18n'] }],
        dirs: [
          'src/composables/**',
          'src/stores/**',
          'src/utils/**',
          'src/api/**',
          '!src/api/generated/**',
          'src/plugins/**',
          'src/lib/**',
        ],
        dts: 'src/typings/auto-imports.d.ts',
      }),
      Components({
        types: [{ from: 'vue-router', names: ['RouterLink', 'RouterView'] }],
        resolvers: [NaiveUiResolver()],
        dts: 'src/typings/components.d.ts',
      }),
      createSvgIconsPlugin({
        iconDirs: [path.resolve(process.cwd(), 'src/assets/icons')],
        symbolId: 'icon-[dir]-[name]',
      }),
    ],
    resolve: {
      alias: {
        '@': fileURLToPath(new URL('./src', import.meta.url)),
      },
    },
    server: {
      watch: {
        ignored: [
          '**/docs/.vitepress/dist/**',
          '**/docs/.vitepress/cache/**',
          '**/documents/**',
          '**/plans/**',
          '**/.claude/**',
          '**/.git/**',
          '**/node_modules/**',
          '**/dist/**',
          '**/build/**',
          '**/public/**',
        ],
      },
    },
    define: {
      __APP_VERSION__: JSON.stringify(pkg.version),
    },
  }
})
