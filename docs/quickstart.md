# Quickstart

Use this guide to get ClubGear running locally from a fresh checkout.

## Requirements

- Git
- .NET SDK 8, only for direct `dotnet run` development
- Docker or Podman, for container-only runs

ClubGear uses SQLite through EF Core. No separate database server is required for local development.

## Run Without a Local .NET Installation

Use this path when the host machine should not install the .NET SDK or runtime. Docker or Podman builds the application inside the SDK container image and then starts the published app in the runtime container.

With Docker:

```bash
git clone <repository-url>
cd ClubGear
./scripts/start-docker.sh local
```

Equivalent direct compose command:

```bash
docker compose --profile local up --build
```

Then open:

```text
http://localhost:5080
```

With Podman:

```bash
git clone <repository-url>
cd ClubGear
./scripts/start-podman.sh local
```

Equivalent direct compose command:

```bash
podman compose --profile local up --build
```

The compose profiles expose these ports:

| Profile | Command argument | URL |
|---|---|---|
| Local development | `local` | `http://localhost:5080` |
| Staging-like run | `staging` | `http://localhost:5081` |
| Production-like run | `prod` | `http://localhost:5082` |

The first build downloads the .NET container images and NuGet packages inside the build container. The host only needs Git and Docker or Podman.

## Run Locally

Restore and start the development profile:

```bash
dotnet restore
dotnet run --launch-profile http
```

Open the app at:

```text
http://localhost:5007
```

The development profile uses `appsettings.Development.json` and the local SQLite database `clubmanagerv0_2.dev.db`. On first startup, ClubGear creates or patches the schema through `ApplicationSeeder`.

## Run Tests

Run the architecture and behavior tests:

```bash
dotnet test tests/ClubGear.ArchitectureTests/ClubGear.ArchitectureTests.csproj
```

Use the focused test classes in `tests/ClubGear.ArchitectureTests/` when changing plugin contracts, membership-type behavior, member hierarchy rendering or plugin-boundary rules.

## Run With Docker

Start the local compose profile:

```bash
docker compose --profile local up --build
```

Then open:

```text
http://localhost:5080
```

The compose file also contains `staging` and `prod` profiles on ports `5081` and `5082`.

## Run With Podman

Use the provided helper script:

```bash
./scripts/start-podman.sh
```

If Docker is your local engine, use:

```bash
./scripts/start-docker.sh
```

## Use an Importable Image Archive

Release builds can be distributed as `clubgear-<version>.tar.gz` image archives. They do not require the target host to clone the repository or build the image.

```bash
docker load -i clubgear-2026.07-core.tar.gz
docker run --rm -p 5080:8080 -e ASPNETCORE_ENVIRONMENT=Production clubgear:2026.07-core
```

With Podman:

```bash
podman load -i clubgear-2026.07-core.tar.gz
podman run --rm -p 5080:8080 -e ASPNETCORE_ENVIRONMENT=Production clubgear:2026.07-core
```

See [Releases](releases.md) for version history, checksums and release artifact conventions.

## Next Steps

- Read [Releases](releases.md) for version history and importable image artifacts.
- Read [Runtime & Deployment](architecture/runtime-deployment.md) for startup, configuration and container details.
- Read [System Overview](architecture/system-overview.md) for the core architecture.
- Read [Plugin Authoring Guide](architecture/plugin-authoring-guide.md) before adding or changing plugin behavior.
