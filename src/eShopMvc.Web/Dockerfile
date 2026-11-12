# Linux image for eShopMvc.Web (step 10; React client since phase 2 step 8).
# Build context: src/ (see docker-compose.yml), because the image needs both projects (Decision P2-10).
# Images are pinned to exact tags; the runtime patch (10.0.13) matches the EF Core packages, the Node
# version matches eShopMvc.Client/.nvmrc (Decision P2-5).

# Decision P2-10: the React client is built here, not by dotnet build. Only its output leaves this stage.
FROM node:26.13.0-bookworm-slim AS client
WORKDIR /src/eShopMvc.Client

# Install first so the package layer is cached until the lock file changes.
COPY eShopMvc.Client/package.json eShopMvc.Client/package-lock.json eShopMvc.Client/.npmrc ./
RUN npm ci

# Type check + Vite build; vite.config.ts writes to ../eShopMvc.Web/wwwroot/app.
COPY eShopMvc.Client/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0.402-noble AS build
WORKDIR /src/eShopMvc.Web

# Restore first so the package layer is cached until the project file changes.
COPY eShopMvc.Web/eShopMvc.Web.csproj ./
RUN dotnet restore eShopMvc.Web.csproj

COPY eShopMvc.Web/ ./
# Before publish, so the static assets manifest (MapStaticAssets) includes the client's files.
COPY --from=client /src/eShopMvc.Web/wwwroot/app ./wwwroot/app
RUN dotnet publish eShopMvc.Web.csproj --configuration Release --no-restore --output /app

# Decision D-A: full Ubuntu 24.04 image. It has ICU (en-US formatting, R-12; globalization is not
# invariant) and bash for the health check (D-B). No curl or wget needed.
# No Node either: the client is static files by now.
FROM mcr.microsoft.com/dotnet/aspnet:10.0.13-noble AS final
WORKDIR /app
COPY --from=build /app ./

# Data Protection key ring (antiforgery tokens). Created here owned by "app" so the
# compose volume mounted on it starts out writable for the non-root user (Decision D-H).
RUN mkdir -p /home/app/.aspnet/DataProtection-Keys \
    && chown -R $APP_UID /home/app/.aspnet

# Non-root: the image's built-in "app" user. Kestrel listens on 8080 (ASPNETCORE_HTTP_PORTS default).
USER $APP_UID
EXPOSE 8080

# Decision D-B: /health/ready includes the database; bash's /dev/tcp avoids installing curl.
HEALTHCHECK --interval=10s --timeout=5s --start-period=30s --retries=6 \
    CMD ["bash", "-c", "exec 3<>/dev/tcp/127.0.0.1/8080 && printf 'GET /health/ready HTTP/1.0\\r\\nHost: localhost\\r\\n\\r\\n' >&3 && grep -q ' 200 ' <&3"]

ENTRYPOINT ["dotnet", "eShopMvc.Web.dll"]
