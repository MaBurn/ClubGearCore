# Plugin Authoring Guide

Audience: Plugin-Autoren, Integrationsentwickler, Reviewer
Scope: Wie Plugins fuer ClubGear erstellt, paketiert, signiert, installiert und aufgerufen werden
Last-Validated: 2026-10-04

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

Beispiel `plugins/Sample.MemberPlugin/Sample.MemberPlugin.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="../../Contracts/Plugin/ClubGear.Plugin.Contracts.csproj" />
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
        RequiredCoreVersion: ">=1.10.0",
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
  "requiredCoreVersion": ">=1.10.0",
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
| `"identity.provider"`    | technical       | `IIdentityProviderPlugin`                | `IdentityProvider`               |

Notes:
- `extensionPoints` values are validated at install time; unrecognized values cause the manifest to be rejected.
- `selfservice.profile` is reserved for a future self-service UI slot; it may be declared to signal intent but has no runtime effect yet.
- `audit.sink` und `identity.provider` sind `generic-core`-Beitraege fuer `technical` Plugins. Aenderungen an diesen Contribution-Typen unterliegen dem ADR 0001 Guardrail GR-002.

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
    public Task OnAuditEventAsync(PluginAuditEvent evt, CancellationToken ct = default)
    {
        // evt.Action, evt.Actor, evt.Before, evt.After, evt.OccurredAtUtc
        return Task.CompletedTask;
    }
}
```

Registrierung in `RegisterContributions`:

```csharp
sink.AddAuditSink(new PluginAuditSinkContribution(
    ProviderType: typeof(MySinkProvider).FullName!,
    Order: 10));
```

`plugin.json` Extension-Point: `"audit.sink"`

---

### Background-Job (Contribution API ohne Manifest-Extension-Point)

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

Fuer Background-Jobs existiert aktuell kein Eintrag in `PluginExtensionPoints`. Deshalb darf
`background.job` **nicht** in `plugin.json.extensionPoints` stehen; der Manifest-Parser wuerde das
Paket sonst ablehnen. Die Runtime-Contribution wird ausschliesslich ueber
`AddBackgroundJob(...)` registriert.

---

### Identity Provider (`identity.provider`, aktueller Contract `1.10.0`)

Ein Identity-Provider-Plugin beschreibt einen externen OpenID-Connect-Provider, prueft dessen
Konfiguration und mappt Claims. Der Core besitzt den OIDC-Handler und erzeugt nach erfolgreicher
Verknuepfung die lokale Application-Session. Reines OAuth 2.0 ohne OIDC, SAML und LDAP werden von
diesem Extension Point nicht unterstuetzt.

```csharp
public sealed class MyIdpPlugin : IIdentityProviderPlugin
{
    public const string Key = "my-oidc";

    public string ProviderKey => Key;
    public string DisplayName => "My OIDC Provider";

    public IReadOnlyList<PluginFieldSchema> GetConfigSchema() =>
    [
        new("authority", "Authority", Required: true, Order: 10),
        new("clientid", "Client ID", Required: true, Order: 20),
        new("clientsecret", "Client Secret", PluginSchemaFieldType.Secret,
            Required: true, Order: 30)
    ];

    public Task<PluginExternalLoginTestResult> TestConnectionAsync(
        IReadOnlyDictionary<string, string> config,
        CancellationToken ct = default)
    {
        string[] required = ["authority", "clientid", "clientsecret"];
        var complete = required.All(key =>
            config.TryGetValue(key, out var value)
            && !string.IsNullOrWhiteSpace(value));

        return Task.FromResult(new PluginExternalLoginTestResult(
            false,
            complete
                ? "Kurzbeispiel: OIDC-Discovery-Test noch nicht implementiert."
                : "Pflichtwerte fehlen."));
    }

    public Task<IReadOnlyList<PluginClaimEntry>> MapClaimsAsync(
        PluginExternalLoginContext ctx,
        CancellationToken ct = default)
    {
        // Verfuegbar: ctx.ProviderKey, ctx.Subject, ctx.RawClaims, ctx.Config.
        var issuer = ctx.RawClaims.TryGetValue("iss", out var rawIssuer)
            && !rawIssuer.TrimStart().StartsWith("[", StringComparison.Ordinal)
                ? rawIssuer
                : ctx.Config.TryGetValue("authority", out var authority)
                    ? authority
                    : throw new InvalidOperationException("OIDC issuer is missing.");
        var material = $"{ctx.ProviderKey}\n{issuer}\n{ctx.Subject}";
        var digest = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(material)))
            .ToLowerInvariant();
        return Task.FromResult<IReadOnlyList<PluginClaimEntry>>(
            [new PluginClaimEntry("sub", $"oidc:{digest}")]);
    }
}
```

