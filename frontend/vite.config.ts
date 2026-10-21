import { fileURLToPath, URL } from 'node:url'
import { execSync } from 'node:child_process'

import { defineConfig, loadEnv } from 'vite'
import vue from '@vitejs/plugin-vue'
import vueJsx from '@vitejs/plugin-vue-jsx'
import vueDevTools from 'vite-plugin-vue-devtools'
import tailwindcss from '@tailwindcss/vite'
import Components from 'unplugin-vue-components/vite'
import AutoImport from 'unplugin-auto-import/vite'
import { NaiveUiResolver } from 'unplugin-vue-components/resolvers'

function resolveAppVersion(): string {
  try {
    return execSync('git describe --tags --always', { stdio: ['ignore', 'pipe', 'ignore'] })
      .toString()
      .trim()
  } catch {
    // No .git or no commits reachable (e.g. built from a source archive) — MinVer-style fallback.
    return 'unknown'
  }
}

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
        resolvers: [
          // Every <n-select> is AppSelect: re-picking clears, filtering ignores accents.
          (name) => {
            if (name === 'NSelect') {
              return { name: 'default', from: '@/components/AppSelect.vue' }
            }
          },
          NaiveUiResolver(),
        ],
        dts: 'src/typings/components.d.ts',
      }),
    ],
    resolve: {
      alias: {
        '@': fileURLToPath(new URL('./src', import.meta.url)),
      },
    },
    server: {
      port: 5175,
      strictPort: true,
      watch: {
        ignored: [
          '**/docs/.vitepress/dist/**',
          '**/docs/.vitepress/cache/**',
          '**/documents/**',
          '**/plans/**',
          // The user guide is its own VitePress package with its own dev server.
          '**/user-guide/**',
          '**/.claude/**',
          '**/.git/**',
          '**/node_modules/**',
          '**/dist/**',
          '**/build/**',
          '**/public/**',
        ],
      },
      // The header's "?" link opens /guide/ on the app's origin, as nginx serves it in production. Run
      // `bun run --cwd frontend/user-guide/site dev` alongside to follow it; dev has no session gate.
      proxy: {
        '/guide': { target: 'http://localhost:5174', ws: true },
      },
    },
    define: {
      __APP_VERSION__: JSON.stringify(resolveAppVersion()),
    },
  }
})
