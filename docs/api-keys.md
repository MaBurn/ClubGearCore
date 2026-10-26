# API Keys and Service Accounts (CORE-09)

Before this, nothing could authenticate against ClubGear but a browser session. An API key is a
credential for a non-browser caller — a mobile inventory app, an import job, a monitoring probe —
that authenticates as a *service account* rather than as a person.

## Presenting a key

Either header works:

```http
Authorization: Bearer cgk_9f3c1a...
X-API-Key: cgk_9f3c1a...
```

The `cgk_` marker exists so a ClubGear key is recognisable at a glance in a log or a config file
and cannot be confused with some other bearer token.

A request without either header is **not** rejected by this scheme — it falls through to the
Identity cookie, so browser traffic is unaffected. Only a header that is present and wrong fails.

## What a key can and cannot do

A key carries an explicit list of permission keys. Those become `permission` claims on the
principal, which the existing `DatabasePermissionService` already honours, so a permission check
written for a signed-in user works unchanged for a key.

Three limits are enforced in code, not by convention:

| Rule | Why |
|---|---|
| The wildcard `*` cannot be granted | A key is a long-lived secret that sits in someone else's config file. It gets an explicit list or nothing. |
| A key with no scope is refused at creation | It would authenticate but authorise nothing — a configuration mistake worth failing loudly. |
| A key never receives the master-admin claim and holds no roles | An API key cannot be an administrator, and cannot inherit permissions by role membership later. |

A scope containing a comma is refused, because scopes are stored comma-separated and one would
otherwise smuggle in a second scope.

## Storage

The secret is 32 bytes of cryptographic randomness, base64url-encoded. It is returned **once**, at
creation, and never again — only its SHA-256 hash is stored. There is no recovery path; a lost key
is revoked and replaced.

A short prefix (12 characters) is kept in clear. It identifies a key in the UI and narrows the
database lookup on each request. It is not a credential: the decision is made by a fixed-time
comparison of the full hash, so two keys sharing a prefix are still told apart correctly and a
near-miss guess learns nothing from timing.

SHA-256 without a work factor is deliberate. Password hashing needs one because passwords are
low-entropy; a 256-bit random key is not brute-forcible, and a per-request KDF would put avoidable
cost on the authentication path.

## Lifecycle

- **Expiry** is optional. A date in the past is refused at creation, and a key stops working the
  moment the clock passes its expiry.
- **Revocation** is permanent and immediate. A revoked key is never reactivated; issue a new one.
  Revoked keys stay in the table — they are hidden from the default listing, not deleted, so the
  audit trail keeps its subject.
- **Last use** is recorded at a resolution of one minute. Writing it on every request would turn
  authentication into a database write on the hot path.

Creation and revocation are both written to the audit log (`apikey.created`, `apikey.revoked`),
with the key's name, prefix, scopes and expiry — never the secret.

## Managing keys

Issuing and revoking requires the `apikeys.manage` permission, which is separate from
`admin.access`: handing someone the admin area does not hand them the ability to mint credentials.

## What is not built yet

- **The admin UI.** The service, the authentication scheme, the permission and the audit trail
  exist; the pages to drive them from the browser do not. Keys can only be created programmatically
  for now.
- **Applying the scheme to an API surface.** No endpoint yet accepts `ApiKey` authentication. The
  existing `api/plugin/page-commands` endpoint is cookie-authenticated with an antiforgery token,
  which is correct for a browser and wrong for a service account; a key-authenticated surface needs
  its own endpoints with antiforgery off and the scheme named explicitly.
- **Rate limiting.** A key is a long-lived credential with no per-key throttle behind it yet.
