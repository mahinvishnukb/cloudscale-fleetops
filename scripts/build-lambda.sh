#!/usr/bin/env bash
# Builds the manifest-processor Lambda as a self-contained custom-runtime zip.
#
# .NET 8 on Lambda uses the `provided.al2023` custom runtime, which expects the
# executable to be named `bootstrap` at the root of the archive.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROJECT="$ROOT/lambda/src/FleetOps.ManifestProcessor/FleetOps.ManifestProcessor.csproj"
OUT="$ROOT/lambda/dist"
STAGE="$OUT/publish"

# Graviton (arm64) is cheaper and faster; set LAMBDA_RUNTIME_ID=linux-x64 to switch.
RUNTIME="${LAMBDA_RUNTIME_ID:-linux-arm64}"

echo "==> Publishing $RUNTIME (self-contained)"
rm -rf "$STAGE"
dotnet publish "$PROJECT" \
  --configuration Release \
  --runtime "$RUNTIME" \
  --self-contained true \
  -p:PublishSingleFile=false \
  --output "$STAGE"

echo "==> Renaming host executable to 'bootstrap'"
mv "$STAGE/FleetOps.ManifestProcessor" "$STAGE/bootstrap"
chmod +x "$STAGE/bootstrap"

echo "==> Zipping"
mkdir -p "$OUT"
rm -f "$OUT/manifest-processor.zip"
(cd "$STAGE" && zip -qr "$OUT/manifest-processor.zip" .)

echo "==> Built $OUT/manifest-processor.zip ($(du -h "$OUT/manifest-processor.zip" | cut -f1))"
