# ── Stage 1: Build Frontend ────────────────────────────────────────────
FROM node:22-alpine AS web-build
WORKDIR /app/web

# Install dependencies
COPY src/web/package.json src/web/package-lock.json ./
RUN npm ci

# Build production assets
COPY src/web/ ./
RUN npm run build

# ── Stage 2: Build & Publish Backend ───────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS api-build
WORKDIR /app

# Copy solution and project definitions for caching restore
COPY GradCast.slnx ./
COPY src/data/*.csproj src/data/
COPY src/api/*.csproj src/api/
RUN dotnet restore src/api/GradCast.Api.csproj

# Copy source code and built web assets
COPY src/data/ src/data/
COPY src/api/ src/api/
COPY --from=web-build /app/web/dist src/web/dist

WORKDIR /app/src/api
RUN dotnet publish -c Release -o /app/publish

# ── Stage 3: Runtime ───────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
WORKDIR /app

# Ensure data directory exists with correct permissions for non-root user
RUN mkdir -p /app/data && chown -R $APP_UID:$APP_UID /app/data

# Copy published application
COPY --from=api-build /app/publish .

# Application configuration
ENV ASPNETCORE_URLS=http://+:5062
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DatabasePath=/app/data/gradcast.db

USER $APP_UID

VOLUME ["/app/data"]
EXPOSE 5062

HEALTHCHECK --interval=30s --timeout=5s --start-period=5s --retries=3 \
  CMD wget -qO- http://localhost:5062/health || exit 1

ENTRYPOINT ["dotnet", "GradCast.Api.dll"]