Registrierung:

```csharp
sink.AddIdentityProvider(new PluginIdentityProviderContribution(
    ProviderKey: MyIdpPlugin.Key,
    ProviderType: typeof(MyIdpPlugin).FullName!));
```

Provider- und Modulklasse muessen public, konkret, parameterlos konstruierbar und in derselben
Plugin-Assembly enthalten sein. Der Provider-Key traegt kein `oidc.`-Praefix. Contribution-Key und
`IIdentityProviderPlugin.ProviderKey` muessen identisch und ueber alle installierten Plugins
eindeutig sein.

Die OIDC-Provider-Konfiguration erfolgt unter `/Admin/ExternalLogin`. Der Core wertet exakt die
Schema-Keys `authority`, `clientid` und `clientsecret` aus; `enabled` verwaltet der Core separat.
Secrets liegen aktuell im Klartext in `SystemConfigEntry`. Beim IdP wird als Redirect URI
`https://<host>/signin-oidc-<providerKey>` registriert.

Das kurze `TestConnectionAsync` oben ist absichtlich ein fail-closed Signaturbeispiel und meldet
keinen Scheinerfolg. Fuer ein funktionsfaehiges Plugin muss es durch den Discovery-Test der
Detailanleitung ersetzt werden. Ebenso muss produktives Claim-Mapping Mehrfachwerte validieren;
das vollstaendige Beispiel liest `iss` und E-Mail als eindeutige Einzelwerte und faellt fuer einen
fehlenden Issuer auf die konfigurierte Authority zurueck.

Die vollstaendige, gegen den aktuellen Code validierte Anleitung inklusive Manifest,
Kontoverknuepfung, Keycloak-/Entra-Beispielen, Claim-Mehrfachwerten und Abnahmecheckliste steht in
[Identity-Provider-Plugins (OpenID Connect)](identity-provider-plugins.md). Eine direkt
ausfuehrbare Implementierung mit Keycloak-, Entra- und Generic-OIDC-Preset liegt im
[`IamOidc`-Referenzplugin](../../plugins/IamOidc/README.md).

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

### Hauptnavigation (`nav.main`)

Ein Plugin registriert seine Eintraege in der Hauptnavigation ueber `sink.AddNavEntries(...)` in `RegisterContributions`. Jeder Eintrag ist ein flacher Link, sofern er keinen `GroupKey` setzt.

```csharp
sink.AddNavEntries(
[
    new PluginNavEntry(
        Label: "Fahrzeuge",
        Icon: "bi-car-front",
        Route: "/plugin-pages/clubgear.plugin.carinfo/carinfo.list",
        RequiredPermission: "clubgear.plugin.carinfo.read",
        SortOrder: 50)
]);
```

**Regeln:**
- `SortOrder` bestimmt die Reihenfolge unter allen Plugin-Eintraegen (nicht nur den eigenen).
- `RequiredPermission` kann `null` sein; der Eintrag ist dann fuer jeden angemeldeten Nutzer sichtbar.
- Der Host prueft die Berechtigung serverseitig, bevor der Eintrag ueberhaupt gerendert wird — ein nicht sichtbarer Eintrag ist also keine Autorisierungsmassnahme, sondern reines UI-Verhalten.

### Mehrere Navigationseintraege als ein Dropdown gruppieren (`GroupKey`, Contract 1.14.0)

