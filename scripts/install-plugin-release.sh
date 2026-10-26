#!/usr/bin/env bash
set -euo pipefail

if [[ $# -lt 1 ]]; then
  echo "Usage: $0 <plugin-dir> [version]" >&2
  echo "  <plugin-dir>  e.g. plugins/Finance" >&2
  echo "  [version]     e.g. 2.2.0 — defaults to the version in <plugin-dir>/plugin.json" >&2
  exit 1
fi

plugin_dir="$(cd "$1" && pwd)"
plugin_name="$(basename "$plugin_dir")"
dist_dir="$plugin_dir/dist"
manifest_path="$plugin_dir/plugin.json"

version="${2:-}"
if [[ -z "$version" ]]; then
  version="$(sed -n 's/.*"version": "\([^"]*\)".*/\1/p' "$manifest_path" | head -1)"
fi
if [[ -z "$version" ]]; then
  echo "Could not determine version — pass it explicitly as \$2." >&2
  exit 1
fi

zip_path="$dist_dir/$plugin_name-$version.zip"
sha_path="$zip_path.sha256"
# Package scripts in this repo name the sidecar <name>-<version>.sha256, not <name>-<version>.zip.sha256.
[[ -f "$sha_path" ]] || sha_path="$dist_dir/$plugin_name-$version.sha256"
sig_path="$dist_dir/$plugin_name-$version.signature.b64"
pem_path="$dist_dir/signer-public.pem"

for f in "$zip_path" "$sha_path" "$sig_path" "$pem_path"; do
  if [[ ! -f "$f" ]]; then
    echo "Missing required file: $f" >&2
    exit 1
  fi
done

base_url="${CLUBGEAR_URL:-http://localhost:5007}"
admin_email="${CLUBGEAR_ADMIN_EMAIL:-admin@clubgear.local}"
admin_password="${CLUBGEAR_ADMIN_PASSWORD:-Geheim1234}"

cookie_jar="$(mktemp)"
trap 'rm -f "$cookie_jar"' EXIT

login_page="$(curl -sS -c "$cookie_jar" "$base_url/Account/Login")"
token="$(printf '%s' "$login_page" | grep -o 'name="__RequestVerificationToken"[^>]*value="[^"]*"' | head -1 | sed -E 's/.*value="([^"]*)".*/\1/')"
if [[ -z "$token" ]]; then
  echo "Could not read antiforgery token — is $base_url reachable and serving the login page?" >&2
  exit 1
fi

login_status="$(curl -sS -o /dev/null -w '%{http_code}' -b "$cookie_jar" -c "$cookie_jar" "$base_url/Account/Login" \
  --data-urlencode "email=$admin_email" \
  --data-urlencode "password=$admin_password" \
  --data-urlencode "__RequestVerificationToken=$token")"
if [[ "$login_status" != "302" ]]; then
  echo "Login failed (HTTP $login_status) for $admin_email against $base_url." >&2
  exit 1
fi

python3 - "$zip_path" "$sha_path" "$sig_path" "$pem_path" "$plugin_name-$version.zip" > /tmp/install-plugin-release-payload.json <<'PYEOF'
import base64, json, sys
zip_path, sha_path, sig_path, pem_path, file_name = sys.argv[1:6]
payload = {
    "fileName": file_name,
    "zipPackageBase64": base64.b64encode(open(zip_path, "rb").read()).decode(),
    "expectedSha256Hex": open(sha_path).read().strip(),
    "signatureBase64": open(sig_path).read().strip(),
    "signerPublicKeyPem": open(pem_path).read(),
}
json.dump(payload, sys.stdout)
PYEOF

install_response="$(curl -sS -b "$cookie_jar" -X POST "$base_url/api/plugins/install/zip" \
  -H "Content-Type: application/json" \
  --data @/tmp/install-plugin-release-payload.json)"
rm -f /tmp/install-plugin-release-payload.json

# A brand-new install does not activate itself (unlike upgrading an already-active plugin) —
# without this, every first-ever install of a plugin silently sits inactive until someone
# notices and calls /activate by hand. Confirmed 2026-10-08 against Inventar's first release.
module_id="$(printf '%s' "$install_response" | python3 -c 'import json,sys; d=json.load(sys.stdin); print(d.get("plugin",{}).get("moduleId","") if d.get("success") else "")')"
is_active="$(printf '%s' "$install_response" | python3 -c 'import json,sys; d=json.load(sys.stdin); print(d.get("plugin",{}).get("isActive", True))')"

if [[ -n "$module_id" && "$is_active" == "False" ]]; then
  curl -sS -b "$cookie_jar" -X POST "$base_url/api/plugins/$module_id/activate"
  echo
else
  printf '%s\n' "$install_response"
fi
