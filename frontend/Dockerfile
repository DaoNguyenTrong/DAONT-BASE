# syntax=docker/dockerfile:1
#
# Build context MUST be the repo root (not frontend/) — orval.config.ts reads
# ../shared/openapi/openapi.json (checked-in contract, no backend/network needed at build time).
# .git/ must be in the context too: vite.config.ts resolveAppVersion() runs
# `git describe --tags --always` for the footer version — without it the footer says "unknown".
# Same reason backend/Dockerfile copies .git for MinVer.
FROM oven/bun:1 AS build
WORKDIR /build

# The bun base image has no git CLI, which resolveAppVersion() needs (not just .git/).
RUN apt-get update \
    && apt-get install -y --no-install-recommends git \
    && rm -rf /var/lib/apt/lists/*

COPY shared/openapi/ shared/openapi/
# orval's postinstall reads src/api/mutator.ts (a hand-written file, not generated — see
# api-contract.md), so the full frontend/ tree must exist before `bun install` runs; this trades
# away the usual "copy lockfile first" caching layer for correctness.
COPY frontend/ frontend/
WORKDIR /build/frontend
RUN bun install --frozen-lockfile

# Left empty by default: axios then calls same-origin relative paths (/api/...), which nginx
# proxies to the api service below — no CORS, no need to know the public domain at build time.
# Every VITE_* value (this one included) can also be set per container at runtime instead — see
# docker/40-write-runtime-config.sh — so the image itself stays environment-neutral.
ARG VITE_API_BASE_URL=
ENV VITE_API_BASE_URL=${VITE_API_BASE_URL}
# Copied last (changes on every commit) so it doesn't invalidate the bun-install layer above.
# At /build/.git so `git describe` from WORKDIR /build/frontend walks up to the monorepo root.
COPY .git/ /build/.git/
# `bun run build` (type-check + build-only in parallel) fails vue-tsc's .vue module resolution in
# this container image specifically (bun/vue-tsc interop, not a real type error — `vite build`
# alone succeeds and produces a valid dist/); type-checking already runs on the dev machine/CI via
# `bun run --cwd frontend build`, so the image build only needs the bundling step.
RUN bun run build-only

# The user guide (frontend/user-guide/site), its own package. Built on top of `build` for its git CLI
# and .git/: the guide's footer shows the same `git describe` version as the app's.
FROM build AS guide
WORKDIR /build/frontend/user-guide/site
RUN bun install --frozen-lockfile && bun run build

FROM nginx:alpine AS final
COPY --from=build /build/frontend/dist /usr/share/nginx/html
COPY --from=guide /build/frontend/user-guide/site/dist /usr/share/nginx/html/guide
COPY frontend/nginx.conf /etc/nginx/conf.d/default.conf
# Both access modes; the private one is also installed as the active snippet, so the image is private
# even if the entrypoint script below never runs.
COPY frontend/docker/guide-private.conf frontend/docker/guide-public.conf /etc/nginx/snippets-available/
COPY frontend/docker/guide-private.conf /etc/nginx/snippets/guide.conf
COPY frontend/docker/guide-unavailable.html /usr/share/nginx/html/_guide_unavailable.html
# Writes /config.json from the container's VITE_* env at startup (nginx runs docker-entrypoint.d/*).
COPY --chmod=755 frontend/docker/40-write-runtime-config.sh /docker-entrypoint.d/40-write-runtime-config.sh
# Picks the user guide's access mode from GUIDE_PUBLIC.
COPY --chmod=755 frontend/docker/41-guide-mode.sh /docker-entrypoint.d/41-guide-mode.sh
EXPOSE 80
