# Offene Features und Plugins aus Clubmanager

Stand: 2026-08-27

## Ziel und Bewertungsgrundlage

Diese Liste vergleicht die Controller unter `../Clubmanager/Controllers/Mvc` und
`../Clubmanager/Controllers/Api` mit dem aktuellen ClubGear-Core und den vorhandenen
ClubGear-Plugins. Sie ist eine fachliche Portierungsplanung, keine Forderung nach einem
1:1-Port der alten Controller. Insbesondere sollen wiederverwendbare Plattformfunktionen in
den Core und konkrete Vereins- oder Anbieterlogik in Plugins geschnitten werden.

Berücksichtigt wurden außerdem das Clubmanager-Datenmodell (`Models/ClubManagerContext.cs`),
die ClubGear-Controller, Core-Services, Plugin-Contracts, Plugin-Manifeste und Plugin-READMEs.
Temporäre `*.cs.tmp`-Dateien sowie reine Debug-Controller gelten nicht als Features.

### Aufwandsskala

| Wert | Größenordnung | Bedeutung |
|---|---:|---|
| XS | 1–2 PT | Kleine Ergänzung auf vorhandener Infrastruktur |
| S | 3–5 PT | Begrenztes Feature mit Tests und UI |
| M | 6–12 PT | Mehrere Persistenz-, Service-, UI- und Berechtigungsanteile |
| L | 13–25 PT | Eigene Domäne oder größere Plattformerweiterung |
| XL | 26–50+ PT | Mehrere Workflows/Integrationen, Migration und hohe Testbreite |

Die Schätzung umfasst Implementierung und Tests, aber keine vollständige Migration historischer
Clubmanager-Produktivdaten. Eine belastbare Datenmigration ist je Domäne zusätzlich zu planen.

## Featureübersicht

| Feature | Kurzbeschreibung | Aufwand |
|---|---|---:|
| Organisationsstruktur und Untergliederungen | Hierarchische Vereinseinheiten, Mitgliedschaften und delegierte Administration | L |
| Sichere Datei-/Medienablage | Gemeinsame, berechtigte Ablage für Uploads aus Core und Plugins | M |
| Plugin-Oberflächen und Host-Contracts | Erweiterbare Plugin-Seiten, Formulare, APIs und Dateiaktionen | L |
| Benachrichtigungs-Inbox und Präferenzen | Inbox und E-Mail im Core, weitere Zustellmedien über Provider-Plugins | L |
| Versionierte Templates und Kommunikationsereignisse | Kanalneutrale Vorlagen mit Vorschau, Historie und Rollback | L |
| Workflow-/State-Machine-Grundlage | Wiederverwendbare Zustände, Transitionen und Prozesshistorien | L |
| Kalender und Veranstaltungen | Vereinstermine, regelmäßige Treffen, Ausnahmen und Anmeldungen | L |
| Familien-/Haushaltsbeziehungen | Validierte Beziehungen zwischen Haupt- und Familienmitgliedern | M |
| API-Keys und Service Accounts | Sichere, eingeschränkte Zugänge für Bots und externe Clients | M |
| Branding und Navigation | Logo, Favicon, Theme-Grundwerte und zusammengeführte Navigation | S–M |
| Übersetzungsverwaltung | Änderbare Übersetzungen für Core und Plugin-Namensräume | M |
| Betriebs-, Audit- und Changelog-Ansichten | Durchsuchbare Betriebsprotokolle und Versionsinformationen | S–M |
| Inventar 2.0 | Lager, Bestände, Bilder, Historie, Barcodes und mobile Inventar-API | XL |
| Finanzen 2.0 und Beitragsläufe | Zahlungsabgleich, Beiträge, SEPA und Kassenwart-Arbeitsabläufe | XL |
| Massenmail und Newsletter | Kampagnen, Empfängerlisten, Versand, Bounces und Opt-in/-out | XL |
| Nextcloud-/CalDAV-Kalender-Connector | Synchronisation externer Kalender mit Vereinseinheiten | M |
| Marktplatz und Vermietung | Inserate, Mietaufträge, Kautionen, Übergaben und Schäden | XL |
| Entscheidungsbäume und Sitzungen | Geführte Abläufe, Sitzungsprotokolle, Ergebnisse und Aufgaben | L |
| Redaktion und Publikationsplanung | Einreichungen, Ausgaben und Freigaben für mehrere Medien | XL |
| Dokumentenkompendium | Geschützte Dokumentablage mit Metadaten, Suche und Wasserzeichen | M |
| Nextcloud-Dokument-Connector | WebDAV-Dokumente als austauschbare externe Ablage anbinden | M |
| Brief-/Postal-Provider | Einzel- und Serienbriefe mit PDF-Erzeugung und Versandstatus | L |
| Organigramm-Ansicht | Visuelle Darstellung der Organisationsstruktur | M |
| Interne Nachrichten und Chat | Threads, Nachrichten und optionale externe Chat-Anbindung | L |
| GPS-Tracker und Traccar | Geräteverwaltung und optionale Fahrzeug-/Traccar-Anbindung | L |
| Öffentliche Inhalte | Editierbare öffentliche Seiten mit Medien und Vorschau | M |

