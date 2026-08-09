# syntax=docker/dockerfile:1
#
# Build context MUST be the repo root (not frontend/) — orval.config.ts reads
# ../shared/openapi/openapi.json (checked-in contract, no backend/network needed at build time).
FROM oven/bun:1 AS build
WORKDIR /build

COPY shared/openapi/ shared/openapi/
# orval's postinstall reads src/api/mutator.ts (a hand-written file, not generated — see
# api-contract.md), so the full frontend/ tree must exist before `bun install` runs; this trades
# away the usual "copy lockfile first" caching layer for correctness.
COPY frontend/ frontend/
WORKDIR /build/frontend
RUN bun install --frozen-lockfile

# Left empty by default: axios then calls same-origin relative paths (/api/...), which nginx
# proxies to the api service below — no CORS, no need to know the public domain at build time.
ARG VITE_API_BASE_URL=
ENV VITE_API_BASE_URL=${VITE_API_BASE_URL}
# `bun run build` (type-check + build-only in parallel) fails vue-tsc's .vue module resolution in
# this container image specifically (bun/vue-tsc interop, not a real type error — `vite build`
# alone succeeds and produces a valid dist/); type-checking already runs on the dev machine/CI via
# `bun run --cwd frontend build`, so the image build only needs the bundling step.
RUN bun run build-only

FROM nginx:alpine AS final
COPY --from=build /build/frontend/dist /usr/share/nginx/html
COPY frontend/nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
