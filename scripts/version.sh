#!/usr/bin/env bash
# Single source of truth for the ClubGear core version.
#
# The version is declared exactly once, in ClubGear.csproj. Scripts and docs must never
# hard-code it — read it from here instead:
#
#   source "$(dirname "$0")/version.sh"
#   TAG="${1:-$(clubgear_version)}"

clubgear_version() {
  local root version
  root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

  version="$(sed -n 's:[[:space:]]*<Version>\([^<]*\)</Version>.*:\1:p' "${root}/ClubGear.csproj" | head -1)"

  if [ -z "${version}" ]; then
    echo "✗ Konnte <Version> nicht aus ${root}/ClubGear.csproj lesen." >&2
    return 1
  fi

  printf '%s\n' "${version}"
}
