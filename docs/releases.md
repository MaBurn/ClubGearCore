# Releases

This page tracks public ClubGear Core releases and the artifacts expected for each release.

## Current Release

| Version | Date | Git ref | Artifact | Summary |
|---|---|---|---|---|
| `2026.07-core` | 2026-07-08 | `main` | `clubgear-2026.07-core.tar.gz` | Deployable core source release with Docker/Podman startup docs, importable image workflow, membership hierarchy support and refreshed architecture documentation. |

## Release Artifacts

Each release should provide:

- Source code in the GitHub repository.
- Optional importable container image archive: `clubgear-<version>.tar.gz`.
- SHA256 checksum: `clubgear-<version>.tar.gz.sha256`.
- Release notes on this page.

The image archive is created with:

```bash
./scripts/build-and-export.sh 2026.07-core docker
```

or with Podman:

```bash
./scripts/build-and-export.sh 2026.07-core podman
```

The script builds `clubgear:<version>`, exports it as a gzip-compressed image archive and writes a checksum file when `shasum` or `sha256sum` is available.

## Install From an Image Archive

Download or copy these files to the target host:

```text
clubgear-2026.07-core.tar.gz
clubgear-2026.07-core.tar.gz.sha256
```

Verify the checksum:

```bash
shasum -a 256 -c clubgear-2026.07-core.tar.gz.sha256
```

or on Linux:

```bash
sha256sum -c clubgear-2026.07-core.tar.gz.sha256
```

Load the image:

```bash
docker load -i clubgear-2026.07-core.tar.gz
```

or:

```bash
podman load -i clubgear-2026.07-core.tar.gz
```

Start the imported image:

```bash
docker run --rm -p 5080:8080 -e ASPNETCORE_ENVIRONMENT=Production clubgear:2026.07-core
```

or:

```bash
podman run --rm -p 5080:8080 -e ASPNETCORE_ENVIRONMENT=Production clubgear:2026.07-core
```

Open:

```text
http://localhost:5080
```

## Changelog

### `2026.07-core`

- Published deployable ClubGear Core source without local AI configuration files, local databases or private upload/runtime artifacts.
- Added Quickstart documentation for direct `.NET` startup and container-only startup without a local .NET installation.
- Added importable Docker/Podman image workflow through `scripts/build-and-export.sh`.
- Documented core membership hierarchy and membership-type metadata behavior.
- Refreshed architecture documentation for system overview, core deep dive, data model, migrations and runtime deployment.
- Included deployment scripts for Docker, Podman and project-specific export/deploy flows.

## Release Checklist

- Update the version row and changelog.
- Build and test the source release.
- Run `./scripts/build-and-export.sh <version> docker` or `./scripts/build-and-export.sh <version> podman`.
- Upload the `.tar.gz` and `.sha256` files to the GitHub release.
- Verify `docker load -i <artifact>` and `podman load -i <artifact>` on a clean host.
