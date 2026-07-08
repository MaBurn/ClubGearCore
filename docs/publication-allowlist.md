# Public Publication Allowlist

Audience: maintainers and release operators
Scope: files that may be copied from the private ClubGear working repository into public publication repositories such as GitHub
Last-Validated: 2026-07-09

## Purpose

ClubGear's private working repository contains application code, public-facing documentation, local runtime artifacts and AI/tooling state. Public publication must be intentionally scoped so that only files representing the application itself are uploaded.

Use this document as the default rule for docs-only publication and public repository syncs.

## Publish

These files may be published when they are relevant to the requested publication scope:

- `README.md` or `Readme.md`.
- `docs/**/*.md`.
- `docs/**/*.puml`, `docs/**/*.drawio` and other intentional architecture diagram sources.
- `docs/**/*.png`, `docs/**/*.jpg`, `docs/**/*.webp` and other intentional documentation images.
- `ADRs/**/*.md` and `adr/**/*.md`.
- Application source files when the publication target is explicitly a source-code release.
- Project files and build manifests required to represent or build the application, such as `.csproj`, `.sln`, `Dockerfile`, package manifests and checked-in scripts that are part of the app workflow.
- Example configuration files that are deliberately sanitized, such as `appsettings.example.json`.

## Do Not Publish

Never publish local state, generated runtime data, private tooling state or machine-specific artifacts:

- `.crispy/**`
- `.agents/**`
- `.codex/**`
- `.claude/**`
- Other AI-agent planning, prompt, run-log or scratch directories.
- `*.db`
- `*.db-shm`
- `*.db-wal`
- `*.sqlite`
- `*.sqlite3`
- `App_Data/MailDrop/**`
- `bin/**`
- `obj/**`
- `.DS_Store`
- `docs/**/.DS_Store`
- Draw.io temporary files such as `.$*.drawio.dtmp`.
- Local archives and exports such as `*.tar`, `*.tar.gz`, `*.zip` unless the artifact is an explicitly intended release package.
- Local test/deployment files such as `docker-compose.portainer-test.yml` unless the user explicitly asks to publish that deployment recipe.
- Secret-bearing configuration files, private keys, certificates, token dumps or environment files.

## Staging Rule

Do not use `git add .` for public publication.

Stage files explicitly from the allowlist. For docs-only publication, the expected set is usually:

```text
README.md
docs/**/*.md
docs/readme-assets/**
docs/architecture/diagrams/**
```

When ADRs are requested for publication, add only the relevant ADR files explicitly:

```text
ADRs/**/*.md
adr/**/*.md
```

## GitHub Docs-Only Publication

For the GitHub publication repository, publish documentation through a separate temporary checkout and copy only the requested allowlisted files into that checkout.

The private working checkout must not be repointed to the GitHub remote. This keeps the public publication boundary separate from the private development repository and prevents accidental upload of AI/tooling files or runtime data.
