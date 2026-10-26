# Identity-Provider-Plugins (OpenID Connect)

Audience: Plugin-Autoren, IAM-Integratoren, Reviewer, Betreiber
Scope: Funktionsfaehige Integration externer OpenID-Connect-Provider ueber ClubGear-Plugins
Last-Validated: 2026-08-05

## Unterstuetzter Standard und Verantwortungsgrenze

ClubGear unterstuetzt externe Anmeldung ueber **OpenID Connect (OIDC)**. Der Core besitzt den
ASP.NET-Core-OIDC-Handler, die temporaere externe Session und die lokale Identity-Session. Ein
`identity.provider`-Plugin liefert ausschliesslich:

- einen stabilen Provider-Schluessel und Anzeigenamen,
- das Konfigurationsschema fuer die Admin-Oberflaeche,
- einen Verbindungstest,
- die Abbildung eingehender OIDC-Claims auf ClubGear-Session-Claims und optional einen stabilen
  Link-Schluessel vom Typ `sub`.

Das Plugin registriert keinen eigenen Authentication Handler. Reines OAuth 2.0 ohne OIDC
Discovery und ID Token, SAML und LDAP werden von diesem Contract nicht unterstuetzt. Das Plugin
erhaelt weder Access Token noch ID Token und kann derzeit keine zusaetzlichen Scopes, UserInfo,
Response Types oder Tokenvalidierungsparameter konfigurieren.

## Aktueller Contract

Massgebliche Typen:

- `IIdentityProviderPlugin`
- `PluginExternalLoginContext`
- `PluginClaimEntry`
- `PluginExternalLoginTestResult`
- `PluginIdentityProviderContribution`
- `PluginExtensionPoints.IdentityProvider`

Der aktuelle Host verwendet `ContractVersion.Current = 1.10.0` und akzeptiert Anforderungen im
Fenster von `1.8.0` bis einschliesslich `1.10.0`. Ein Plugin, das eine neuere Contract-Version
fordert, wird bis zu einem passenden Core-Upgrade abgewiesen. Ein neu gegen den aktuellen
Contract gebautes Plugin deklariert deshalb
`requiredCoreVersion: ">=1.10.0"`. Trotz des historischen Namens bezeichnet dieses Manifestfeld
die benoetigte **Plugin-Contract-Version**, nicht die CalVer aus `ClubGear.csproj`.

## Funktionsfaehiges Referenzplugin

Die produktionsnahe Referenz liegt unter
[`plugins/IamOidc`](../../plugins/IamOidc/README.md). Sie registriert drei eigenstaendige
Contributions mit festen Provider-Keys:

| Preset | Provider-Key | Einsatz |
|---|---|---|
| Keycloak | `iam-keycloak` | Keycloak Realm-Authority |
| Microsoft Entra ID / Azure AD | `iam-entra` | Tenant-spezifische Entra-v2-Authority |
| OpenID Connect (generisch) | `iam-oidc` | Standardkonformer OIDC-Issuer |

Das Projekt besitzt eigene Offline-Tests, einen signierten Paketworkflow und eine
Schritt-fuer-Schritt-Einrichtung fuer alle drei Anbieter. Es ist die ausfuehrbare Referenz fuer
Discovery-Haertung, stabile Link-Keys und Claim-Mapping; das folgende Kurzbeispiel erklaert nur
die Contract-Form.

## Projekt und Manifest

Solange die Contracts nicht als versioniertes Paket publiziert werden, verwendet ein Plugin im
ClubGear-Arbeitsbaum eine Projekt-Referenz:

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

Ein spaeter eigenstaendiges Git-Repository darf diesen relativen Pfad nicht beibehalten. Es
benoetigt ein versioniertes Contracts-Artefakt oder einen explizit versionierten Source-Bezug.

Minimale `plugin.json`:

```json
{
  "key": "clubgear.plugin.iam.oidc",
  "name": "ClubGear IAM OIDC",
  "version": "0.1.0",
  "requiredCoreVersion": ">=1.10.0",
  "entryPoint": "ClubGear.Plugin.IamOidc.IamOidcPluginModule",
  "author": "ClubGear",
  "license": "Proprietary",
  "category": "technical",
  "permissions": [],
  "extensionPoints": ["identity.provider"]
}
```

`plugin.json` und `IPluginModule.Manifest` muessen mindestens bei Key, Version, Entry Point und
Contract-Anforderung denselben Stand beschreiben.