## Zusammenfassung und empfohlener Schnitt

| Prio | Baustein | Ziel | Status in ClubGear | Aufwand | Wichtigste Abhängigkeiten |
|---:|---|---|---|---:|---|
| P0 | Organisationsstruktur und Untergliederungen | **Core** | fehlt | L | Mitglieder, Rollen/Rechte, Audit |
| P0 | Generische Datei-/Medienablage | **Core** | fehlt | M | Storage-Konfiguration, Berechtigungen, Viren-/Typprüfung |
| P0 | Plugin-UI und Plugin-API ausbauen | **Core** | teilweise | L | Plugin-Contracts, Berechtigungen, Navigation |
| P0 | Benachrichtigungs-Inbox und Präferenzen | **Core + Provider-Contract** | Versandbasis vorhanden | L | Benutzerbezug, Templates, Plugin-Fassade, Secret-Schutz |
| P0 | Template-/Kommunikationsgrundlage | **Core** | einfacher Renderer vorhanden | L | NotificationService, Audit, Dateiablage |
| P1 | Kalender und Veranstaltungen | Core + Plugin | fehlt | L + M | Organisation, Benachrichtigungen; Nextcloud optional |
| P1 | Inventar 2.0 | **Plugin** `Inventar` | Basis vorhanden | XL | Plugin-Plattform, Dateiablage, Organisation |
| P1 | Finanzen 2.0 | **Plugin** `Finance` | Teilmenge vorhanden | XL | Mitglieder, Templates, Dateiablage, Audit |
| P1 | Workflows/State Machine | **Core** | fehlt | L | Rechte, Audit, Background Jobs |
| P1 | Familien-/Haushaltsbeziehungen | **Core** | Hierarchie teilweise vorhanden | M | Mitglieder, Mitgliedstypen, Self-Service |
| P1 | Massenmail/Newsletter | **Plugin** | fehlt | XL | Template-Core, Benachrichtigungen, Dateiablage |
| P2 | Marktplatz und Vermietung | **Plugin** | fehlt | XL | Inventar 2.0, Workflow-Core, Finance-Schnittstelle |
| P2 | Organisation/Organigramm-Ansicht | Core-Ansicht | fehlt | M | Organisationsstruktur |
| P2 | Entscheidungsbäume und Sitzungen | **Plugin** | fehlt | L | Dateiablage, Benachrichtigungen |
| P2 | Redaktion und Publikationsplanung | **Plugin** | fehlt | XL | Templates, Dateiablage, Nextcloud optional |
| P2 | Dokumentenkompendium | **Plugin** | fehlt | M | Dateiablage, optional Dokumentverarbeitung |
| P2 | Briefversand | **Plugin** | fehlt | L | Templates, Mitglieder/Adressen, Provider |
| P2 | API-Key-/Service-Account-Verwaltung | **Core** | fehlt | M | Rechte, Audit, Secret-Schutz |
| P3 | Interne Nachrichten/Chat | **Plugin** | fehlt | L | Benachrichtigungen, Echtzeit optional |
| P3 | GPS-Tracker | **Plugin** | fehlt | L | CarInfo optional, Traccar-Connector, Dateiablage |
| P3 | CMS/öffentliche Inhaltsseiten | **Plugin** | statische Core-Seiten vorhanden | M | Dateiablage, Templates |
| P3 | Übersetzungsverwaltung | Core-Erweiterung | fehlt | M | Caching, Lokalisierungskonzept |
| P3 | Changelog-/Log-Viewer | Core-Admin | Audit vorhanden | S–M | Audit/EventLog, sichere Dateizugriffe |

## Core-Backlog

### CORE-01 – Organisationsstruktur und Untergliederungen (P0, L)

**Quelle:** `LocalUnitController`, `LocalUnitAdminController`, Teile von
`AdminController` (Club Roles) und `OrganigrammApiController`.

