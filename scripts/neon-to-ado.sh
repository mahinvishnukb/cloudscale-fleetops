#!/usr/bin/env bash
# Convert a Neon (or any Postgres) URI into the ADO.NET form Npgsql expects.
#
#   postgresql://user:pass@host/db?sslmode=require
#     ->  Host=host;Database=db;Username=user;Password=pass;SSL Mode=Require;...
#
# Reads the URI from $1 or stdin so the secret never has to be retyped:
#
#   npx neonctl@latest connection-string --project-id <id> --pooled | ./scripts/neon-to-ado.sh
#
set -euo pipefail

uri="${1:-}"
if [[ -z "$uri" ]]; then
  read -r uri
fi

python3 - "$uri" <<'PY'
import sys
from urllib.parse import urlparse, unquote

raw = sys.argv[1].strip()
if not raw:
    sys.exit("no connection URI supplied")

p = urlparse(raw)
if not p.hostname:
    sys.exit(f"could not parse a hostname out of: {raw[:40]}...")

# Neon's pooled endpoint carries '-pooler' in the host. The direct endpoint drops
# connections when the compute has been suspended, which on a free tier is most of
# the time, so warn rather than silently producing a flaky connection string.
if "-pooler" not in p.hostname:
    print("WARNING: this is the DIRECT endpoint, not the pooled one.", file=sys.stderr)
    print("         Re-run neonctl with --pooled for a free-tier-friendly string.", file=sys.stderr)

parts = [
    f"Host={p.hostname}",
    f"Database={p.path.lstrip('/') or 'neondb'}",
    f"Username={unquote(p.username or '')}",
    f"Password={unquote(p.password or '')}",
    "SSL Mode=Require",
    "Trust Server Certificate=true",
]
print(";".join(parts))
PY
