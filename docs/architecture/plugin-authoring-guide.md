# Plugin Authoring Guide

Audience: Plugin-Autoren, Integrationsentwickler, Reviewer
Scope: Wie Plugins fuer ClubGear erstellt, paketiert, signiert, installiert und aufgerufen werden
Last-Validated: 2026-06-18

## Purpose

Dieses Dokument ist die praktische Schritt-fuer-Schritt-Anleitung zur Plugin-Entwicklung.
Es beschreibt sowohl die technischen Regeln (Contract-first, Isolation, Sicherheit)
als auch den konkreten Build-, Packaging- und Installationsablauf.

## 1. Voraussetzungen

- .NET 8 SDK
- Zugriff auf das NuGet/Projekt-Artifact `ClubGear.Plugin.Contracts`
- Basiswissen in ASP.NET Core und C#

Wichtig:
- Plugins duerfen nur gegen `ClubGear.Plugin.Contracts` entwickeln.
- Direkte Referenzen auf `ClubGear.Services`, `ClubGear.Data`, `ClubGear.Controllers` oder `ClubGear.Models` sind nicht erlaubt.

## 2. Minimales Plugin-Projekt

Beispiel `Sample.MemberPlugin.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="../Contracts/Plugin/ClubGear.Plugin.Contracts.csproj" />
  </ItemGroup>
</Project>
```

## 3. Plugin-Modul implementieren

Jedes Plugin benoetigt eine Klasse mit `IPluginModule`:

```csharp
using ClubGear.Plugin.Contracts;

namespace Sample.MemberPlugin;

public sealed class SampleMemberPluginModule : IPluginModule
{
    public PluginManifest Manifest => new(
        Key: "sample.member.plugin",
        Name: "Sample Member Plugin",
        Version: new Version(1, 0, 0),
        Author: "Sample Team",
        License: "Proprietary",
        EntryPoint: typeof(SampleMemberPluginModule).FullName!,
        RequiredCoreVersion: ">=1.0.0",
        Permissions: new[] { "members.read" },
        ExtensionPoints: new[] { "member.detail", "runtime.route" });

    public void RegisterContributions(IPluginContributionSink sink)
    {
        sink.AddRoute(new PluginRouteContribution("/health", "members.read"));
        sink.AddMemberProvider(new PluginMemberProviderContribution(
            PluginMemberSlotKind.DetailCard,
            typeof(SampleDetailCardProvider).FullName!,
            Order: 10));
    }
}
```

Optional kann das Plugin Migrationen bereitstellen:

```csharp
public IReadOnlyList<IPluginMigration> GetMigrations()
{
    return new IPluginMigration[]
    {
        new CreateSampleTableMigration()
    };
}
```

## 4. Manifest-Datei (`plugin.json`)

Im ZIP-Paket muss eine `plugin.json` enthalten sein.
Pflichtfelder:
- `key` (alternativ legacy: `moduleId`)
- `name` (alternativ legacy: `displayName`)
- `version` (alternativ legacy: `pluginVersion`)
- `requiredCoreVersion` (alternativ legacy: `requiredContractVersion`)
- `entryPoint` (alternativ legacy: `entryPointType`)

Optional:
- `author`
- `license`
- `permissions`
- `extensionPoints`

Beispiel:

```json
{
  "key": "sample.member.plugin",
  "name": "Sample Member Plugin",
  "version": "1.0.0",
  "requiredCoreVersion": ">=1.0.0",
  "entryPoint": "Sample.MemberPlugin.SampleMemberPluginModule",
  "author": "Sample Team",
  "license": "Proprietary",
  "permissions": ["members.read"],
  "extensionPoints": ["member.detail", "runtime.route"]
}
```

## 4a. Extension-Point Catalog

The `extensionPoints` array in `plugin.json` must contain only strings from the following catalog.
Values are validated at install time; unrecognized values cause the manifest to be rejected.

