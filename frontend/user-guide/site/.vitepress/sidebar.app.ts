import type { Chapter } from './sidebar-types'

// [fork] This product's own chapters, listed after the base's. Pages go in src/app/ (Vietnamese)
// and src/app/en/ (English); `bun run new-page <chapter>/<page> --app` creates both.
export const appChapters: Chapter[] = []
