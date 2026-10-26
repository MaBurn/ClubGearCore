#!/usr/bin/env bash
set -euo pipefail

source "$(dirname "${BASH_SOURCE[0]}")/version.sh"

# Aufruf: deploy.sh <tag> <user@host>  oder  deploy.sh <user@host>  (Tag = Version aus csproj)
if [[ $# -eq 1 && "$1" == *@* ]]; then
    TAG="$(clubgear_version)"
    SSH_HOST="$1"
else
    TAG="${1:-$(clubgear_version)}"
    SSH_HOST="${2:-}"
fi

ARCHIVE="clubgear-${TAG}.tar.gz"

if [[ -z "$SSH_HOST" ]]; then
    echo "Verwendung: ./scripts/deploy.sh [tag] <user@host>"
    echo "Beispiel:   ./scripts/deploy.sh max@192.168.1.10          (Tag: $(clubgear_version))"
    echo "            ./scripts/deploy.sh 2026.7.1 max@192.168.1.10"
    exit 1
fi

if [[ ! -f "$ARCHIVE" ]]; then
    echo "✗ ${ARCHIVE} nicht gefunden. Zuerst ./scripts/build-and-export.sh ${TAG} ausführen."
    exit 1
fi

echo "→ Übertrage ${ARCHIVE} nach ${SSH_HOST} ..."
scp "$ARCHIVE" "${SSH_HOST}:/tmp/${ARCHIVE}"

echo "→ Lade Image auf Docker-Host ..."
ssh "$SSH_HOST" "docker load -i /tmp/${ARCHIVE} && rm /tmp/${ARCHIVE}"

echo "→ Starte Container neu ..."
ssh "$SSH_HOST" "docker restart \$(docker ps -q --filter ancestor=clubgear:${TAG}) 2>/dev/null || echo '  Kein laufender Container gefunden – manuell in Portainer starten.'"

echo "✓ Fertig. Image clubgear:${TAG} ist aktiv."
