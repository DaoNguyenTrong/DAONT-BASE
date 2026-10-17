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
      coverage: {
        provider: 'v8',
        // json-summary feeds the coverage table CI writes to the run summary (.github/workflows/test.yml)
        reporter: ['text', 'html', 'json-summary'],
        include: ['src/**/*.{ts,vue}'],
        exclude: ['src/typings/**', 'src/api/generated/**', 'src/main.ts'],
      },
    },
  }),
)