**Umfang:** Hierarchische Organisationseinheiten, Mitgliedschaften und Ansprechpartner,
zuständige Administratoren, Ein-/Austritt im Self-Service, rollenbasierte Zuständigkeiten und
feingranulare Rechte pro Einheit. Das Clubmanager-Feature „Referate“ sollte nicht als feste
Controller-Sammlung übernommen werden, sondern als konfigurierbare Organisationseinheit bzw.
Plugin-Navigation auf dieser Grundlage.

**Warum Core:** Fast alle späteren Fachmodule müssen Daten, Rechte, Veranstaltungen oder
Inventar einer Gliederung zuordnen. Ein Plugin hierfür würde zentrale Identitäten und
Berechtigungsentscheidungen duplizieren.

**Abhängigkeiten:** bestehende Mitglieder-, Rollen-/Permission- und Audit-Services. Blockiert
Kalender, Organigramm, lagerbezogene Rechte, Redaktion und rollenbasierte Benachrichtigungen.

### CORE-02 – Sichere Datei-/Medienablage (P0, M)

**Quelle:** Uploads in `AdminContentController`, `MembersController`, `InventoryController`,
`MobileInventoryController`, `GPSTrackerController`, `EditorialApiController`,
`KompendiumApiController`, `TreffenApiController`, `MassMailController` und
`MarketplaceRentalController`.

**Umfang:** Generischer Storage-Service mit Metadaten, Größen- und MIME-Limits, sicherer
Dateinamensbildung, Autorisierungsprüfung, Download/Inline-Policy, Löschung, optionalem
Thumbnailing und austauschbarem lokalem/Object-Storage-Backend. Plugins speichern nur
fachliche Referenzen, nicht beliebige Pfade unter `wwwroot`.

**Warum Core:** Nahezu jedes größere Plugin benötigt Uploads. Sicherheitsregeln und
Lebenszyklus dürfen nicht je Plugin neu implementiert werden.

**Abhängigkeiten:** SystemConfig, Audit, Plugin-Host-Kontext; optional Malware-Scanner.

### CORE-03 – Plugin-Oberflächen und Host-Contracts erweitern (P0, L)

**Quelle:** Die großen Clubmanager-Controller liefern eigene MVC- und REST-Oberflächen;
ClubGear bietet aktuell vor allem `page.generic`, Member-/Self-Service-Slots,
`admin.functions`, Navigation und Commands.

**Umfang:** Formular-/Tabellen-/Filter-/Pagination-Komponenten für Plugin-Seiten, validierte
Datei-Uploads, typisierte Action-Argumente, Download-/Stream-Antworten, Plugin-eigene API-Routen
oder ein ausreichend reiches deklaratives API, konsistente Anti-CSRF-/Auth-Regeln und
standardisierte Fehler-/Erfolgsmeldungen. Mobile Clients brauchen stabile JSON-Endpunkte statt
HTML-Renderer.

**Warum Core:** Das ist Plattformfähigkeit, keine Fachlogik. Ohne sie würden Inventar,
Finanzen, Newsletter oder Redaktion Core-Sonderfälle benötigen.

**Abhängigkeiten:** Plugin-Contract-Versionierung, Runtime, Permission-Fassade, Dateiablage.
**Hinweis:** Vor Umsetzung jedes Fachplugins dessen kleinsten notwendigen Contract-Zuwachs
festlegen; keine plugin-spezifischen Schlüssel oder DTOs in den Core aufnehmen.

### CORE-04 – Benachrichtigungs-Inbox, Status und Präferenzen (P0, L)

**Quelle:** `NotificationController`, Benachrichtigungsanteile in `UserController`,
`AdminController`, `MessagingController`, `EditorialApiController` und
`InventoryController`.

**Umfang:** Benutzerbezogene Inbox, ungelesen-Zähler, gelesen/alle gelesen, Löschen,
Aktionslink, Kanalpräferenzen und Ereignistypen. Plugin-Ereignisse werden über die vorhandene
Notification-Fassade erzeugt. E-Mail ist der standardmäßig mitgelieferte externe Kanal. Matrix,
WhatsApp, Telegram, SMS, E-Post und weitere Medien werden ausschließlich als austauschbare
Channel-Provider-Plugins registriert; der Core enthält keine providerspezifische Logik.

**Warum Core:** Systemweite Zustellung, Empfängerauflösung und Präferenzen werden von vielen
Plugins benötigt. ClubGear besitzt bereits `NotificationService` und Persistenz, aber noch
keine vollständige Benutzer-Inbox wie Clubmanager.

**Abhängigkeiten:** eindeutige Zuordnung ApplicationUser ↔ Member, Permissions, Background Jobs.

### CORE-05 – Versionierte Templates und Kommunikationsereignisse (P0, L)

