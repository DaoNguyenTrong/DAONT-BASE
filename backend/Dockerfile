# syntax=docker/dockerfile:1
#
# Build context MUST be the repo root (not backend/) — MinVer (backend/Directory.Build.props)
# derives the assembly version from git tags and needs the actual .git history, copied below,
# which is unreachable from a context scoped to backend/ alone.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files first so `dotnet restore` is cached across builds that only change source.
COPY backend/Directory.Build.props backend/Directory.Build.props
COPY backend/src/StarterKit.Domain/StarterKit.Domain.csproj backend/src/StarterKit.Domain/
COPY backend/src/StarterKit.Application/StarterKit.Application.csproj backend/src/StarterKit.Application/
COPY backend/src/StarterKit.Infrastructure/StarterKit.Infrastructure.csproj backend/src/StarterKit.Infrastructure/
COPY backend/src/StarterKit.API/StarterKit.API.csproj backend/src/StarterKit.API/
RUN dotnet restore backend/src/StarterKit.API/StarterKit.API.csproj

COPY backend/src/ backend/src/
# Copied last (changes on every commit) so it doesn't invalidate the restore-cache layers above.
# Only .git's metadata is needed (MinVer runs `git describe` against it) — none of it reaches the
# final image below, which only copies the published output.
COPY .git/ .git/
RUN dotnet publish backend/src/StarterKit.API/StarterKit.API.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Npgsql tries to load libgssapi for GSS encryption negotiation on every connection; the base
# image doesn't ship it, so every DB query logs a spurious ERR-level "Cannot load library
# libgssapi_krb5.so.2" (harmless — Npgsql falls back and the connection still works, but it's
# noisy enough to trip log-based alerting). Verified empirically against this exact image.
RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

# uploads/ and logs/ are bind targets for named volumes in docker-compose.prod.yml. An empty
# named volume gets its ownership seeded from whatever already exists at the mount path in the
# image on first mount — without this, Docker creates the mount point as root:root and the
# non-root user below can't write to it (verified empirically: file uploads and file-sink logging
# both silently fail with Permission denied otherwise).
RUN mkdir -p uploads logs && chown -R $APP_UID:$APP_UID uploads logs

EXPOSE 8080
# Base image defaults to root; opt into the non-root "app" user it ships (uid $APP_UID).
USER $APP_UID
ENTRYPOINT ["dotnet", "StarterKit.API.dll"]
