#!/usr/bin/env bash
set -euo pipefail

# Veröffentlicht den Core-Quellcode als Release-Snapshot im öffentlichen GitHub-Spiegel.
#
# Das GitHub-Repository ist ein Open-Core-Spiegel unter AGPL-3.0: es enthält den
# baubaren Core, aber nicht die proprietären Plugins unter plugins/. Es ist kein
# Spiegel der GitLab-Historie — pro Release entsteht genau ein Commit plus Tag.
#
# Veröffentlicht wird immer HEAD, nie der Working Tree. Uncommittete Änderungen
# landen also nicht im Release.
#
#   scripts/publish-release-github.sh              # Version aus ClubGear.csproj
#   scripts/publish-release-github.sh 2026.8.0     # Version explizit
#   scripts/publish-release-github.sh --dry-run    # alles bauen, nichts pushen

source "$(dirname "${BASH_SOURCE[0]}")/version.sh"

REMOTE="${CLUBGEAR_GITHUB_REMOTE:-git@github.com:MaBurn/ClubGearCore.git}"
BRANCH="${CLUBGEAR_GITHUB_BRANCH:-main}"

# Pfade, die im Spiegel nicht erscheinen. plugins/ ist proprietär und separat
# versioniert; die übrigen Einträge beschreiben den GitLab-Arbeitsablauf und
# haben im veröffentlichten Quellbaum keine Bedeutung.
#
# wwwroot/uploads enthält Laufzeitdaten echter Mitglieder (Profilfotos). Diese
# Dateien sind versehentlich versioniert und dürfen keinesfalls in ein
# öffentliches Repository — der Ausschluss hier ist eine Sicherung, kein Detail.
#
# Die Auswahl folgt der Publikationsrichtlinie, die als docs/publication-allowlist.md
# im GitHub-Spiegel dokumentiert war (Commit efa9135, entfernt in ec8dfdf).
EXCLUDE=(
  plugins
  .gitmodules
  .gitignore
  CHANGELOG.md
  Readme.md
  wwwroot/uploads
  .crispy
  .claude
  .agents
  .codex
  App_Data
  docker-compose.portainer-test.yml
  docs/gitlab-ci.md
)

# Letzte Reißleine vor dem Push. Die Ausschlussliste oben ist eine Absicht, das
# hier ist die Kontrolle: taucht eines dieser Muster im fertigen Snapshot auf,
# bricht der Lauf ab, statt es zu veröffentlichen.
FORBIDDEN='(^|/)\.DS_Store$|\.(db|db-shm|db-wal|sqlite|sqlite3)$|^\.(crispy|claude|agents|codex)/|^App_Data/|^wwwroot/uploads/|\.(tar|tar\.gz|zip|pem|key|pfx|p12)$|(^|/)\.env$'

# Dateien, die ausschließlich im GitHub-Repo leben und beim Neuaufbau des
# Snapshots erhalten bleiben müssen.
KEEP=(LICENSE README.md)

DRY_RUN=0
VERSION=""

for arg in "$@"; do
  case "${arg}" in
    --dry-run) DRY_RUN=1 ;;
    -h|--help)
      sed -n '3,16p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'
      exit 0
      ;;
    -*)
      echo "✗ Unbekannte Option: ${arg}" >&2
      echo "  Usage: $0 [version] [--dry-run]" >&2
      exit 1
      ;;
    *) VERSION="${arg}" ;;
  esac
done

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "${ROOT}"

VERSION="${VERSION:-$(clubgear_version)}"
TAG="v${VERSION}"
SOURCE_SHA="$(git rev-parse HEAD)"

echo "→ Release ${VERSION} aus ${SOURCE_SHA:0:12} nach ${REMOTE}"

if ! git diff --quiet HEAD 2>/dev/null; then
  echo "  ⚠ Working Tree hat uncommittete Änderungen — veröffentlicht wird HEAD, nicht der Working Tree."
fi

WORK="$(mktemp -d)"
trap 'rm -rf "${WORK}"' EXIT

MIRROR="${WORK}/mirror"
echo "→ Klone Spiegel ..."
git clone --quiet --branch "${BRANCH}" "${REMOTE}" "${MIRROR}"

# Die Identität aus dem Quellrepo übernehmen. Ohne globale Git-Config hätte der
# frische Klon keine und der Commit unten würde abbrechen.
git -C "${MIRROR}" config user.name  "$(git config user.name)"
git -C "${MIRROR}" config user.email "$(git config user.email)"