**Quelle:** `AdminTemplateController`, `AdminTemplateManagerController`,
`AdminTemplatePortalController`, `TemplatesV2Controller`, `TemplateMappingsV2Controller` sowie
Template-Anteile in `MembersController`, `MassMailController` und `PostalController`.

**Umfang:** Kanalneutrale Templates, freigegebene Placeholder-Schemas, Vorschau/Validierung,
Versionen, Historie/Rollback, Haupt-/Layout-Templates und Mapping von semantischen
Ereignisschlüsseln auf Templates. Plugins registrieren Ereignisse, Placeholder und
Default-Templates über Contracts; sie kennen keine Core-Template-IDs.

**Warum Core:** Rendering und Ereignis-zu-Template-Auflösung sind wiederverwendbare
Infrastruktur. Kampagnen, Listmonk-Synchronisation oder Briefprovider bleiben Plugins.

**Abhängigkeiten:** bestehender `IMessageComposer`/`ITemplateRenderer`, Audit,
Benachrichtigungen; CORE-03 für Plugin-Contributions.

### CORE-06 – Generische Workflow-/State-Machine-Grundlage (P1, L)

**Quelle:** `AdminWorkflowController`, `WorkflowSeedController` und die Zustandslogik in
`MarketplaceRentalController`.

**Umfang:** Definition von Zuständen und erlaubten Transitionen, Berechtigungen pro Transition,
Instanzen, unveränderliche Historie, idempotente Commands und optionale Background-Aktionen.
Definitions- und Instanzdaten müssen mandanten-/pluginbezogen isoliert sein. Keine fest
eingebauten „Rental“-/„Purchase“-Workflows im Core.

**Warum Core:** Vermietung, Beitrittsprüfung, Freigaben und redaktionelle Prozesse benötigen
dasselbe technische Muster. Die konkrete Workflow-Definition gehört dem jeweiligen Plugin.

**Abhängigkeiten:** Audit, Permissions, Plugin-Migrationen, Background Jobs,
Benachrichtigungen.

### CORE-07 – Kalender-/Event-Grundmodell (P1, L)

**Quelle:** `LocalUnitController`, `CalendarController`, Teile von `AdminCalendarController`.

**Umfang:** Veranstaltungen, wiederkehrende Treffen, Ausnahmen, An-/Abmeldung,
Teilnehmergrenzen und Zuordnung zu Organisationseinheiten. Import-/Exportports (nicht
Nextcloud-spezifisch) ermöglichen Connector-Plugins.

**Warum Core:** Vereinstermine und Teilnahmen sind eine zentrale Domäne mit direktem Bezug zu
Mitgliedern und Einheiten. CalDAV/Nextcloud-Konfiguration und Synchronisation bleiben Plugin.

**Abhängigkeiten:** CORE-01, CORE-04; später CALDAV-01.

### CORE-08 – Familien-/Haushaltsbeziehungen (P1, M)

**Quelle:** Familienaktionen in `MembersController` und `MembershipTypeController`.

**Umfang:** Explizite, validierte Beziehungen zwischen Haupt- und Familienmitgliedern,
Anzeige als Hierarchie, konsistente Mitgliedstypregeln, Schutz vor Zyklen und definierte
Auswirkungen auf Beiträge/Kommunikation. ClubGear hat bereits Mitgliedshierarchie; offen sind
die Clubmanager-Parität und fachliche Regeln.

**Warum Core:** Die Beziehung beeinflusst Kern-Mitgliedschaft, Self-Service und mehrere
Plugins. Beitragsberechnung bleibt im Finance-Plugin.

**Abhängigkeiten:** Mitglieder, Mitgliedstypen, Finance-Verbraucherschnittstelle.

### CORE-09 – API-Keys und Service Accounts (P2, M)

**Quelle:** `ApiKeyAdminController`, `APIController`; Clubmanager nennt Matrix-Bots als Beispiel.

**Umfang:** Einmalige Secret-Anzeige, nur gehashte Speicherung, Scopes/Permissions,
Ablaufdatum, Rotation, Sperrung, letzte Nutzung, Audit und Rate Limits. Bevorzugt als echte
Service-Identity statt als universeller Schlüssel.

**Warum Core:** Authentifizierung und Autorisierung externer Clients sind eine
Sicherheitsgrenze des Hosts. Fach-APIs legen lediglich benötigte Scopes fest.

**Abhängigkeiten:** Permissions, Audit, Auth-Middleware, CORE-03 für Plugin-API-Scopebeiträge.

### CORE-10 – Branding und Navigation (P2, S–M)

**Quelle:** Branding-, Menü- und Dynamic-Navigation-Aktionen in `AdminController`.

