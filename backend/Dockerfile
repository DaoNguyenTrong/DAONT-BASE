# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files first so `dotnet restore` is cached across builds that only change source.
COPY Directory.Build.props .
COPY src/StarterKit.Domain/StarterKit.Domain.csproj src/StarterKit.Domain/
COPY src/StarterKit.Application/StarterKit.Application.csproj src/StarterKit.Application/
COPY src/StarterKit.Infrastructure/StarterKit.Infrastructure.csproj src/StarterKit.Infrastructure/
COPY src/StarterKit.API/StarterKit.API.csproj src/StarterKit.API/
RUN dotnet restore src/StarterKit.API/StarterKit.API.csproj

COPY src/ src/
RUN dotnet publish src/StarterKit.API/StarterKit.API.csproj -c Release -o /app/publish --no-restore

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