if git -C "${MIRROR}" rev-parse -q --verify "refs/tags/${TAG}" >/dev/null; then
  echo "✗ Tag ${TAG} existiert im Spiegel bereits. Version in ClubGear.csproj erhöhen." >&2
  exit 1
fi

# Bisherigen Baum leeren, aber die GitHub-eigenen Dateien behalten.
for entry in "${MIRROR}"/* "${MIRROR}"/.[!.]*; do
  [ -e "${entry}" ] || continue
  name="$(basename "${entry}")"
  [ "${name}" = ".git" ] && continue
  keep=0
  for k in "${KEEP[@]}"; do
    [ "${name}" = "${k}" ] && keep=1
  done
  [ "${keep}" = "1" ] && continue
  rm -rf "${entry}"
done

for k in "${KEEP[@]}"; do
  if [ ! -e "${MIRROR}/${k}" ]; then
    echo "✗ ${k} fehlt im Spiegel — es lebt nur dort und kann nicht neu erzeugt werden." >&2
    exit 1
  fi
done

echo "→ Exportiere HEAD ..."
# Ausgeschlossene Pfade dürfen gar nicht erst auf die Platte: auf einem
# case-insensitiven Dateisystem (macOS) würde das lokale Readme.md die
# GitHub-eigene README.md überschreiben, und ein anschließendes Löschen
# von Readme.md träfe dann beide.
ARCHIVE_SPEC=(.)
for e in "${EXCLUDE[@]}"; do
  ARCHIVE_SPEC+=(":(exclude)${e}")
done
git archive "${SOURCE_SHA}" -- "${ARCHIVE_SPEC[@]}" | tar -x -C "${MIRROR}"

# .DS_Store liegt verstreut und lässt sich nicht als Pfad ausschließen.
find "${MIRROR}" -name '.DS_Store' -not -path "${MIRROR}/.git/*" -delete

cd "${MIRROR}"
git add -A

# Gegen den Index prüfen, nicht gegen das Dateisystem: macOS ist
# case-insensitiv, dort matcht ein Test auf Readme.md auch die README.md,
# die wir gerade behalten wollen. Der Index führt die Schreibweise exakt.
STAGED="$(git ls-files)"

for e in "${EXCLUDE[@]}"; do
  if printf '%s\n' "${STAGED}" | grep -qxF "${e}" \
     || printf '%s\n' "${STAGED}" | grep -qF "${e}/"; then
    echo "✗ ${e} ist trotz Ausschluss im Snapshot gelandet." >&2
    exit 1
  fi
done

# Nach dem Aufbau erneut prüfen — der Check vor dem Export übersieht Dateien,
# die der Export selbst zerstört hat.
for k in "${KEEP[@]}"; do
  if ! printf '%s\n' "${STAGED}" | grep -qxF "${k}"; then
    echo "✗ ${k} wurde beim Aufbau des Snapshots zerstört." >&2
    exit 1
  fi
done

if VIOLATIONS="$(printf '%s\n' "${STAGED}" | grep -E "${FORBIDDEN}")"; then
  echo "✗ Der Snapshot enthält Dateien, die nicht veröffentlicht werden dürfen:" >&2
  printf '    %s\n' ${VIOLATIONS} >&2
  echo "  EXCLUDE in $(basename "${BASH_SOURCE[0]}") ergänzen und erneut laufen lassen." >&2
  exit 1
fi

if git diff --cached --quiet; then
  echo "✓ Keine Änderungen gegenüber dem letzten Snapshot — nichts zu veröffentlichen."
  exit 0
fi

echo "→ Änderungen gegenüber dem letzten Snapshot:"
git diff --cached --stat | tail -20

git commit --quiet -m "release ${VERSION}: publish deployable core source" \
                   -m "Snapshot of the ClubGear core at ${SOURCE_SHA}. Plugins are proprietary and released separately."
git tag -a "${TAG}" -m "ClubGear ${VERSION}"

if [ "${DRY_RUN}" = "1" ]; then
  echo ""
  echo "✓ Trockenlauf fertig. Nichts gepusht."
  echo "  Commit läge bei: $(git rev-parse --short HEAD) mit Tag ${TAG}"
  echo "  Snapshot zum Nachsehen: ${MIRROR}"
  trap - EXIT
  exit 0
fi

echo "→ Pushe ${BRANCH} und ${TAG} ..."
git push --quiet origin "${BRANCH}" "${TAG}"

echo "✓ Release ${VERSION} veröffentlicht."
echo "  https://github.com/MaBurn/ClubGearCore/releases/tag/${TAG}"
