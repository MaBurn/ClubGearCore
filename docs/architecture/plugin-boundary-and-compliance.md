# Plugin Boundary And Compliance

Audience: Entwickler, Plugin-Autoren, Architekten, Compliance-Verantwortliche
Scope: Plugin-Vertrag, erlaubte Schnittstellen, verbotene Kernzugriffe, Lizenzgrenzen
Last-Validated: 2026-06-07
Source-Commit: ae195e7
Related-Diagrams: diagrams/img/ctr-plugin-boundary.png

## Purpose

Dieses Dokument definiert die technische und lizenzrechtliche Grenze zwischen
dem ClubGear-Core und Drittanbieter-Plugins. Die zentrale Regel lautet:
Plugins duerfen nur ueber Contracts und Facades mit dem Core interagieren.

---

## Plugin-Boundary

![Plugin Boundary](diagrams/img/ctr-plugin-boundary.png)

### Erlaubte Pfade

| Plugin-Zugriff | Erlaubt? | Bemerkung |
|---|---|---|
| `ClubGear.Plugin.Contracts` referenzieren | Ja | Einzige verpflichtende direkte Abhaengigkeit |
| `IPluginRuntimeBridge.HasPermissionAsync()` | Ja | Berechtigungen nur ueber Bridge |
| `IPluginRuntimeBridge.LogAsync()` | Ja | Audit nur ueber Facade |
| `IPluginRuntimeBridge.NotifyAsync()` | Ja | Benachrichtigungen nur ueber Facade |
| `IPluginModule` implementieren | Ja | Einstiegspunkt fuer Plugin-Manifest |
| Route per `PluginEndpointRegistrar.RegisterGet()` registrieren | Ja | Nur mit isoliertem Handler |
| Member-Slots und Actions registrieren | Ja | Rendering bleibt im Host, Inhalt kommt aus Plugin-Contributions |
| Admin-Function-Panels registrieren | Ja | Host stellt generische Panel- und Command-UI bereit |

### Verbotene Pfade

| Plugin-Zugriff | Erlaubt? | Warum |
|---|---|---|
| `ClubGear.Services` direkt referenzieren | Nein | Bricht Contract-First-Architektur |
| `ClubGear.Data` direkt referenzieren | Nein | Umgeht Core-Datenzugriff |
| `ClubGear.Controllers` direkt referenzieren | Nein | Kopplung an interne UI-/API-Struktur |
| `ClubGear.Models` direkt referenzieren | Nein | Verhindert kontrollierte Contract-Grenze |
| Direkter Zugriff auf `ApplicationDbContext` | Nein | Kein Core-DB-Zugriff fuer Plugins |
| Direkte Nutzung von Core-Services ohne Facade | Nein | Schutz vor unkontrollierter Kopplung |

---

## Laufzeitverhalten

### Isolation

`PluginRuntimeAdapter.EnsureIsolated()` blockiert Delegates aus verbotenen Namensraeumen.
Die aktuelle Liste umfasst:
- `ClubGear.Services`
- `ClubGear.Data`
- `ClubGear.Controllers`
- `ClubGear.Models`

Wenn ein Plugin-Handler diese Regel verletzt, wird eine `UserFriendlyException`
geworfen und der Handler nicht registriert bzw. ausgefuehrt.

### Endpoint-Registrierung

`PluginEndpointRegistrar` speichert alle registrierten Routen modular nach
`ModuleId:RoutePattern`. Vor dem Aufruf wird per `HasPermissionAsync()` geprueft,
ob der aktuelle Benutzer die in der Route hinterlegte Permission besitzt.

### UI-Dispatch und Befehle

Schema-gesteuerte Plugin-Aktionen und Admin-Befehle werden nicht direkt aus Plugins gerendert.
Der Host stellt die UI, Antiforgery-Verarbeitung, HTTP-Mappings und Fehlerdarstellung bereit.
Plugins liefern nur Contribution-Metadaten, Eingabeschemata und Handler.

### Sicherheitskette fuer Installation

`IPluginInstallerService` unterstuetzt zwei Installationspfade:
- Marketplace-Installationen ueber Modul-ID
- ZIP-Installationen mit SHA-256-Hash und RSA-Signatur

`PluginIntegrityVerifier` prueft dabei:
1. Paket nicht leer
2. Erwarteten SHA-256-Hash vorhanden
3. Signatur vorhanden
4. Hash stimmt mit Paket ueberein
5. RSA-Public-Key ist lesbar
6. Signatur ist gueltig

---

## Compliance und Lizenzgrenze

ClubGear-Core bleibt unter AGPLv2. Die Plugin-Schnittstelle ist absichtlich
contract-first, damit Drittanbieter-Plugins proprietaer oder kommerziell lizenziert
werden koennen, solange sie nur die Contracts verwenden.

### Konkrete Regeln

1. Plugins duerfen nur `ClubGear.Plugin.Contracts` als direkte Core-Grenze verwenden.
2. Plugins muessen ihre Logik ueber Bridges und Facades aufrufen, nicht ueber interne Services.
3. Kern-Namensraeume sind fuer Plugin-Handler verboten.
4. Contract-Kompatibilitaet wird vor Aktivierung geprueft (`ContractVersion`, Manifest-Parsing).
5. Plugin-Installationen sind nur gueltig, wenn Integritaet und Signatur erfolgreich verifiziert wurden.

### Dokumentierter Bezugsrahmen

Die Doku [plugin-license-boundary.md](plugin-license-boundary.md) beschreibt die
urspruengliche Lizenz- und Vertragsgrenze; dieses Dokument erweitert sie um die
aktuell implementierten Runtime- und Installationsregeln.

## Open Questions
- Der Runtime-Dispatcher deckt aktuell GET-Routen ab; Erweiterungen fuer weitere HTTP-Methoden sind offen.
- Marketplace-Metadatenquelle und Update-Strategie fuer Plugin-Kataloge sind noch nicht finalisiert.

## References
- [Core Deep-Dive](core-deep-dive.md)
- [UI Flow Map](ui-flow-map.md)
- [Plugin Authoring Guide](plugin-authoring-guide.md)
- [Diagrammquelle Plugin Boundary](diagrams/src/ctr-plugin-boundary.puml)
- [Vorherige Lizenzgrenze](plugin-license-boundary.md)
