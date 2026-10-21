import boundaries from 'eslint-plugin-boundaries'
import vueParser from 'vue-eslint-parser'
import tseslint from 'typescript-eslint'

// Layer boundaries only, no style rules; dependencies point down the stack. lib and api stay out of
// state and UI, stores stay out of UI, components never reach routed views or layouts, and views
// are only routed to.
const UI = ['components', 'composables', 'layouts', 'views', 'router']

export default [
  {
    ignores: ['dist/**', 'coverage/**', 'playwright-report/**', 'test-results/**', 'src/api/generated/**', 'src/typings/**'],
  },
  {
    files: ['src/**/*.{ts,vue}'],
    languageOptions: {
      parser: vueParser,
      parserOptions: { parser: tseslint.parser, ecmaVersion: 'latest', sourceType: 'module' },
    },
    plugins: { boundaries },
    settings: {
      // Resolves the '@/' alias, so an import is classified by the layer it actually lands in.
      'import/resolver': { typescript: { project: ['tsconfig.app.json'] } },
      'boundaries/elements': [
        { type: 'lib', pattern: 'src/lib/**' },
        { type: 'api', pattern: 'src/api/**' },
        { type: 'stores', pattern: 'src/stores/**' },
        { type: 'composables', pattern: 'src/composables/**' },
        { type: 'components', pattern: 'src/components/**' },
        { type: 'layouts', pattern: 'src/layouts/**' },
        { type: 'views', pattern: 'src/views/**' },
        { type: 'router', pattern: 'src/router/**' },
      ],
    },
    rules: {
      'boundaries/dependencies': [
        'error',
        {
          default: 'allow',
          policies: [
            {
              from: { element: { type: 'lib' } },
              disallow: { to: { element: { type: ['stores', ...UI] } } },
            },
            {
              from: { element: { type: 'api' } },
              disallow: { to: { element: { type: ['stores', ...UI] } } },
            },
            {
              from: { element: { type: 'stores' } },
              disallow: { to: { element: { type: ['components', 'layouts', 'views', 'router'] } } },
            },
            {
              from: { element: { type: 'components' } },
              disallow: { to: { element: { type: ['layouts', 'views', 'router'] } } },
            },
            {
              from: { element: { type: ['lib', 'api', 'stores', 'composables', 'components', 'layouts'] } },
              disallow: { to: { element: { type: 'views' } } },
            },
          ],
        },
      ],
    },
  },
]
