#!/usr/bin/env bash
set -euo pipefail

TAG="${1:-test}"
SSH_HOST="${2:-}"
ARCHIVE="clubgear-${TAG}.tar.gz"

if [[ -z "$SSH_HOST" ]]; then
    echo "Verwendung: ./scripts/deploy.sh <tag> <user@host>"
    echo "Beispiel:   ./scripts/deploy.sh test max@192.168.1.10"
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