## Vereinfachtes, kompilierbares Contract-Beispiel

Provider- und Modulklasse muessen `public`, konkret, parameterlos konstruierbar und in derselben
Plugin-Assembly enthalten sein. Der Core erzeugt Provider-Instanzen mit
`Activator.CreateInstance`; Constructor Injection steht hier nicht zur Verfuegung.

Das Beispiel ist bewusst auf eine generische Contribution gekuerzt. Fuer produktiven Einsatz
sind insbesondere Redirect-Abweisung, Antwortgroessen-/Zeitlimits, `code`-Pruefung,
provider-spezifische Identitaetsclaims und Tests aus dem Referenzplugin zu uebernehmen.

```csharp
using System.Security.Cryptography;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using ClubGear.Plugin.Contracts;

namespace ClubGear.Plugin.IamOidc;

public sealed class IamOidcPluginModule : IPluginModule
{
    public PluginManifest Manifest => new(
        Key: "clubgear.plugin.iam.oidc",
        Name: "ClubGear IAM OIDC",
        Version: new Version(0, 1, 0),
        Author: "ClubGear",
        License: "Proprietary",
        EntryPoint: typeof(IamOidcPluginModule).FullName!,
        RequiredCoreVersion: ">=1.10.0",
        Permissions: Array.Empty<string>(),
        ExtensionPoints: [PluginExtensionPoints.IdentityProvider])
    {
        Category = "technical"
    };

    public void RegisterContributions(IPluginContributionSink sink)
    {
        sink.AddIdentityProvider(new PluginIdentityProviderContribution(
            ProviderKey: GenericOidcIdentityProvider.Key,
            ProviderType: typeof(GenericOidcIdentityProvider).FullName!));
    }
}

public sealed class GenericOidcIdentityProvider : IIdentityProviderPlugin
{
    public const string Key = "iam-oidc";
    private static readonly HttpClient DiscoveryClient = new()
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    public string ProviderKey => Key;
    public string DisplayName => "OpenID Connect";

    public IReadOnlyList<PluginFieldSchema> GetConfigSchema() =>
    [
        new("authority", "Authority / Issuer URL", Required: true, Order: 10,
            HelpText: "Zum Beispiel https://id.example/realms/club"),
        new("clientid", "Client ID", Required: true, Order: 20),
        new("clientsecret", "Client Secret", PluginSchemaFieldType.Secret,
            Required: true, Order: 30)
    ];

    public async Task<PluginExternalLoginTestResult> TestConnectionAsync(
        IReadOnlyDictionary<string, string> config,
        CancellationToken ct = default)
    {
        string[] required = ["authority", "clientid", "clientsecret"];
        var missing = required.Where(key =>
            !config.TryGetValue(key, out var value)
            || string.IsNullOrWhiteSpace(value)).ToArray();

        if (missing.Length > 0)
        {
            return new PluginExternalLoginTestResult(
                false,
                $"Fehlende Konfiguration: {string.Join(", ", missing)}");
        }

        if (!TryNormalizeIssuer(config["authority"], out var expectedIssuer)
            || !Uri.TryCreate(expectedIssuer, UriKind.Absolute, out var authority))
        {
            return new PluginExternalLoginTestResult(
                false,
                "Authority muss HTTPS verwenden; HTTP ist nur fuer Loopback-Entwicklung erlaubt.");
        }

        try
        {
            var discoveryUri = new Uri(
                $"{authority.AbsoluteUri.TrimEnd('/')}/.well-known/openid-configuration");
            using var response = await DiscoveryClient.GetAsync(discoveryUri, ct);
            response.EnsureSuccessStatusCode();
            await using var body = await response.Content.ReadAsStreamAsync(ct);
            using var document = await JsonDocument.ParseAsync(body, cancellationToken: ct);
            var root = document.RootElement;
            var issuerMatches = root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty("issuer", out var issuerElement)
                && issuerElement.ValueKind == JsonValueKind.String
                && TryNormalizeIssuer(issuerElement.GetString(), out var discoveredIssuer)
                && string.Equals(discoveredIssuer, expectedIssuer, StringComparison.Ordinal);
            string[] endpointNames =
                ["authorization_endpoint", "token_endpoint", "jwks_uri"];
            var endpointsValid = root.ValueKind == JsonValueKind.Object
                && endpointNames.All(name =>
                    root.TryGetProperty(name, out var endpointElement)
                    && endpointElement.ValueKind == JsonValueKind.String
                    && Uri.TryCreate(endpointElement.GetString(), UriKind.Absolute, out var endpoint)
                    && UsesPermittedTransport(endpoint));

            return issuerMatches && endpointsValid
                ? new PluginExternalLoginTestResult(true, "OIDC Discovery ist konsistent.")
                : new PluginExternalLoginTestResult(
                    false,
                    "Discovery-Issuer oder erforderliche Endpoints passen nicht zur Authority.");
        }
        catch (OperationCanceledException exception) when (!ct.IsCancellationRequested)
        {
            return new PluginExternalLoginTestResult(false, exception.Message);
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException)
        {
            return new PluginExternalLoginTestResult(false, exception.Message);
        }
    }

    public Task<IReadOnlyList<PluginClaimEntry>> MapClaimsAsync(
        PluginExternalLoginContext context,
        CancellationToken ct = default)
    {
        // Member.OauthID ist global eindeutig. Provider und Issuer werden deshalb
        // in einen kurzen, stabilen Link-Schluessel einbezogen.
        var configuredAuthority = context.Config.TryGetValue("authority", out var authority)
            ? authority
            : throw new InvalidOperationException("OIDC authority is missing.");
        var rawIssuer = ReadOptionalSingleton(context.RawClaims, "iss")
            ?? configuredAuthority;
        if (!TryNormalizeIssuer(rawIssuer, out var issuer))
        {
            throw new InvalidOperationException("OIDC issuer is invalid.");
        }

        var subjectMaterial = $"{context.ProviderKey}\n{issuer}\n{context.Subject}";
        var subjectHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(subjectMaterial)))
            .ToLowerInvariant();
        var linkedSubject = $"oidc:{subjectHash}";

        var mapped = new List<PluginClaimEntry>
        {
            new("sub", linkedSubject)
        };

        var email = ReadOptionalSingleton(context.RawClaims, "email")
            ?? ReadOptionalSingleton(context.RawClaims, ClaimTypes.Email);
        if (!string.IsNullOrWhiteSpace(email))
        {
            mapped.Add(new PluginClaimEntry("email", email));
        }

        return Task.FromResult<IReadOnlyList<PluginClaimEntry>>(mapped);
    }

    private static string? ReadOptionalSingleton(
        IReadOnlyDictionary<string, string> claims,
        string claimType)
    {
        if (!claims.TryGetValue(claimType, out var raw))
        {
            return null;
        }

        if (!raw.TrimStart().StartsWith("[", StringComparison.Ordinal))
        {
            return !string.IsNullOrWhiteSpace(raw)
                ? raw
                : throw new InvalidOperationException($"Claim '{claimType}' is empty.");
        }

        using var document = JsonDocument.Parse(raw);
        if (document.RootElement.ValueKind != JsonValueKind.Array
            || document.RootElement.GetArrayLength() != 1
            || document.RootElement[0].ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(document.RootElement[0].GetString()))
        {
            throw new InvalidOperationException(
                $"Claim '{claimType}' must contain exactly one non-empty value.");
        }

        return document.RootElement[0].GetString();
    }

    private static bool TryNormalizeIssuer(string? value, out string normalized)
    {
        normalized = string.Empty;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || !UsesPermittedTransport(uri)
            || string.IsNullOrWhiteSpace(uri.Host)
            || !string.IsNullOrEmpty(uri.UserInfo)
            || !string.IsNullOrEmpty(uri.Query)
            || !string.IsNullOrEmpty(uri.Fragment))
        {
            return false;
        }

        normalized = uri.AbsoluteUri.TrimEnd('/');
        return true;
    }

    private static bool UsesPermittedTransport(Uri uri)
        => string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
           || (string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
               && uri.IsLoopback);
}
```