**Umfang:** Logo/Favicon, wenige Theme-Tokens und geordnete Navigation. Plugin-Navigation muss
weiter aus Manifest/Contributions stammen; der Core darf keine Route eines Plugins erraten.

**Warum Core:** Branding und das Zusammenführen aller Navigationseinträge sind Host-Aufgaben.

**Abhängigkeiten:** CORE-02 für Medien, bestehender `PluginNavEntryService`.

### CORE-11 – Übersetzungsverwaltung (P3, M)

**Quelle:** `AdminTranslationController`.

**Umfang:** Übersetzungskatalog nach Kontext/Schlüssel/Sprache, Fallback, Cache-Invalidierung,
Import/Export und Plugin-Namensräume. Die hartcodierte Clubmanager-Liste `de/en/fr` nicht
übernehmen.

**Warum Core:** Navigation, Core und Plugins brauchen ein einheitliches Lokalisierungssystem.

**Abhängigkeiten:** Plugin-Contributions, Admin-UI, Caching.

### CORE-12 – Betriebs-, Audit- und Changelog-Ansichten (P3, S–M)

**Quelle:** `LogController`, `ChangelogController`, `MaintenanceController`, Debug-Controller.

**Umfang:** Paginierte Audit-/Eventlog-Suche, Detailansicht, CSV-Export mit Berechtigung und
Aufbewahrungsregeln; Anzeige des installierten Core-/Plugin-Changelogs. Maintenance Mode ist in
ClubGear bereits vorhanden und braucht höchstens UI-Politur. Token-/Claims-Debugseiten nur in
Development, nicht als Produktfeature portieren.

**Warum Core:** Betriebsdaten und installierte Versionen gehören zum Host.

**Abhängigkeiten:** bestehende Audit-/Eventlog-Services und PluginRegistry.

## Plugin-Backlog

### INV-01 – Inventar 2.0 (P1, XL)

**Quelle:** `InventoryController`, `MobileInventoryController`,
`AdminInventoryReferenceCatalogController`, teilweise `ToolRentalController`.

**Vorhanden:** Das Plugin `clubgear.plugin.inventory` listet derzeit Inventarnummer,
Beschreibung, Kategorie, Status und Standort und bietet Basis-CRUD.

**Offen gegenüber Clubmanager:** Lager und hierarchische Lagerorte, Bestände und
Bestandskorrektur, Kategorien, Bilder, Historie, Barcode/Referenznummern, CSV-Referenzkatalog,
lagerbezogene Rechte, mobile JSON-API, Suche/Filter/Pagination sowie Ausleihe. Das alte
„ToolRental“-Feature sollte als vereinfachte Inventar-Ausleihe aufgehen, nicht als zweites
Plugin dupliziert werden.

**Abhängigkeiten:** CORE-01/02/03/04; optionale Verknüpfung zum Rental-Plugin. Persistenz bleibt
vollständig im Plugin-Schema.

### FIN-01 – Finanzen 2.0 und Beitragsläufe (P1, XL)

**Quelle:** `FinanceController`, Finanz-/Bankaktionen in `MembersController` und
`UserController`.

**Vorhanden:** `clubgear.plugin.finance` unterstützt versionierte Bankdaten,
SEPA-Mandatsreferenzen, Audit, Konten/Buchungen, Beitragsforderungen und pain.008-Export in
unterschiedlichem Reifegrad.

**Offen/zu verifizieren:** Zahlungsdatei-Import, automatische und manuelle Zuordnung zu
Mitgliedern, Unmatch/Re-Match, CSV-Export, Beitragslauf mit Familien-/Mitgliedstypregeln,
Zahlungsarten, belastbare SEPA-Mandatskommunikation und Kassenwart-UI. Vor einer Portierung
aktionsweise gegen Plugin-Tests und `nextSteps.md` abgleichen, um bereits vorhandene Funktionen
nicht neu zu bauen.

**Abhängigkeiten:** CORE-05/08, Audit, Notification-Fassade; für spätere Mietzahlungen eine
schmale, generische Payment-/Booking-Integration statt direktem Tabellenzugriff.

### NEWS-01 – Massenmail und Newsletter (P1, XL)

**Quelle:** `MassMailController`, `NewsletterTemplateApiController`, Newsletter-/Listmonk- und
E-Mail-Sync-Aktionen in `AdminController`, `MembersController` und `UserController`.

