# syntax=docker/dockerfile:1
# Multi-stage build: the SDK never ships in the runtime image.
#
# Build context is the REPO ROOT, not backend/ — Directory.Build.props and
# Directory.Packages.props live at the root and are needed by every project:
#   docker build -f backend/Dockerfile -t fleetops-api .

# ---------- Stage 1: restore + publish ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src

# Copy only the manifests first so `dotnet restore` stays cached until a .csproj
# or a centrally-pinned package version actually changes.
COPY Directory.Build.props Directory.Packages.props ./
COPY backend/src/FleetOps.Domain/FleetOps.Domain.csproj                 backend/src/FleetOps.Domain/
COPY backend/src/FleetOps.Application/FleetOps.Application.csproj       backend/src/FleetOps.Application/
COPY backend/src/FleetOps.Infrastructure/FleetOps.Infrastructure.csproj backend/src/FleetOps.Infrastructure/
COPY backend/src/FleetOps.Api/FleetOps.Api.csproj                       backend/src/FleetOps.Api/

# Restore the API project only. Restoring the solution would drag in the test and
# Lambda projects, neither of which belongs in this image.
RUN dotnet restore backend/src/FleetOps.Api/FleetOps.Api.csproj

COPY backend/src backend/src

RUN dotnet publish backend/src/FleetOps.Api/FleetOps.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

# ---------- Stage 2: runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS final

# Run as a non-root user. Required by the `runAsNonRoot: true` pod security context.
RUN addgroup -S fleetops && adduser -S fleetops -G fleetops

WORKDIR /app
COPY --from=build --chown=fleetops:fleetops /app/publish .

USER fleetops

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=true

# Kubernetes uses /health/live and /health/ready; this is for plain `docker run`.
HEALTHCHECK --interval=30s --timeout=3s --start-period=20s --retries=3 \
    CMD wget --quiet --tries=1 --spider http://localhost:8080/health/live || exit 1

ENTRYPOINT ["dotnet", "FleetOps.Api.dll"]
