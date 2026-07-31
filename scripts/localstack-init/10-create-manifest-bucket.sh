#!/bin/bash
# Runs inside the LocalStack container once it reports healthy.
# `awslocal` is preinstalled there and already points at the local endpoint.
set -euo pipefail

BUCKET="${MANIFEST_BUCKET:-fleetops-manifests-upload-dev}"
REGION="${AWS_DEFAULT_REGION:-ca-central-1}"

echo "[fleetops] creating bucket ${BUCKET} in ${REGION}"

# Idempotent: re-running compose must not fail on an existing bucket.
awslocal s3api create-bucket \
  --bucket "${BUCKET}" \
  --create-bucket-configuration "LocationConstraint=${REGION}" >/dev/null 2>&1 || true

# Placeholder keys so the prefixes are visible in any S3 browser. S3 has no real
# directories, so an empty prefix is otherwise invisible.
for prefix in incoming processed rejected; do
  printf '' | awslocal s3 cp - "s3://${BUCKET}/${prefix}/.keep" >/dev/null
done

echo "[fleetops] bucket ${BUCKET} ready with incoming/ processed/ rejected/"