**Umfang:** Empfängerselektion, Vorschau, geplante/batchweise Zustellung, Anhänge,
Versandstatus, Bounce-Verarbeitung, ungültige Adressen, Opt-in/-out und Listenabgleich.
Listmonk ist ein optionaler Provider-Adapter; SMTP/IMAP laufen über Core-Kanäle. Die
Clubmanager-Sonderfunktion „Brief an ungültige E-Mail-Adressen“ nutzt POST-01 statt eigenen
Providercode.

**Abhängigkeiten:** CORE-02/04/05, Mitglieder/Organisation, Background Jobs, IMAP. Hoher
Datenschutz- und Zustellbarkeits-Testbedarf.

### CALDAV-01 – Nextcloud-/CalDAV-Kalender-Connector (P1, M)

**Quelle:** `AdminCalendarController`, Synchronisationsanteile in `CalendarController` und
`LocalUnitController`.

**Umfang:** Verbindungstest, Kalender-Discovery, Mapping zu Organisationseinheiten,
inkrementelle Synchronisation, Konflikt-/Zeitzonenregeln und manuelles Resync. Der Connector
implementiert die Ports aus CORE-07; Credentials liegen geschützt in Plugin-Konfiguration.

**Abhängigkeiten:** CORE-01/07, Background Jobs, Secret-Schutz.

### RENT-01 – Marktplatz und Vermietung (P2, XL)

**Quelle:** `MarketplaceRentalController`, Rental-Anteile von `InventoryController` und
`MobileInventoryController`, `WorkflowSeedController`, `MessagingController`.

**Umfang:** Inserate und Verfügbarkeit, Mietanfragen/-aufträge, Eigentümer/Mieter-Rollen,
Kaution und Zahlungen, Haftungshinweis, Abwesenheiten, Übergaben, Fotos, Schäden,
Abzüge/Erstattung, Zustandsverlauf und auftragsbezogene Kommunikation.

**Schnitt:** Eigenes Plugin auf Inventar-Referenzen; der Workflow wird als Plugin-Definition in
CORE-06 registriert. Zahlungen nur über ein Finance-Contract/Event, niemals über dessen
Tabellen. Allgemeiner Chat kann später ersetzt werden; zunächst reicht ein Auftrags-Thread.

**Abhängigkeiten:** INV-01, CORE-02/04/06; optional FIN-01. Wegen Geld, Haftung und
Zustandsübergängen hohe Testtiefe.

### TREE-01 – Entscheidungsbäume, technische Hilfe und Treffen (P2, L)

**Quelle:** `DecisionTreeApiController`, `TechHelpApiController`, `TreffenApiController`.

**Umfang:** Baum-/Knoten-Editor, Aktivierungsvalidierung, geführte Sessions, Antworten,
Ergebnisse, Todos und Anhänge. „TechHelp“ und „Treffen“ sind Konfigurationen/Capabilities
desselben generischen Plugins, keine getrennten Implementierungen. Rollen wie Mitglied und
Koordinator werden Plugin-Permissions.

**Abhängigkeiten:** CORE-02/03/04; CORE-01 nur für Zuordnung zu Gremien/Einheiten.

### EDIT-01 – Redaktion und Publikationsplanung (P2, XL)

**Quelle:** `EditorialApiController`, `NewsletterTemplateApiController`, Referate-Routen und
Teile von `NextcloudDocumentApiController`.

**Umfang:** Medien, Kategorien, Einreichungen, Bilder, Ausgaben, Sortierung, Zuordnung,
redaktionelle Rollen/Freigaben und Benachrichtigungen. „BuKu“ und „Newsletter“ werden als
konfigurierte Medieninstanzen modelliert, nicht als fest codierte Permission-Präfixe.

**Abhängigkeiten:** CORE-01/02/04/05, optional CORE-06; NC-DOC-01 und NEWS-01 als optionale
Integrationen.

### DOC-01 – Dokumentenkompendium (P2, M)

**Quelle:** `KompendiumApiController` und Kompendium-Route in `ReferateController`.

**Umfang:** Dokument-Upload, Kategorien/Hersteller, Metadaten, Suche/Filter, geschütztes
PDF-Viewing, Wasserzeichen und CRUD. Dokumentbytes liegen über CORE-02, Metadaten im
Plugin-Schema.

**Abhängigkeiten:** CORE-02/03; optional Dokumentkonvertierung/Preview als separater Provider.

### NC-DOC-01 – Nextcloud-Dokument-Connector (P2, M)

**Quelle:** `NextcloudDocumentApiController`, WebDAV-Konfiguration in `AdminController` und
Verwendungen in Redaktion/Mitgliederverwaltung.

