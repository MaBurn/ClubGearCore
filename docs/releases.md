# Releases

This page tracks public ClubGear Core releases and the artifacts expected for each release.

The core uses **CalVer**: `<year>.<month>.<counter>`. The third component counts the releases
within a month, so `2026.7.0`, `2026.7.1`, `2026.7.2` may all ship in July; it is not a SemVer
patch level and may contain features. Plugins are versioned independently with SemVer.

The version is declared exactly once, in `ClubGear.csproj`. Never hard-code it: scripts read it
through `scripts/version.sh`, and the running app shows it in the page footer.

For what changed in each release, see [`CHANGELOG.md`](../CHANGELOG.md).

## Release History

| Version | Date | Git ref | Artifact | Summary |
|---|---|---|---|---|
| `2026.10.0` | 2026-10-26 | `main` | `clubgear-2026.10.0.tar.gz` | Plugin page grouping/column preferences, grouped navigation and multi-page plugin modules, icon list columns, external-login admin entry point, and consolidated plugin backlog tracking. |
| `2026.7.0` | 2026-07-08 | `main` | `clubgear-2026.7.0.tar.gz` | Deployable core source release with Docker/Podman startup docs, importable image workflow, membership hierarchy support and refreshed architecture documentation. |

## Release Artifacts

Each release should provide:

- Source code in the GitLab repository.
- Optional importable container image archive: `clubgear-<version>.tar.gz`.
- SHA256 checksum: `clubgear-<version>.tar.gz.sha256`.
- Release notes on this page.

The image archive is created with the script below. Both the tag and the engine are optional:
the tag defaults to the version in `ClubGear.csproj`, the engine to whichever of Docker or
Podman is installed.

```bash
./scripts/build-and-export.sh                    # e.g. clubgear:2026.7.0, engine auto-detected
./scripts/build-and-export.sh "" docker          # force Docker
./scripts/build-and-export.sh 2026.7.1 podman    # explicit tag and engine
```

The script builds `clubgear:<version>`, exports it as a gzip-compressed image archive and writes a checksum file when `shasum` or `sha256sum` is available.

## Install From an Image Archive

The commands below use `<version>` as a placeholder — substitute the version from the release
table above, for example `2026.7.0`.

Download or copy these files to the target host:

```text
clubgear-<version>.tar.gz
clubgear-<version>.tar.gz.sha256
```

Verify the checksum:

```bash
shasum -a 256 -c clubgear-<version>.tar.gz.sha256
```

or on Linux:

```bash
sha256sum -c clubgear-<version>.tar.gz.sha256
```

Load the image:

```bash
docker load -i clubgear-<version>.tar.gz
```

or:

```bash
podman load -i clubgear-<version>.tar.gz
```

Start the imported image:

```bash
docker run --rm -p 5080:8080 -e ASPNETCORE_ENVIRONMENT=Production clubgear:<version>
```

or:

```bash
podman run --rm -p 5080:8080 -e ASPNETCORE_ENVIRONMENT=Production clubgear:<version>
```

Open:

```text
http://localhost:5080
```

Note that `docker run --rm` above keeps no state — it is a smoke test, not a deployment. The
database lives inside the container and is discarded when it stops. For a host that should keep
its data, use the deployment below.

## Deploy on a Docker Host

The `deploy` profile in [`compose.yaml`](../compose.yaml) runs the image that CI published to the
registry. It builds nothing, and it is the only service with a volume, because it is the only one
whose data must outlive the container.

Put `compose.yaml` and a `.env` next to each other on the host:

```dotenv
# .env
CLUBGEAR_IMAGE=registry.example.org/clubgear/clubgearv1:latest
CLUBGEAR_PORT=5080
CLUBGEAR_PLUGINS_ENABLED=false
```

Log in to the registry once, then start it:

```bash
docker login registry.example.org
docker compose --profile deploy up -d
```

Updating to a newer image is the same two commands:

```bash
docker compose --profile deploy pull
docker compose --profile deploy up -d
```

Everything mutable — the SQLite database and the mail pickup directory — is written to `/data`
inside the container, which is the named volume `clubgear_clubgear-data`. Recreating or updating
the container leaves it untouched; only `docker compose down -v` destroys it.

Back it up while the container is stopped, so SQLite is not mid-write:

```bash
docker compose --profile deploy stop
docker run --rm -v clubgear_clubgear-data:/data -v "$PWD:/backup" \
  alpine tar czf /backup/clubgear-data-$(date +%F).tar.gz -C /data .
docker compose --profile deploy start
```

Pin a specific image instead of `:latest` when you want a deployment you can reason about later —
CI tags every build with its commit SHA, so `CLUBGEAR_IMAGE=…/clubgearv1:<sha>` both pins the
version and makes a rollback a one-line edit to `.env` followed by `up -d`.

## Changelog

Release notes live in [`CHANGELOG.md`](../CHANGELOG.md), which is the single source of truth for
what changed and when. This page only describes the artifacts and how to install them.

## Release Checklist

- Bump `<Version>` in `ClubGear.csproj` (this is the only place the version is declared).
- Move the `Unreleased` section of `CHANGELOG.md` under the new version heading.
- Update the version row above.
- Build and test the source release.
- Run `./scripts/build-and-export.sh` (tag defaults to the csproj version) or pass an explicit
  tag and engine: `./scripts/build-and-export.sh <version> docker|podman`.
- Tag the commit: `git tag v<version> && git push origin v<version>`.
- Upload the `.tar.gz` and `.sha256` files to the GitLab release.
- Verify `docker load -i <artifact>` and `podman load -i <artifact>` on a clean host.
