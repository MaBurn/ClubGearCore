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

echo "→ Starte ClubGear (Docker, Profil: ${PROFILE}) ..."
docker compose --profile "${PROFILE}" up --build