Der Wert von `IIdentityProviderPlugin.ProviderKey` und der
`PluginIdentityProviderContribution.ProviderKey` muessen identisch sein. Der Key enthaelt kein
`oidc.`-Praefix. Er muss 1 bis 128 Zeichen lang sein, mit einem ASCII-Buchstaben oder einer Ziffer
beginnen und darf danach nur ASCII-Buchstaben, Ziffern, Punkt, Unterstrich oder Bindestrich
enthalten (`^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$`). `ProviderType` ist der case-sensitive,
vollqualifizierte CLR-Typname.

## Konfiguration und Redirect URI

Nach Installation und Aktivierung des Plugins erscheint jeder deklarierte Provider unter
`/Admin/ExternalLogin`. Die drei vom Core ausgewerteten Schema-Keys heissen exakt:

| Key | Bedeutung |
|---|---|
| `authority` | OIDC-Issuer/Authority mit Discovery-Dokument |
| `clientid` | Client-ID des vertraulichen Web-Clients |
| `clientsecret` | Client-Secret des vertraulichen Web-Clients |

`enabled` wird vom Core ueber den Aktivieren-Schalter verwaltet und gehoert nicht in das
Plugin-Schema. Ein leer abgesendetes Secret behaelt einen vorhandenen Wert bei. Die Werte liegen
derzeit unverschluesselt in `SystemConfigEntry`; Datenbank, Backups und Admin-Zugriff muessen
entsprechend geschuetzt werden.

