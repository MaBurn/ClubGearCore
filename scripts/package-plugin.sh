#!/usr/bin/env bash
set -euo pipefail

if [[ $# -lt 1 ]]; then
  echo "Usage: $0 <plugin-dir>" >&2
  exit 1
fi

plugin_dir="$(cd "$1" && pwd)"
repo_root="$(cd "$plugin_dir/../.." && pwd)"
manifest_path="$plugin_dir/plugin.json"
dist_dir="$plugin_dir/dist"
package_dir="$plugin_dir/.package"
signing_dir="$plugin_dir/.signing"
legacy_private_key="$dist_dir/signer-private.pem"
private_key="$signing_dir/signer-private.pem"
public_key="$dist_dir/signer-public.pem"

if [[ ! -f "$manifest_path" ]]; then
  echo "plugin.json fehlt: $manifest_path" >&2
  exit 1
fi

version="$(sed -n 's/.*"version": "\([^"]*\)".*/\1/p' "$manifest_path")"
if [[ -z "$version" ]]; then
  echo "Version konnte nicht aus plugin.json gelesen werden." >&2
  exit 1
fi

project_count="$(find "$plugin_dir" -maxdepth 1 -name '*.Plugin.csproj' -print | wc -l | tr -d ' ')"
if [[ "$project_count" -ne 1 ]]; then
  echo "Erwartet genau eine *.Plugin.csproj in $plugin_dir, gefunden: $project_count" >&2
  exit 1
fi

project_file="$(find "$plugin_dir" -maxdepth 1 -name '*.Plugin.csproj' -print | sort | head -n 1)"
assembly_name="$(basename "$project_file" .csproj)"
dll_name="$assembly_name.dll"
build_dir="$plugin_dir/bin/Release/net8.0"
package_name="$(basename "$plugin_dir")-$version"
package_path="$dist_dir/$package_name.zip"

dotnet build "$project_file" -c Release

mkdir -p "$package_dir" "$dist_dir" "$signing_dir"
rm -f "$package_dir"/*
cp "$manifest_path" "$package_dir/plugin.json"
cp "$build_dir/$dll_name" "$package_dir/$dll_name"

rm -f "$package_path"
(
  cd "$package_dir"
  zip -q "$package_path" plugin.json "$dll_name"
)

if [[ ! -f "$private_key" && -f "$legacy_private_key" ]]; then
  cp "$legacy_private_key" "$private_key"
fi

if [[ ! -f "$private_key" ]]; then
  openssl genpkey \
    -algorithm RSA \
    -out "$private_key" \
    -pkeyopt rsa_keygen_bits:3072
fi

openssl rsa \
  -pubout \
  -in "$private_key" \
  -out "$public_key"

openssl dgst \
  -sha256 \
  -sign "$private_key" \
  -out "$dist_dir/$package_name.signature.bin" \
  "$package_path"

openssl base64 \
  -A \
  -in "$dist_dir/$package_name.signature.bin" \
  -out "$dist_dir/$package_name.signature.b64"

hash="$(shasum -a 256 "$package_path" | awk '{print toupper($1)}')"
printf '%s\n' "$hash" > "$dist_dir/$package_name.sha256"

manifest_file="$repo_root/PLUGIN-SHA256SUMS"
relative_package_path="${package_path#"$repo_root"/}"
if [[ -f "$manifest_file" ]]; then
  tmp_manifest="$(mktemp)"
  awk -v package="$relative_package_path" '$2 != package { print }' "$manifest_file" > "$tmp_manifest"
  printf '%s  %s\n' "$hash" "$relative_package_path" >> "$tmp_manifest"
  sort -k2,2 "$tmp_manifest" > "$manifest_file"
  rm -f "$tmp_manifest"
fi

echo "Paket erstellt: $package_path"