**Umfang:** WebDAV/Nextcloud-Verbindung, Pfad-Policy, Metadaten-Sync, Stream/Download und
fachneutrale Dokumentreferenzen. Zugangsdaten verschlüsselt speichern. Redaktion und andere
Plugins konsumieren einen Dokumentprovider-Contract; sie enthalten keinen Nextcloud-Code.

**Abhängigkeiten:** CORE-02/03, Secret-Schutz und Background Jobs.

### POST-01 – Brief-/Postal-Provider (P2, L)

**Quelle:** `PostalController`, `APIController`, postalische Aktionen in `MembersController`
und `MassMailController`.

**Umfang:** Einzel-/Batchbrief, PDF-Erzeugung aus Core-Templates, Portoauswahl, Providerstatus,
Historie und Testmodus. Konkreter Versanddienst und dessen API sind austauschbare
Provider-Implementierungen.

**Abhängigkeiten:** CORE-02/05/09, Mitgliederadressen, Background Jobs.

### ORGVIEW-01 – Organigramm-Ansicht (P2, M)

**Quelle:** `OrganigrammController`, `OrganigrammApiController`.

**Umfang:** Visualisierung und Reihenfolge der CORE-01-Struktur, optionale manuelle
Präsentationsknoten sowie Vorschau. CRUD der eigentlichen Organisation bleibt im Core; eine
aufwendige alternative Darstellung kann als Plugin-Seite geliefert werden.

**Abhängigkeiten:** CORE-01/03. Keine direkte Keycloak-Gruppenverwaltung übernehmen; externe
Gruppenabbildung gehört zum IAM-Plugin.

### CHAT-01 – Interne Nachrichten und Chat-Integration (P3, L)

**Quelle:** `MessagingController`, `ChatController` und Thread-Nutzung der Vermietung.

**Umfang:** Threads, Teilnehmer, Nachrichten, ungelesen-Zähler, Löschen/Moderation sowie
optional ein Matrix-/externer Chat-Adapter. Erst den konkreten Bedarf klären: Für reine
Systemkommunikation genügt CORE-04, für Vermietung ein domäneneigener Thread in RENT-01.

**Abhängigkeiten:** CORE-04/09; Echtzeit optional via SignalR. Deshalb bewusst niedriger
priorisiert.

### GPS-01 – GPS-Tracker und Traccar (P3, L)

**Quelle:** `GPSTrackerController` und Clubmanager-Plugin `plugins/traccar`.

**Umfang:** Trackertypen, Gerätezuordnung, Initialisierungskommandos, Anleitungen und
Traccar-Provisionierung. Geräte können optional per stabiler Fahrzeugreferenz mit CarInfo
verbunden werden; keine Tabellenkopplung.

**Abhängigkeiten:** CORE-02/03, CarInfo optional, externer Traccar-Adapter und Secret-Schutz.

### CMS-01 – Öffentliche Inhalte (P3, M)

**Quelle:** `AdminContentController`, `PublicController`, statische Seiten in `HomeController`.

**Umfang:** Editierbare Inhaltsseiten und Bilder mit klarer Publish-/Preview-Regel. Anmeldung,
Impressum und Datenschutz-Routing bleiben Core; frei editierbare Inhalte sind optionales CMS.

**Abhängigkeiten:** CORE-02/05/10. Datenschutzannahme als eigener Core-Self-Service-Schritt
bewerten, nicht in das CMS verschieben.

## Bereits abgedeckt oder nur gezielt zu ergänzen