Der Core registriert beim Start nach dem Laden der aktivierten Plugins fuer jeden aktivierten,
verfuegbaren Provider das provider-spezifische Scheme `oidc.{providerKey}`. Der Challenge-Pfad
registriert es zusaetzlich bei Bedarf, etwa wenn ein Provider erst spaeter aktiviert wurde. Damit
ist der Callback auch nach einem Prozessneustart oder auf einem frisch gestarteten Knoten bekannt.
Der Handler verwendet folgende externe Redirect URI:

```text
https://<clubgear-host>/signin-oidc-<providerKey>
```

Diese URI muss exakt beim IdP als erlaubte Redirect URI eingetragen werden.
`/Account/ExternalLoginCallback` ist der anschliessende interne ClubGear-Redirect und darf nicht
anstelle der OIDC-Callback-URI beim Provider registriert werden.

Beispiele:

| Provider | Beispiel-Authority | Redirect URI bei Provider-Key |
|---|---|---|
| Keycloak | `https://id.example/realms/club` | `https://club.example/signin-oidc-iam-keycloak` |
| Microsoft Entra ID | `https://login.microsoftonline.com/<tenant-id>/v2.0` | `https://club.example/signin-oidc-iam-entra` |
| Generisches OIDC | `https://id.example` | `https://club.example/signin-oidc-iam-oidc` |

Der Host erwartet derzeit einen vertraulichen Web-Client mit Secret. Authority und Discovery-
Endpoints muessen HTTPS verwenden; fuer lokale Entwicklung wird `http://` ausschliesslich bei
Loopback-Authorities akzeptiert. Der Core erzwingt vor Registrierung Pflichtwerte sowie Syntax
und Transport der Authority; die tiefere Discovery-Pruefung von Issuer und Endpoints erfolgt im
Plugin-Verbindungstest und ist derzeit kein Aktivierungs-Gate. Public Clients ohne Secret sind
nicht unterstuetzt. Eine aktivierte, aber unvollstaendige oder core-seitig unbrauchbare
Konfiguration wird nicht als Scheme registriert.
Wird eine bereits registrierte Konfiguration spaeter ungueltig oder deaktiviert, materialisiert
der Core nichtwerfende Quarantaene-Options; dadurch bleibt die Anwendung samt Reparatur-UI
erreichbar. Der normale ClubGear-Challenge- und Callback-Pfad weist den Provider fail-closed ab;
eine direkte oder race-bedingte Handler-Challenge bleibt durch die statische Konfiguration inert
und kann keinen externen Login abschliessen.

Hinter einem TLS-terminierenden Reverse Proxy muessen Scheme und Host korrekt an ClubGear
weitergereicht und vom Deployment als vertrauenswuerdig konfiguriert werden. Andernfalls erzeugt
der Challenge eine falsche `http://`- oder interne Redirect URI; der Core konfiguriert derzeit
keine deployment-spezifischen Forwarded-Header-Netze automatisch.

## Claim-Mapping und lokale Kontoverknuepfung

`PluginExternalLoginContext` enthaelt:

- `ProviderKey`: logischer Provider-Key,
- `Subject`: vom OIDC-Handler ermitteltes Subject,
- `RawClaims`: vom ASP.NET-Core-Handler projizierte Claims,
- `Config`: gespeicherte Provider-Konfiguration.