| Extension-Point String   | Category        | Provider Interface / Contribution Method | `PluginExtensionPoints` Constant |
|--------------------------|-----------------|------------------------------------------|----------------------------------|
| `"member.detail"`        | member-profile  | `IMemberDetailCardProvider`              | `MemberDetail`                   |
| `"member.edit"`          | member-profile  | `IMemberEditTabProvider`                 | `MemberEdit`                     |
| `"member.badge"`         | member-profile  | `IMemberStatusBadgeProvider`             | `MemberBadge`                    |
| `"member.action"`        | member-profile  | `IMemberActionProvider`                  | `MemberAction`                   |
| `"selfservice.profile"`  | member-profile  | reserved — no interface yet              | `SelfServiceProfile`             |
| `"admin.functions"`      | technical       | `IAdminFunctionPanelProvider`            | `AdminFunctions`                 |
| `"runtime.route"`        | technical       | `IPluginContributionSink.AddRoute`       | `RuntimeRoute`                   |
| `"audit.sink"`           | technical       | `IAuditSinkProvider`                     | `AuditSink`                      |
| `"background.job"`       | technical       | `IPluginBackgroundJob`                   | `BackgroundJob`                  |
| `"identity.provider"`    | technical       | `IIdentityProviderPlugin`                | `IdentityProvider`               |

Notes:
- `extensionPoints` values are validated at install time; unrecognized values cause the manifest to be rejected.
- `selfservice.profile` is reserved for a future self-service UI slot; it may be declared to signal intent but has no runtime effect yet.
- `audit.sink`, `background.job` und `identity.provider` sind `generic-core`-Beitraege fuer `technical` Plugins. Aenderungen an diesen Contribution-Typen unterliegen dem ADR 0001 Guardrail GR-002.

## 4b. Member-Edit-Tab-Gruppen (`GroupKey` / `GroupTitle`)

Mehrere Plugins koennen ihre Edit-Tabs in einem gemeinsamen Bootstrap-Kasten zusammenfassen, anstatt je einen eigenen Kasten zu erhalten. Dazu setzen sie in `GetTabsAsync` die optionalen init-Properties `GroupKey` und `GroupTitle` auf dem zurueckgegebenen `MemberEditTabSlot`.

```csharp
public Task<IEnumerable<MemberEditTabSlot>> GetTabsAsync(PluginTabContext ctx)
{
    return Task.FromResult<IEnumerable<MemberEditTabSlot>>(new[]
    {
        new MemberEditTabSlot(
            Key: "my.tab",
            Title: "Mein Reiter",
            Content: "<p>Inhalt</p>",
            Order: 10)
        {
            GroupKey  = "fahrzeuge",   // gemeinsamer Kasten-Schluessel
            GroupTitle = "Fahrzeuge",  // Beschriftung des Kastens
        }
    });
}
```

**Regeln:**
- Alle Plugins, die denselben `GroupKey` verwenden, landen in **einem** Bootstrap-Kasten mit Bootstrap-Nav-Tabs.
- `GroupTitle` sollte bei allen Plugins mit gleichem `GroupKey` identisch sein — der Host nimmt den Wert des ersten sortierten Tabs.
- Fehlen `GroupKey` und `GroupTitle`, rendert der Host einen separaten Einzel-Kasten (bisheriges Verhalten).
- Die Properties sind `init`-only und nicht positional — bestehende Plugins, die den Konstruktor positional aufrufen, sind binaer kompatibel.

**Beispiel:** CarInfo und ServiceBook teilen `GroupKey = "fahrzeuge"` und erscheinen dem Nutzer als ein Kasten "Fahrzeuge" mit zwei Reitern.

## 4c. Technische Contribution-Typen (technical Plugins)

### Audit-Sink (`audit.sink`, `ContractVersion` >= 1.6.0)

Ein Audit-Sink empfaengt jeden Audit-Log-Eintrag, den der Core schreibt, und kann ihn an ein externes System weiterleiten (z.B. SIEM, Elasticsearch). Der Host ruft alle registrierten Sinks parallel und mit Fehler-Isolation auf.

```csharp
public sealed class MySinkProvider : IAuditSinkProvider
{
    public Task HandleAuditEventAsync(PluginAuditEvent evt, CancellationToken ct)
    {
        // evt.Action, evt.Actor, evt.Before, evt.After, evt.OccurredAtUtc
        return Task.CompletedTask;
    }
}
```

Registrierung in `RegisterContributions`:

