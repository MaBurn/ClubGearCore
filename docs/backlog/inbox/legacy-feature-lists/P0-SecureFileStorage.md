# Secure File and Media Storage

**Priority:** P0  
**Target:** ClubGear Core  
**Estimated effort:** M (6–12 person-days)

## Objective

Provide a single secure storage abstraction for Core and plugin uploads. Business modules store
opaque file references and metadata, never trusted client paths or direct `wwwroot` locations.

## Functional requirements

1. Core exposes services to upload, read, stream, replace, and delete files.
2. Every file has an opaque ID, owner scope, original display name, generated storage name, media
   type, size, checksum, creation time, and creator identity.
3. Upload policies define permitted media types, extensions, maximum size, and access mode.
4. Content signatures are checked where practical; client-provided MIME type and extension are
   not trusted on their own.
5. Downloads require authorization for the owning Core feature or plugin resource.
6. Responses set safe content disposition, content type, cache, and anti-sniffing headers.
7. Images may generate bounded thumbnails without decoding untrusted formats in request threads.
8. Storage supports a local backend first and permits later object-storage implementations.
9. Deleting a business object can mark associated files for deletion; cleanup is retryable and
   reports orphaned data.
10. Plugins use a namespaced storage contract and cannot enumerate or access another plugin's
    files.
11. Upload, download of sensitive files, replacement, and deletion are auditable.
12. Administrators can configure global limits without exposing secrets or filesystem paths.

## Security and operational requirements

- Prevent path traversal, executable upload, unrestricted public URLs, and filename collisions.
- Stream large files; do not load arbitrary uploads fully into memory.
- Support cancellation, bounded processing time, and cleanup of partial uploads.
- Define backup and restore behavior for metadata and file bytes.
- Provide an optional malware-scanning hook with quarantine state.

## Dependencies

- System configuration, authorization, audit, and plugin host context.
- Used by profile images, Inventory 2.0, newsletters, documents, editorial, and rentals.

## Out of scope

- Domain-specific image galleries or document metadata.
- A mandatory cloud vendor or malware-scanner implementation.
- Public CMS routing.

## Acceptance criteria

- Tests reject traversal names, oversized files, invalid types, cross-scope reads, and partial data.
- Two equal original filenames never overwrite each other.
- A plugin can store and retrieve its own file but cannot resolve another plugin's file.
- The local backend can be replaced through dependency injection without domain code changes.