Ein Plugin mit mehreren zusammengehoerenden Seiten (z. B. Buchungen, Budget, Berichte) kann dafuer mehrere flache Links vermeiden und sie statt dessen in **einem** Dropdown in der Hauptnavigation zusammenfassen. Dazu bekommen alle beteiligten `PluginNavEntry`s denselben `GroupKey`; nur der erste (nach `SortOrder`) braucht `GroupLabel`/`GroupIcon` zu setzen — die bestimmen Beschriftung und Icon des Dropdown-Buttons selbst.

```csharp
sink.AddNavEntries(
[
    new PluginNavEntry(
        Label: "Buchungen",
        Icon: "bi-journal-bookmark",
        Route: "/plugin-pages/clubgear.plugin.finance/finance.treasurer.bookings",
        RequiredPermission: "clubgear.plugin.finance.treasurer.read",
        SortOrder: 10)
    {
        GroupKey   = "finance",       // gemeinsamer Dropdown-Schluessel
        GroupLabel = "Finanzen",      // Beschriftung des Dropdown-Buttons
        GroupIcon  = "bi-cash-stack", // Icon des Dropdown-Buttons
    },
    new PluginNavEntry(
        Label: "Berichte",
        Icon: "bi-clipboard-data",
        Route: "/plugin-pages/clubgear.plugin.finance/finance.treasurer.reports",
        RequiredPermission: "clubgear.plugin.finance.treasurer.read",
        SortOrder: 20)
    {
        GroupKey = "finance", // GroupLabel/GroupIcon hier nicht noetig
    }
]);
```

**Regeln:**
- Alle Eintraege mit demselben `GroupKey` erscheinen als **ein** Dropdown; `GroupLabel`/`GroupIcon` werden vom ersten Eintrag der Gruppe (nach `SortOrder`) genommen, bei den anderen werden sie ignoriert.
- Fehlt `GroupKey`, bleibt der Eintrag ein flacher Link (bisheriges Verhalten) — binaerkompatibel, kein Plugin muss sich anpassen.
- `GroupKey` ist nur innerhalb der eigenen Navigation sinnvoll eindeutig zu halten; zwei Plugins mit demselben `GroupKey` wuerden in **einem** gemeinsamen Dropdown landen.

### Mehrere generische Seiten statt einer Seite mit internem "Bereich" (`PageKey`, Contract 1.14.0)

Fuer das Beispiel oben braucht jede Dropdown-Zeile eine **eigene** Seite mit eigenen Spalten/Befehlen — nicht eine Seite, die per Query-Parameter zwischen "Bereichen" umschaltet. Dazu registriert das Plugin mehrere `page.generic`-Provider und gibt jedem einen eigenen `PageKey`:

```csharp
sink.AddPageProvider(new PluginPageProviderContribution(typeof(FinanceBookingsPageProvider).FullName!)
{
    PageKey = "finance.treasurer.bookings"
});
sink.AddPageProvider(new PluginPageProviderContribution(typeof(FinanceReportsPageProvider).FullName!)
{
    PageKey = "finance.treasurer.reports"
});
```

Jede Provider-Klasse implementiert `IPluginPageProvider` eigenstaendig und liefert in `GetPageDefinitionAsync` nur die Spalten/Befehle, die zu ihrem eigenen Bereich gehoeren.

**Regeln:**
- Registriert ein Modul **genau einen** Provider ohne `PageKey` (bisheriges Verhalten), loest der Host weiterhin unabhaengig von der angeforderten Route genau diesen Provider auf.
- Registriert ein Modul **mehrere** Provider, braucht **jeder** einen eigenen, eindeutigen `PageKey` — sonst ist der zweite (und jeder weitere) Provider nie erreichbar, weil der Host sonst immer den erstregistrierten nimmt.
- Die Route, unter der eine Seite erreichbar ist, ist `/plugin-pages/{moduleId}/{pageKey}` — der `PageKey` in der Contribution muss exakt dem Pfadsegment entsprechen, auf das der zugehoerige `PluginNavEntry.Route` zeigt.

**Beispiel:** Finance teilt eine fruehere Einzelseite mit internem Query-Parameter (`?section=...`) in vier eigenstaendige Seiten (`finance.treasurer.fibu`, `.budget`, `.fees`, `.reports`) auf und gruppiert deren Nav-Eintraege unter `GroupKey = "finance"` zu einem "Finanzen"-Dropdown.

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
