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

## Demo Data

A fresh database holds one placeholder member, which makes the list, filter and hierarchy views hard to judge. An optional demo roster — 30 fictional members across the system membership types plus Jugend, Passiv and Ehrenmitglied, including three families with sub-members, two departed members and one deceased — is seeded on startup when you switch it on:

```json
"ClubGear": {
  "SeedDemoData": true
}
```

For containers, set the environment variable instead — this is what the `local` profile in `docker-compose.yml` does:

```bash
ClubGear__SeedDemoData=true
```

Seeding runs once: it stops as soon as member `M-1001` exists, so restarting does not duplicate anything. It adds nothing when the switch is off, and it never touches members that are already there.

The data is invented — fictional names, `clubgear.local` e-mail addresses and phone numbers from the `030 23125` block reserved for film and television. Do not enable it against a database holding real members.

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

Substitute `<version>` with the version you downloaded, for example `2026.7.0`.

```bash
docker load -i clubgear-<version>.tar.gz
docker run --rm -p 5080:8080 -e ASPNETCORE_ENVIRONMENT=Production clubgear:<version>
```

With Podman:

```bash
podman load -i clubgear-<version>.tar.gz
podman run --rm -p 5080:8080 -e ASPNETCORE_ENVIRONMENT=Production clubgear:<version>
```

See [Releases](releases.md) for version history, checksums and release artifact conventions.

## Next Steps

- Read [Releases](releases.md) for version history and importable image artifacts.
- Read [Runtime & Deployment](architecture/runtime-deployment.md) for startup, configuration and container details.
- Read [System Overview](architecture/system-overview.md) for the core architecture.
- Read [Plugin Authoring Guide](architecture/plugin-authoring-guide.md) before adding or changing plugin behavior.