```csharp
sink.AddAuditSink(new PluginAuditSinkContribution(
    Key: "my.audit.sink",
    SinkType: typeof(MySinkProvider).FullName!));
```

`plugin.json` Extension-Point: `"audit.sink"`

---

### Background-Job (`background.job`, `ContractVersion` >= 1.6.0)

Background-Jobs werden vom Host beim Plugin-Aktivieren gestartet und beim Deaktivieren sauber gestoppt (5-Sekunden-Drain). Jeder Job laeuft in einem eigenen `IServiceScope` und erhaelt einen `CancellationToken`.

```csharp
public sealed class MyJob : IPluginBackgroundJob
{
    public async Task ExecuteAsync(IPluginHostContext ctx, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            // periodische Arbeit
            await Task.Delay(TimeSpan.FromMinutes(5), ct);
        }
    }
}
```

Registrierung:

```csharp
sink.AddBackgroundJob(new PluginBackgroundJobContribution(
    Key: "my.sync.job",
    JobType: typeof(MyJob).FullName!));
```

Laufzeitstatus (State, LastRunUtc, LastError) ist in der Plugin-Admin-Detailansicht sichtbar (`/PluginAdmin/Detail/{key}`).

`plugin.json` Extension-Point: `"background.job"`

---

### Identity Provider (`identity.provider`, `ContractVersion` >= 1.7.0)

Ein Identity-Provider-Plugin liefert Claims-Mapping-Logik fuer den konfigurierten externen OIDC-Provider. Der Host ruft `MapClaimsAsync` nach erfolgreichem OIDC-Callback auf und reichert den Login-Kontext mit plugin-eigenen Claims an.

```csharp
public sealed class MyIdpPlugin : IIdentityProviderPlugin
{
    public Task<IReadOnlyList<PluginClaimEntry>> MapClaimsAsync(
        PluginExternalLoginContext ctx,
        CancellationToken ct)
    {
        // ctx.Subject, ctx.ProviderClaims, ctx.ProviderKey
        var claims = new List<PluginClaimEntry>
        {
            new("custom.role", ctx.ProviderClaims["groups"])
        };
        return Task.FromResult<IReadOnlyList<PluginClaimEntry>>(claims);
    }
}
```

Registrierung:

```csharp
sink.AddIdentityProvider(new PluginIdentityProviderContribution(
    Key: "my.idp",
    PluginType: typeof(MyIdpPlugin).FullName!,
    ProviderKey: "oidc.generic"));
```

Die OIDC-Provider-Konfiguration (Authority, Client-ID, Client-Secret) erfolgt ueber den Admin-Bereich unter `/Admin/ExternalLogin`, nicht ueber den Plugin-Code. Secrets werden in `SystemConfigEntry` gespeichert (aktuell Klartext — Verschluesselung ist geplant).

`plugin.json` Extension-Point: `"identity.provider"`

---

## 5. Erlaubte Host-Interaktion

Im Runtime-Kontext stehen die Contract-Facades zur Verfuegung:
- `IPluginRuntimeBridge.HasPermissionAsync(...)`
- `IPluginRuntimeBridge.LogAsync(...)`
- `IPluginRuntimeBridge.NotifyAsync(...)`
- `IPluginHostContext.Members`
- `IPluginHostContext.MemberActions`
- `IPluginHostContext.Persistence`

Fuer UI-getriebene Erweiterungen stehen zusaetzlich folgende Contribution-Typen zur Verfuegung:
- Member-Slots (`DetailCard`, `EditTab`, `StatusBadge`, `Action`)
- Admin-Function-Panels ueber `IAdminFunctionPanelProvider`
- Schema-gesteuerte Eingabeparameter fuer Member-Aktionen und Admin-Befehle

Isolationsregel:
- Handler mit direkten Referenzen auf verbotene Core-Namensraeume werden durch den Host blockiert.

## 6. UI-Erweiterungen registrieren

### Member-Aktion mit Eingabeschema

Plugins koennen Member-Aktionen mit Eingabeschema bereitstellen. Der Host rendert dafuer automatisch ein generisches Modal in Mitgliederverwaltung und Selfservice.

