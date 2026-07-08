#!/usr/bin/env python3
import argparse
import json
import re
import sys
import zipfile
from datetime import date
from hashlib import sha256
from pathlib import Path


def read_text(path):
    return path.read_text(encoding="utf-8").strip()


def load_manifest(path):
    with path.open("r", encoding="utf-8") as handle:
        return json.load(handle)


def zip_manifest_matches(zip_path, expected_key, expected_version):
    try:
        with zipfile.ZipFile(zip_path) as archive:
            with archive.open("plugin.json") as manifest_file:
                manifest = json.load(manifest_file)
    except (KeyError, OSError, json.JSONDecodeError, zipfile.BadZipFile):
        return False

    return (
        manifest.get("key") == expected_key
        and manifest.get("version") == expected_version
    )


def find_package(plugin_dir, manifest):
    dist_dir = plugin_dir / "dist"
    if not dist_dir.exists():
        raise SystemExit(f"Kein dist-Ordner gefunden: {dist_dir}")

    version = manifest["version"]
    preferred_names = [
        f"{plugin_dir.name}-{version}.zip",
        f"{manifest['name']}-{version}.zip",
    ]
    for preferred_name in preferred_names:
        preferred_path = dist_dir / preferred_name
        if preferred_path.exists() and zip_manifest_matches(preferred_path, manifest["key"], version):
            return preferred_path

    zips = sorted(dist_dir.glob("*.zip"), key=lambda path: path.stat().st_mtime, reverse=True)
    if not zips:
        raise SystemExit(f"Kein ZIP-Paket in {dist_dir} gefunden.")

    matching = [
        zip_path
        for zip_path in zips
        if zip_manifest_matches(zip_path, manifest["key"], version)
    ]
    if matching:
        return matching[0]

    raise SystemExit(
        "Kein ZIP-Paket passt zu "
        f"{manifest['key']} Version {version}."
    )


def read_sha256(package_path):
    sha_path = package_path.with_suffix(".sha256")
    computed = sha256(package_path.read_bytes()).hexdigest().upper()

    if not sha_path.exists():
        return computed

    stored = read_text(sha_path).split()[0].upper()
    if stored != computed:
        raise SystemExit(
            f"SHA-256 passt nicht zu {package_path.name}: "
            f"{stored} in {sha_path.name}, berechnet {computed}"
        )

    return stored


def relative_to_cwd(path):
    try:
        return path.resolve().relative_to(Path.cwd().resolve()).as_posix()
    except ValueError:
        return path.resolve().as_posix()


def build_markdown(plugin_dir, manifest, package_path, expected_hash, signature, public_key):
    package_location = relative_to_cwd(package_path)
    descriptor = {
        "moduleId": manifest["key"],
        "name": manifest["name"],
        "version": manifest["version"],
        "location": package_location,
        "expectedSha256Hex": expected_hash,
        "signatureBase64": signature,
        "signerPublicKeyPem": public_key,
    }

    return f"""# {manifest["name"]} plugin activation values

Generated: {date.today().isoformat()}

## Package

- pluginKey: `{manifest["key"]}`
- name: `{manifest["name"]}`
- version: `{manifest["version"]}`
- packageFile: `{package_location}`
- packageSizeBytes: `{package_path.stat().st_size}`

## ZIP upload fields

- expectedSha256Hex:

```text
{expected_hash}
```

- signatureBase64:

```text
{signature}
```

- signerPublicKeyPem:

```text
{public_key}
```

## Marketplace descriptor values

```json
{json.dumps(descriptor, ensure_ascii=False, indent=2)}
```
"""


def main():
    parser = argparse.ArgumentParser(
        description="Generate ClubGear plugin activation values from a packaged ZIP."
    )
    parser.add_argument("plugin_dir", help="Path to the plugin directory")
    parser.add_argument(
        "--output",
        help="Output markdown file. Defaults to <plugin_dir>/aktivierungswerte.md",
    )
    args = parser.parse_args()

    plugin_dir = Path(args.plugin_dir).resolve()
    manifest_path = plugin_dir / "plugin.json"
    if not manifest_path.exists():
        raise SystemExit(f"plugin.json fehlt: {manifest_path}")

    manifest = load_manifest(manifest_path)
    for required in ("key", "name", "version"):
        if not manifest.get(required):
            raise SystemExit(f"plugin.json enthaelt kein Feld '{required}'.")

    package_path = find_package(plugin_dir, manifest)
    expected_hash = read_sha256(package_path)

    signature_path = package_path.with_suffix(".signature.b64")
    if not signature_path.exists():
        raise SystemExit(f"Base64-Signatur fehlt: {signature_path}")
    signature = re.sub(r"\s+", "", read_text(signature_path))

    public_key_path = package_path.parent / "signer-public.pem"
    if not public_key_path.exists():
        raise SystemExit(f"Public Key fehlt: {public_key_path}")
    public_key = read_text(public_key_path)

    output_path = Path(args.output).resolve() if args.output else plugin_dir / "aktivierungswerte.md"
    output_path.write_text(
        build_markdown(plugin_dir, manifest, package_path, expected_hash, signature, public_key),
        encoding="utf-8",
    )

    print(f"Aktivierungswerte geschrieben: {output_path}")


if __name__ == "__main__":
    try:
        main()
    except SystemExit:
        raise
    except Exception as exc:
        print(f"Fehler: {exc}", file=sys.stderr)
        raise SystemExit(1)
