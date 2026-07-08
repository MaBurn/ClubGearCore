#!/usr/bin/env bash
set -euo pipefail

PROFILE="${1:-local}"

case "$PROFILE" in
  local|staging|prod) ;;
  *)
    echo "Unbekanntes Profil: ${PROFILE}" >&2
    echo "Verwendung: $0 [local|staging|prod]" >&2
    exit 1
    ;;
esac

echo "→ Starte ClubGear (Podman, Profil: ${PROFILE}) ..."
podman compose --profile "${PROFILE}" up --build