Beispielhaftes Zielbild:
- Action-Key: `carinfo.add`
- Sichtbar in `Views/Members/_HeaderActions.cshtml` und `Views/SelfService/Profile.cshtml`
- Submit an `POST /api/member/plugin-actions` oder `POST /api/self-service/plugin-actions`

Die Eingabefelder werden ueber `PluginFieldSchema` beschrieben. Feldfehler werden vom Host auf einzelne Formfelder zurueckgemappt.

### Admin/Functions-Panel

Plugins koennen in `Admin/Functions` ein generisches Panel registrieren. Der Host laedt die Panel-Metadaten ueber `GET /api/admin/plugin-commands/panels` und fuehrt schema-gesteuerte Befehle ueber `POST /api/admin/plugin-commands` aus.

Das ist fuer plugin-eigene Konfigurationsflaechen gedacht, ohne plugin-spezifische Core-Views einzubauen.

## 7. Runtime-Route aufrufen

Der Host stellt einen Dispatcher fuer Plugin-GET-Routen bereit:
- `GET /api/plugin-runtime/{moduleId}` fuer Route `/`
- `GET /api/plugin-runtime/{moduleId}/{**routePath}` fuer weitere Pfade

Beispiel:

```bash
curl -H "Authorization: Bearer <token>" \
  "https://localhost:5001/api/plugin-runtime/sample.member.plugin/health"
```

## 8. Paket erstellen (ZIP)

Das ZIP sollte mindestens enthalten:
- `plugin.json`
- Plugin-Assembly (`*.dll`)
- ggf. weitere Abhaengigkeiten (`*.dll`)

Beispiel (vereinfacht):

```bash
dotnet build -c Release
mkdir -p out/package
cp plugin.json out/package/
cp bin/Release/net8.0/Sample.MemberPlugin.dll out/package/
cd out/package && zip -r ../sample.member.plugin.zip .
```

## 9. Hash und Signatur erzeugen (ZIP-Installation)

Der Host erwartet bei ZIP-Installation:
- SHA-256 Hash (hex)
- RSA-Signatur (Base64)
- Public Key (PEM)

Beispiel mit OpenSSL:

```bash
# 1) Schluessel erstellen (nur einmal)
openssl genpkey -algorithm RSA -out signer-private.pem -pkeyopt rsa_keygen_bits:3072
openssl rsa -pubout -in signer-private.pem -out signer-public.pem

# 2) Hash (hex)
shasum -a 256 out/sample.member.plugin.zip | awk '{print toupper($1)}'

# 3) Signatur (Binary -> Base64)
openssl dgst -sha256 -sign signer-private.pem -out signature.bin out/sample.member.plugin.zip
base64 < signature.bin
```

## 10. Installation im Host

### Marketplace

```http
POST /api/plugins/install/marketplace
Content-Type: application/json

{
  "moduleId": "sample.member.plugin"
}
```

### ZIP

```http
POST /api/plugins/install/zip
Content-Type: application/json

{
  "fileName": "sample.member.plugin.zip",
  "zipPackageBase64": "<base64-zip>",
  "expectedSha256Hex": "<SHA256_HEX>",
  "signatureBase64": "<BASE64_SIGNATURE>",
  "signerPublicKeyPem": "-----BEGIN PUBLIC KEY-----..."
}
```

Aktivieren/Deaktivieren:
- `POST /api/plugins/{moduleId}/activate`
- `POST /api/plugins/{moduleId}/deactivate`

Statusabfrage:
- `GET /api/plugins/installed`

## 11. Typische Fehler

- Manifestfelder fehlen oder haben falschen Namen.
- `entryPoint` zeigt auf einen Typ, der `IPluginModule` nicht implementiert.
- ZIP ohne `plugin.json`.
- Hash oder Signatur passt nicht zur ZIP-Datei.
- Plugin referenziert verbotene Core-Namespaces.
- Plugin-Aktionen definieren ein Schema, liefern aber keine zu den Feldkeys passenden Validierungsfehler.
- Admin-Panels registrieren Befehle ohne Permission-Key oder ohne eindeutige Command-Keys.

## References

- [Plugin Boundary & Compliance](plugin-boundary-and-compliance.md)
- [Plugin License Boundary Policy](plugin-license-boundary.md)
- [UI Flow Map](ui-flow-map.md)