| Clubmanager-Bereich | ClubGear-Entsprechung | Bewertung |
|---|---|---|
| Login/Logout/Registrierung | Account-Core + `IamOidc` | Kein Port des Keycloak-spezifischen Controllers; IAM-Plugin gezielt um Provisioning/Linking erweitern. |
| Mitglieder-CRUD, Suche, Import, Verify, Löschen | Member-Core | Basis vorhanden; einzelne Clubmanager-Felder und Bulk-Aktionen nur nach fachlichem Bedarf ergänzen. |
| Mitgliedstypen | Core | Bereits vorhanden und flexibler durch dynamische Felder. |
| Rollen und Rechte | Core | Bereits vorhanden; CORE-01 ergänzt einheitenbezogene Zuständigkeit. Keine Keycloak-Gruppen als lokale Autoritätsquelle. |
| Self-Service-Profil und Profilbild | Core | Vorhanden; Datenschutzstatus und E-Mail-Änderungsbestätigung separat prüfen. |
| Bankdaten/SEPA | `Finance` | Nicht zurück in Core verschieben. |
| Fahrzeuge | `CarInfo` | Vorhanden; Clubmanager-Titel-/Fahrzeugfelder gegen tatsächlichen Bedarf prüfen. |
| Serviceheft | `ServiceBook` | Vorhanden, abhängig von CarInfo; API-/Self-Service-Parität über CORE-03 herstellen. |
| Basisinventar | `Inventar` | Vorhanden, Erweiterungen in INV-01. |
| Pluginverwaltung | Core | Installation, Signatur, Aktivierung, Deaktivierung und Entfernung sind bereits vorhanden; Clubmanager-Out-of-process/gRPC nicht portieren. |
| Konfiguration | Core | `ISystemConfigService` vorhanden; Secret-Werte brauchen zusätzlich geschützte Speicherung/Redaction. |
| Audit/EventLog | Core | Services und Plugin-Fassade vorhanden; hauptsächlich Admin-UI/Aufbewahrung offen. |
| Maintenance Mode | Core | Vorhanden; nur Verhalten/UI gegen Clubmanager prüfen. |
| Geburtstagsgrüße | kleines Plugin/Background Job | Kein Kernmerkmal. Nutzt CORE-04/05 und Member-Datumsfelder. Aufwand S–M. |
| Datenschutzannahme | Core | Fachlich zentral; Status, Version der Erklärung und erneute Zustimmung als S–M-Ergänzung prüfen. |
| E-Mail-Änderungsworkflow | Core | Tokenisiert, zeitlich begrenzt, Audit und externe IdP-Grenzen beachten. Aufwand M. |

## Bewusst nicht übernehmen

- `DebugController` und `DebugContactPersonController` als produktive Routen; Diagnostik nur in
  Development bzw. über strukturierte Logs.
- `ReferateController` als Sammlung fest codierter View-Namen. Stattdessen CORE-01 plus
  Plugin-Navigation.
- Clubmanager-spezifische Keycloak-Gruppenverwaltung im Core. ClubGear hält lokale Rollen als
  Autoritätsquelle; externe Gruppenabbildung ist IAM-Plugin-Funktion.
- Parallele alte und neue Template-Controller. Es soll genau ein versioniertes Template-System
  geben.
- `ToolRental` neben Inventar und Marktplatz. Einfache Ausleihe gehört in Inventar; kommerzielle
  Vermietung in RENT-01.
- Direkte Pfad-/Dateizugriffe aus Controllern sowie Secrets in gewöhnlichen Config-Werten.
- Die alte Out-of-process/gRPC-Pluginarchitektur. ClubGear entwickelt seinen bestehenden
  contract-basierten In-process-Host weiter.
- Controller mit tausenden Zeilen als Portierungseinheit. Jede Domäne erhält Services,
  Commands/Queries, schlanke Endpunkte und eigene Tests.

## Abhängigkeitsreihenfolge

1. **Plattformfundament:** CORE-01, CORE-02 und CORE-03.
2. **Kommunikationsfundament:** CORE-04 und CORE-05; parallel CORE-08.
3. **Generische Abläufe:** CORE-06 und CORE-07.
4. **Bestehende Plugins vervollständigen:** INV-01 und FIN-01; CarInfo/ServiceBook nur gezielt
   um fehlende Host-Oberflächen ergänzen.
5. **Kommunikationsmodule:** NEWS-01, CALDAV-01, POST-01 und NC-DOC-01.
6. **Große Fachmodule:** RENT-01 und EDIT-01.
7. **Unabhängigere optionale Module:** TREE-01, DOC-01, CHAT-01, GPS-01 und CMS-01.

Der kritische Pfad für die meiste Clubmanager-Parität lautet:

`CORE-03 → CORE-02/04/05 → CORE-06 → INV-01 → RENT-01`

Für organisationsbezogene Funktionen lautet er:

`CORE-01 → CORE-07 → CALDAV-01` sowie `CORE-01 → EDIT-01`.

## Empfohlene nächste Schnitte

1. CORE-03 als Contract-Design-Spike: Anforderungen von Inventar, Finance und einem
   Upload-lastigen Plugin gemeinsam prüfen; Ergebnis als generische Contracts und ADR.
2. CORE-01 mit minimalem Modell `OrganizationUnit`, Mitgliedszuordnung und delegierter
   Administration implementieren; noch keinen Kalender einbauen.
3. CORE-02 als gehärtete Dateiablage implementieren und zuerst im Profilbild sowie einem
   Plugin-Slot verwenden.
4. Die vorhandenen Plugins per Capability-Matrix gegen die zugehörigen Clubmanager-Aktionen
   testen; daraus INV-01 und FIN-01 in kleinere Releases schneiden.
5. Erst danach CORE-05/06 so dimensionieren, dass Newsletter, Redaktion und Vermietung ohne
   fachliche Sonderfälle im Host umsetzbar sind.