`RawClaims` ist trotz des Namens kein unveraenderter ID-Token. Bei genau einem Wert steht der
Claim als String im Dictionary. Wiederholte Claim-Typen, etwa `groups` oder `roles`, werden als
JSON-Array-String dargestellt, zum Beispiel `["group-a","group-b"]`. Ein Plugin muss diesen Fall
vor der Gruppen-/Profilauswertung erkennen und deserialisieren. Durch das standardmaessige
.NET-Inbound-Mapping koennen bekannte Claims sowohl unter dem kurzen JWT-Namen (`email`, `sub`)
als auch unter einem `ClaimTypes`-URI-Namen erscheinen. Ein Plugin muss die im Testsystem
tatsaechlich projizierten Keys pruefen; das Beispiel behandelt beide E-Mail-Varianten.
Der Core behaelt den bereits durch den OIDC-Handler validierten `iss`-Claim ausdruecklich bei,
damit das Plugin den globalen Link-Key issuer-spezifisch bilden kann. Fehlt er dennoch, verwendet
das Beispiel die konfigurierte Authority als fail-closed Namespace-Fallback und niemals nur den
rohen `sub`-Wert.

Ein von `MapClaimsAsync` gelieferter Claim vom Typ `sub` ersetzt den Link-Schluessel fuer die
Suche nach `Member.OauthID`. Weitere nicht reservierte Profilclaims werden der lokalen
Application-Session beigefuegt. Der Host verwirft lokale User-ID-, User-Name-, Role- und
Security-Stamp-Claimtypen sowie `permission`, `permissions`, `clubgear.system-role`, `role` und
`roles`. Ein IAM-Plugin kann damit keine lokalen ClubGear-Berechtigungen durch direkte
Claim-Injection vergeben; Rollen und Permissions werden weiterhin lokal ueber ASP.NET Identity
und ClubGear verwaltet. Das gemappte `sub` entscheidet jedoch, welches bereits verknuepfte lokale
Konto samt dessen lokalen Rollen gefunden wird, und bleibt daher eine sicherheitskritische Trust
Boundary. Ein fehlender,
nicht instanziierbarer oder fehlerhafter Mapper sowie ein `null`-Ergebnis brechen die Anmeldung
fail-closed ab.

Eine erfolgreiche Anmeldung setzt voraus:

1. Ein aktives Identity-Provider-Plugin deklariert den Provider.
2. Der Provider ist vollstaendig konfiguriert und unter `/Admin/ExternalLogin` aktiviert.
3. `Member.OauthID` entspricht dem rohen oder vom Plugin gemappten `sub`-Wert.
4. Das Mitglied ist ueber `Member.ApplicationUserId` mit einem vorhandenen `ApplicationUser`
   verbunden.
5. Der lokale `ApplicationUser` darf sich anmelden, ist nicht gesperrt und hat keine lokale
   Zwei-Faktor-Anmeldung aktiviert. Fuer den lokalen 2FA-Fortsetzungsschritt existiert im
   OIDC-Callback noch kein Workflow; ClubGear lehnt diesen Login daher ab statt 2FA zu umgehen.

ClubGear legt dabei weder automatisch Mitglieder noch ApplicationUser an. Ohne beide
Verknuepfungen zeigt der Callback den Status `NoLinkedMember` und erzeugt keine lokale Session.
Das temporaere externe Cookie wird nach erfolgreichem Lesen vor Mapping und lokaler Anmeldung
entfernt; behandelte Fehlerpfade versuchen ebenfalls eine Bereinigung. Ein Fehler beim
Cookie-Loeschen beendet die Anmeldung fail-closed.

### Praktischer Verknuepfungsablauf

1. Den externen Login einmal ausfuehren. Die `NoLinkedMember`-Seite zeigt den effektiven,
   gegebenenfalls gehashten Link-Key.
2. Diesen Wert beim betroffenen Mitglied als OAuth ID (`Member.OauthID`) hinterlegen.
3. Falls noch keine lokale Verknuepfung existiert, das lokale Konto mit derselben eindeutigen
   E-Mail per Passwort anmelden und Selfservice oeffnen. Der bestehende Selfservice-Fallback
   setzt dann `Member.ApplicationUserId`. Bei keiner oder mehreren passenden E-Mails gibt es
   derzeit keinen dedizierten Admin-Workflow fuer diese Zuordnung; sie erfordert kontrolliertes
   Datenbank-/Administrations-Tooling oder eine kuenftige UI-Erweiterung.
