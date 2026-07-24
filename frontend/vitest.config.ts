import { defineConfig, mergeConfig } from 'vitest/config'
import viteConfig from './vite.config'

export default mergeConfig(
  viteConfig({ mode: 'test', command: 'serve' }),
  defineConfig({
    test: {
      environment: 'happy-dom',
      setupFiles: ['./tests/setup.ts'],
      include: ['tests/**/*.{test,spec}.ts'],
      globals: true,
      env: {
        VITE_API_BASE_URL: '',
      },
    },
  }),
)
