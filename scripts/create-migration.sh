#!/usr/bin/env bash
# Scaffolds an EF Core migration against the Postgres provider.
#
# The API falls back to EnsureCreated when no migrations exist so a fresh clone boots,
# but anything deployed should run real migrations. Run this once, commit the result,
# and the fallback stops being used.
#
# Usage: ./scripts/create-migration.sh InitialCreate
set -euo pipefail

NAME="${1:-InitialCreate}"
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

# dotnet-ef is a LOCAL tool pinned in .config/dotnet-tools.json, not a global install.
# That keeps the tool version matched to the EF Core version the projects reference
# (a newer dotnet-ef against older EF Core produces confusing failures) and avoids
# depending on ~/.dotnet/tools being on PATH.
echo "==> Restoring local tools"
dotnet tool restore

# A reachable database is not required to scaffold, but the provider must resolve
# and the options validator needs a non-empty JWT key.
export ConnectionStrings__FleetOpsDb="${ConnectionStrings__FleetOpsDb:-Host=localhost;Database=fleetops;Username=fleetops;Password=placeholder}"
export Jwt__Key="${Jwt__Key:-scaffolding-only-key-not-used-at-runtime-0000}"

echo "==> Creating migration '$NAME'"
dotnet ef migrations add "$NAME" \
  --project "$ROOT/backend/src/FleetOps.Infrastructure" \
  --startup-project "$ROOT/backend/src/FleetOps.Api" \
  --output-dir Persistence/Migrations

echo "==> Done. Review and commit backend/src/FleetOps.Infrastructure/Persistence/Migrations"
