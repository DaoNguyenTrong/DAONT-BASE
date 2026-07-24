# FeedbackHub Frontend

Vue 3 + Vite + TypeScript dashboard, imported from the standalone `FEEDBACK-HUB-FE` app-starter template (auth flow, role-based routing, responsive layout, design-token theming, i18n en/vi, Vitest + Playwright).

Will consume the backend's OpenAPI contract from `../shared/openapi/` once codegen is wired up.

## Commands

```bash
bun install
bun run dev          # start dev server
bun run type-check
bun run test:run     # vitest, no browser
bun run test:e2e      # playwright — requires `bun run test:e2e:install` once, and a running backend
```

Copy `.env.example` to `.env` and adjust `VITE_API_BASE_URL` if the backend isn't on `http://localhost:7000`.

For a deployed build, the API base URL can also be overridden at runtime — copy `public/config.example.json` to `public/config.json` (gitignored) with the real backend URL and drop it alongside the built `dist/` output. If present and valid, it takes priority over the build-time `VITE_API_BASE_URL`; if absent or invalid, the build-time value is used. This lets one build get deployed to multiple environments without rebuilding. (Testing this locally with `bun run dev`: `public/` isn't watched — see `vite.config.ts`'s `server.watch.ignored` — so restart the dev server after adding/editing `public/config.json`, it won't hot-reload.)
