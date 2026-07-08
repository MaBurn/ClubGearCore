#!/usr/bin/env bash
set -euo pipefail

TAG="${1:-test}"
ENGINE="${2:-auto}"
OUTPUT="clubgear-${TAG}.tar.gz"
GIT_SHA="$(git rev-parse --short HEAD 2>/dev/null || echo 'unknown')"
BUILD_DATE="$(date -u +%Y-%m-%dT%H:%M:%SZ)"

if [ "${ENGINE}" = "auto" ]; then
  if command -v docker >/dev/null 2>&1; then
    ENGINE="docker"
  elif command -v podman >/dev/null 2>&1; then
    ENGINE="podman"
  else
    echo "Neither docker nor podman was found." >&2
    exit 1
  fi
fi

case "${ENGINE}" in
  docker|podman) ;;
  *)
    echo "Unknown engine: ${ENGINE}" >&2
    echo "Usage: $0 [tag] [auto|docker|podman]" >&2
    exit 1
    ;;
esac

IMAGE="clubgear:${TAG}"

echo "→ Baue Image ${IMAGE} mit ${ENGINE} (commit ${GIT_SHA}) ..."
"${ENGINE}" build \
  --label "org.opencontainers.image.revision=${GIT_SHA}" \
  --label "org.opencontainers.image.created=${BUILD_DATE}" \
  --label "org.opencontainers.image.version=${TAG}" \
  -t "${IMAGE}" .

echo "→ Exportiere nach ${OUTPUT} ..."
"${ENGINE}" save "${IMAGE}" | gzip > "${OUTPUT}"

if command -v shasum >/dev/null 2>&1; then
  shasum -a 256 "${OUTPUT}" > "${OUTPUT}.sha256"
elif command -v sha256sum >/dev/null 2>&1; then
  sha256sum "${OUTPUT}" > "${OUTPUT}.sha256"
fi

echo "✓ Fertig: ${OUTPUT}"
if [ -f "${OUTPUT}.sha256" ]; then
  echo "✓ Checksumme: ${OUTPUT}.sha256"
fi
echo ""
echo "  Auf dem Server laden:"
echo "  docker load -i ${OUTPUT}"
echo "  podman load -i ${OUTPUT}"
echo ""
echo "  Oder direkt per SSH:"
echo "  cat ${OUTPUT} | ssh user@server 'docker load'"
echo ""
echo "  Danach starten:"
echo "  docker run --rm -p 5080:8080 -e ASPNETCORE_ENVIRONMENT=Production ${IMAGE}"
echo "  podman run --rm -p 5080:8080 -e ASPNETCORE_ENVIRONMENT=Production ${IMAGE}"
