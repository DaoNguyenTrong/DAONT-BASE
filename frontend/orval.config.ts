import { defineConfig } from 'orval'

export default defineConfig({
  starterKit: {
    input: '../shared/openapi/openapi.json',
    output: {
      mode: 'tags-split',
      target: 'src/api/generated',
      schemas: 'src/api/generated/model',
      client: 'axios',
      clean: true,
      override: {
        mutator: {
          path: 'src/api/mutator.ts',
          name: 'apiRequest',
        },
      },
    },
  },
})