4. Den externen Login erneut ausfuehren.

## Ablauf

```text
Login-Seite
  -> /Account/ExternalLoginChallenge?provider=<key>
  -> dynamisches Scheme oidc.<key>
  -> externer IdP
  -> /signin-oidc-<key>
  -> temporaere Identity.External-Session
  -> /Account/ExternalLoginCallback
  -> MapClaimsAsync
  -> Member.OauthID + Member.ApplicationUserId pruefen
  -> Identity.Application-Session erzeugen
  -> SelfService oder lokale returnUrl
```

## Abnahmecheckliste fuer ein Provider-Plugin

1. Projekt baut ausschliesslich gegen `ClubGear.Plugin.Contracts`.
2. `plugin.json` wird vom `PluginManifestParser` akzeptiert.
3. Modul- und Providerklasse sind public und parameterlos konstruierbar.
4. Contribution-Key und Interface-Key sind gleich und repositoryweit eindeutig.
5. `authority`, `clientid` und `clientsecret` werden im Schema angeboten.
6. Discovery-Test und Fehlertexte funktionieren fuer falsche Authority sowie fehlende
   Client-Daten. Falsche Client-Credentials werden erst durch einen echten Browser-E2E-Login
   erkannt; der anonyme Discovery-Aufruf authentifiziert den Client nicht.
7. Die exakte `/signin-oidc-<providerKey>`-URI ist beim IdP registriert.
8. Einzel- und Mehrfachclaims werden defensiv verarbeitet.
9. Das gemappte `sub` bleibt stabil, provider-/issuer-spezifisch und maximal 100 Zeichen lang.
10. Ein Testmitglied besitzt sowohl den erwarteten `OauthID` als auch eine gueltige
    `ApplicationUserId`-Verknuepfung.
11. Erfolgs-, unbekannter Benutzer-, deaktivierter Provider- und Mappingfehler-Pfad sind getestet.

## Verbleibende Grenzen

- Doppelte Provider-Keys, ungueltige Keys, nicht instanziierbare Provider und Abweichungen zwischen
  Contribution- und Interface-Key werden als nicht verfuegbar markiert und fail-closed nicht zur
  Anmeldung angeboten. Eine separate Installationsvalidierung mit detailliertem Diagnosebericht
  gibt es noch nicht.
- Schema-Constraints und alle generischen Feldtypen werden in der IAM-Admin-Ansicht noch nicht
  vollstaendig serverseitig ausgewertet.
- OIDC-Scopes, UserInfo, Tokenzugriff, Public Clients und Auto-Provisioning sind nicht Teil des
  aktuellen Contracts.
- Wird ein Provider in einem Multi-Node-Deployment erst nach dem Startup aktiviert, registriert
  der Challenge-Fallback das Scheme nur im aktuellen Prozess. Bis ein koordinierter Runtime-
  Reload existiert, muessen die weiteren Knoten kontrolliert neu gestartet werden.
- Lokale Zwei-Faktor-Konten koennen den OIDC-Login noch nicht fortsetzen und werden abgelehnt.
- `Member.IsActive` und `Member.IsVerified` sind wie beim lokalen Kontologin keine zusaetzlichen
  Authentifizierungs-Gates; die lokale `ApplicationUser`-Policy entscheidet ueber den Sign-in.
- `Member.OauthID` ist ein global eindeutiger String statt eines strukturierten
  `(Provider, Issuer, Subject)`-Schluessels. Das Plugin muss deshalb selbst einen stabilen,
  namespaced Link-Schluessel erzeugen.

## Relevante Core-Dateien

- `Program.cs`
- `Controllers/Account/Account_Controller.cs`
- `Controllers/Admin/ExternalLoginAdmin_Controller.cs`
- `Services/ExternalLogin/ExternalLoginConfigService.cs`
- `Services/ExternalLogin/ExternalLoginService.cs`
- `Services/ExternalLogin/IdpClaimsEnricher.cs`
- `Services/ExternalLogin/OidcOptionsReloader.cs`
- `Contracts/Plugin/IIdentityProviderPlugin.cs`
- `Contracts/Plugin/PluginExternalLoginContracts.cs`
- `Contracts/Plugin/PluginContributions.cs`
- `plugins/IamOidc/README.md`
- `plugins/IamOidc/src/`
