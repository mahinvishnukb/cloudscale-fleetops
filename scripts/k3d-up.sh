#!/usr/bin/env bash
# Brings up the local Kubernetes cluster and deploys FleetOps to it. Costs nothing.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CLUSTER="fleetops"

for tool in docker k3d kubectl; do
  command -v "$tool" >/dev/null 2>&1 || { echo "error: $tool is not installed"; exit 1; }
done

if ! k3d cluster list | grep -q "^${CLUSTER}\b"; then
  echo "==> Creating k3d cluster"
  k3d cluster create --config "$ROOT/k8s/k3d-cluster.yaml"
else
  echo "==> Cluster '${CLUSTER}' already exists"
fi

echo "==> Building the API image"
docker build -f "$ROOT/backend/Dockerfile" -t fleetops-api:local "$ROOT"

echo "==> Importing the image into the cluster"
k3d image import fleetops-api:local --cluster "$CLUSTER"

echo "==> Applying manifests"
kubectl apply -k "$ROOT/k8s/overlays/local"

echo "==> Waiting for the rollout"
kubectl -n fleetops rollout status deployment/postgres --timeout=120s
kubectl -n fleetops rollout status deployment/fleetops-api --timeout=180s

cat <<'MSG'

FleetOps is running on Kubernetes.

  API      http://fleetops.localtest.me:8080
  Swagger  http://fleetops.localtest.me:8080/swagger
  Logs     kubectl -n fleetops logs -l app.kubernetes.io/name=fleetops-api -f
  Teardown k3d cluster delete fleetops

MSG
