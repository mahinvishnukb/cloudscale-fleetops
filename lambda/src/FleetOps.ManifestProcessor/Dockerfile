# syntax=docker/dockerfile:1
# Optional container packaging for the manifest processor.
# The default deployment path is the zip built by scripts/build-lambda.sh; this image
# exists for image-based Lambda deploys and for running the handler under LocalStack.
#
# Build from the REPO ROOT:
#   docker build -f lambda/src/FleetOps.ManifestProcessor/Dockerfile -t fleetops-manifest-processor .

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ./
COPY backend/src/FleetOps.Domain/FleetOps.Domain.csproj                 backend/src/FleetOps.Domain/
COPY backend/src/FleetOps.Application/FleetOps.Application.csproj       backend/src/FleetOps.Application/
COPY backend/src/FleetOps.Infrastructure/FleetOps.Infrastructure.csproj backend/src/FleetOps.Infrastructure/
COPY lambda/src/FleetOps.ManifestProcessor/FleetOps.ManifestProcessor.csproj lambda/src/FleetOps.ManifestProcessor/

RUN dotnet restore lambda/src/FleetOps.ManifestProcessor/FleetOps.ManifestProcessor.csproj

COPY backend/src backend/src
COPY lambda/src lambda/src

# Self-contained: the `provided.al2023` runtime ships no .NET.
RUN dotnet publish lambda/src/FleetOps.ManifestProcessor/FleetOps.ManifestProcessor.csproj \
      -c Release --self-contained true -o /app/publish --no-restore \
 && mv /app/publish/FleetOps.ManifestProcessor /app/publish/bootstrap \
 && chmod +x /app/publish/bootstrap

FROM public.ecr.aws/lambda/provided:al2023
COPY --from=build /app/publish ${LAMBDA_TASK_ROOT}
# The custom runtime invokes ${LAMBDA_TASK_ROOT}/bootstrap; the handler string is unused.
CMD ["bootstrap"]
