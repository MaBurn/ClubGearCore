# ClubGear Backlog

Stand: 2026-06-09

## Codeanalyse

ClubGear ist aktuell eine ASP.NET-Core-8-MVC-Anwendung mit SQLite, Identity und einem bereits angelegten Plugin-System. Die Plugin-Vertraege liegen in `Contracts/Plugin` und definieren unter anderem `IPluginModule`, Manifestdaten, Member-Slots, Admin-Panels, Runtime-Routen, Background-Jobs und schema-basierte Formulare.

Bereits vorhanden:
- Plugin-Lifecycle fuer Installation, Aktivierung, Deaktivierung und Laden aus ZIP/Marketplace.
- Signatur- und Hash-Pruefung fuer Plugin-Pakete.
- Contract-Kompatibilitaetspruefung.
- Plugin-Migrationen und plugin-eigene Persistence ueber tabellenpraefixierte SQL-Zugriffe.
- Runtime-Isolation gegen direkte Core-Abhaengigkeiten.
- Member-Erweiterungspunkte fuer Detailkarten, Edit-Tabs, Status-Badges und Aktionen.
- Generische Admin-Function-Panels mit schema-basierten Commands.
- Facades fuer Berechtigungen, Audit und Notifications.
- Beispielplugin `CarInfo` fuer Fahrzeuge am Mitglied.
- Umfangreiche Architektur- und Regressionstests im `tests/ClubGear.ArchitectureTests`-Projekt.

Erkennbare Luecken fuer das Zielbild Vereinsverwaltung mit Plugin-Erweiterbarkeit:
- Die drei Plugin-Arten sind fachlich noch nicht klar im Modell abgebildet: Mitglieder-Profil, generelle Erweiterung, technische Plugins.
- Member-Profil-Plugins koennen Daten darstellen und Aktionen ausfuehren, aber ein einheitliches Konzept fuer erweiterte Mitgliedseigenschaften, Suche, Import/Export und Self-Service-Freigabe fehlt noch.
- Generelle Erweiterungen haben Runtime-Routen und Admin-Panels, aber noch keine sichtbare Navigation, keine Dashboard-/Menue-Integration und keine standardisierte List-/Detail-UI.
- Technische Plugins koennen ueber Facades arbeiten, aber es fehlen explizite Provider-Vertraege fuer Notification-Kanaele, Identity Provider, Audit-Sinks und Background-Jobs.
- Plugin-Berechtigungen sind vorhanden, brauchen aber bessere Verwaltung, Sichtbarkeit und Dokumentation fuer Vereinsadmins.

## Epic 1: Plugin-Taxonomie und Manifest

### CG-BL-001: Plugin-Kategorien im Manifest einfuehren

**Ziel:** Plugins deklarieren eindeutig, ob sie Mitgliedsprofil-Erweiterungen, generelle Erweiterungen oder technische Plugins sind.

**Akzeptanzkriterien:**
- `PluginManifest` unterstuetzt eine Kategorie, z.B. `member-profile`, `general-extension`, `technical`.
- `plugin.json` kann diese Kategorie setzen.
- Parser validiert unbekannte Kategorien mit verstaendlicher Fehlermeldung.
- Admin-UI zeigt Kategorie pro installiertem Plugin.
- Tests decken Manifest-Parsing, Legacy-Kompatibilitaet und Anzeige ab.

### CG-BL-002: Extension-Points katalogisieren und validieren

**Ziel:** Plugin-Autoren sehen klar, welche Eingriffspunkte erlaubt sind.

**Akzeptanzkriterien:**
- Extension-Points werden als definierte Konstanten oder Enum-ähnliche Contract-Werte beschrieben.
- Manifest-Validation warnt oder blockt unbekannte Extension-Points.
- Dokumentation ordnet jeden Extension-Point einer Plugin-Kategorie zu.
- Beispiel: `member.profile.card`, `member.profile.edit-tab`, `general.navigation`, `technical.notification-channel`.

### CG-BL-003: Plugin-Detailseite fuer Vereinsadmins erweitern

**Ziel:** Admins verstehen Zweck, Risiken und Berechtigungen eines Plugins vor Aktivierung.

**Akzeptanzkriterien:**
- Plugin-Admin zeigt Kategorie, Berechtigungen, Extension-Points, Version, Quelle, Signaturstatus und Migrationsstatus.
- Technische Plugins werden sichtbar als systemnah markiert.
- Bei Aktivierung erscheint eine Zusammenfassung der angeforderten Berechtigungen.

## Epic 2: Mitglieder-Profil-Plugins

### CG-BL-010: Standardisiertes Datenmodell fuer erweiterte Mitgliedseigenschaften

**Ziel:** Plugins koennen strukturierte Zusatzdaten an einem Mitglied speichern, ohne Core-Tabellen direkt zu kennen.

**Akzeptanzkriterien:**
- Contract bietet eine `MemberExtensionData`-Abstraktion oder einen typisierten Persistence-Helper fuer Mitgliedsbezug.
- Daten werden plugin-isoliert gespeichert und eindeutig mit `MemberId` verknuepft.
- CRUD-Operationen pruefen Permissions und Self-Service-Kontext.
- Migrationen koennen dazu passende Tabellen erzeugen.
- Beispiel `CarInfo` nutzt den neuen Helper oder wird als Referenz angepasst.

### CG-BL-011: Mitglieder-Profil: Fahrzeuge als Referenzplugin stabilisieren

**Ziel:** `CarInfo` wird zum belastbaren Beispiel fuer 0-n Datensaetze pro Mitglied.

**Akzeptanzkriterien:**
- Fahrzeuge koennen im Mitgliederprofil hinzugefuegt, bearbeitet und geloescht werden.
- Detailansicht zeigt Fahrzeuge als Plugin-Detailkarte.
- Bearbeitungsansicht bietet ein eigenes Plugin-Tab.
- Self-Service kann je nach Permission Fahrzeuge anzeigen oder pflegen.
- Tests decken Admin- und Self-Service-Flows ab.

### CG-BL-012: Mitglieder-Profil: GPS-Tracker Beispielplugin konzipieren

**Ziel:** Ein zweites Profilplugin zeigt andere Datenformen als Fahrzeuge.

**Akzeptanzkriterien:**
- Backlog/Design beschreibt Felder wie Tracker-ID, Anbieter, Aktivstatus, Ausgabe-/Rueckgabedatum.
- Plugin nutzt dieselben Member-Extension-Kontrakte wie `CarInfo`.
- Detailkarte zeigt Status-Badge fuer aktive Tracker.
- Admin-Aktion kann Tracker einem Mitglied zuweisen oder entziehen.

### CG-BL-013: Plugin-Felder in Mitgliedersuche und Segmenten nutzbar machen

**Ziel:** Vereinsadmins koennen nach plugin-erweiterten Eigenschaften suchen.

**Akzeptanzkriterien:**
- Member-Profil-Plugins koennen filterbare Felder deklarieren.
- Mitgliederliste kann diese Filter dynamisch anzeigen.
- Filterauswertung bleibt plugin-isoliert und sicher gegen freies SQL.
- Mindestens ein Referenzfilter fuer Fahrzeuge oder GPS-Tracker ist implementiert.

### CG-BL-014: Import/Export fuer plugin-erweiterte Mitgliedsdaten

**Ziel:** Erweiterte Mitgliedsdaten gehen bei Datenpflege nicht verloren.

**Akzeptanzkriterien:**
- Export kann Plugin-Daten optional einschliessen.
- Import kann Plugin-Spalten validieren und an das passende Plugin delegieren.
- Fehler werden pro Zeile und Feld angezeigt.
- Import ist permission-geschuetzt.

### CG-BL-015: Self-Service-Freigaben fuer Profilplugins

**Ziel:** Admins steuern, welche Plugin-Daten Mitglieder selbst sehen oder bearbeiten duerfen.

**Akzeptanzkriterien:**
- Pro Plugin/Slot kann Sichtbarkeit fuer Mitglieder konfiguriert werden.
- Schreibrechte sind getrennt von Leserechten.
- Self-Service-UI blendet nicht erlaubte Aktionen aus.
- Tests pruefen mindestens Lesen erlaubt / Schreiben verboten.

## Epic 3: Generelle Erweiterungen

### CG-BL-020: Navigationsbeitrag fuer generelle Plugins

**Ziel:** Plugins wie Inventar oder Vereinszeitung koennen in der Hauptnavigation erscheinen.

**Akzeptanzkriterien:**
- Contract definiert einen Navigation-Contribution-Typ mit Label, Icon, Route, Permission und Sortierung.
- Host rendert sichtbare Navigationseintraege nur bei vorhandener Permission.
- Admin-UI zeigt, welche Menuepunkte ein Plugin bereitstellt.
- Tests pruefen Berechtigungsfilterung und Sortierung.

### CG-BL-021: Generische Plugin-Seiten fuer Listen und Details

**Ziel:** Generelle Plugins koennen nutzbare Oberflaechen bereitstellen, ohne eigene Core-Views zu benoetigen.

**Akzeptanzkriterien:**
- Contract beschreibt Listenansichten mit Spalten, Aktionen, Filterfeldern und Detailansicht.
- Host rendert diese Views konsistent mit Bootstrap-Layout.
- Plugin-Kommandos koennen Create/Edit/Delete-Aktionen ausloesen.
- Fehler und Validierung werden feldbezogen angezeigt.

### CG-BL-022: Inventar-Plugin als Referenz fuer generelle Erweiterungen

**Ziel:** Ein Inventarplugin demonstriert nicht-mitgliedsgebundene Vereinsdaten.

**Akzeptanzkriterien:**
- Inventarobjekte haben Nummer, Name, Kategorie, Status, Lagerort und Verantwortlichen.
- Optional kann ein Objekt einem Mitglied zugeordnet werden.
- Plugin liefert Navigation, Listenansicht, Detailansicht und Admin-Panel.
- Tests decken Installation, Anzeige und CRUD-Kommandos ab.

### CG-BL-023: Vereinszeitung-Plugin als Content-Beispiel

**Ziel:** Ein Content-orientiertes Plugin zeigt Artikel, Ausgaben und Publikationsstatus.

**Akzeptanzkriterien:**
- Plugin verwaltet Ausgaben und Artikel.
- Mitglieder koennen veroeffentlichte Ausgaben im Self-Service lesen.
- Admins koennen Entwuerfe pflegen.
- Plugin zeigt, wie generelle Erweiterungen mit oeffentlichen und geschuetzten Bereichen umgehen.

### CG-BL-024: Dashboard-Widgets fuer generelle Erweiterungen

**Ziel:** Plugins koennen kompakte Uebersichten auf Start- oder Adminseiten beitragen.

**Akzeptanzkriterien:**
- Contract definiert Widget-Contributions mit Titel, Inhaltstyp, Permission und Sortierung.
- Host rendert Widgets in vorhandenen Dashboard-Bereichen.
- Widgets duerfen keine ungeprueften HTML-Inhalte einspeisen.
- Beispiel: Inventar zeigt "Ausgeliehene Gegenstaende".

## Epic 4: Technische Plugins

### CG-BL-030: Notification-Channel-Provider als Plugin-Vertrag

**Ziel:** Notification-Plugins koennen neue Kanaele wie Matrix, Push oder Webhook bereitstellen.

**Akzeptanzkriterien:**
- Contract definiert Provider fuer Versand, Konfigurationstest und Kanal-Metadaten.
- Core-Notification-Service kann Plugin-Kanaele aus der Runtime aufrufen.
- Admin-UI kann Kanal-Konfiguration schema-basiert anzeigen.
- Versandfehler werden auditierbar protokolliert.

### CG-BL-031: Identity-Provider-Plugin-Schnittstelle

**Ziel:** SSO/Identity Provider wie Keycloak koennen als technische Plugins angebunden werden.

**Akzeptanzkriterien:**
- Architekturentscheidung klaert, welche Teile wegen ASP.NET-Core-Auth beim App-Start statisch sein muessen.
- Contract oder Konfigurationsmodell beschreibt Provider-Metadaten, Claims-Mapping und Rollen-Mapping.
- Admins koennen Provider aktivieren/deaktivieren und testen.
- Login-Flow bleibt auch bei fehlerhaftem Provider erreichbar.

### CG-BL-032: Audit-Log-Sink-Plugins

**Ziel:** Audit-Events koennen zusaetzlich an externe Systeme gesendet werden.

**Akzeptanzkriterien:**
- Contract definiert einen Audit-Sink mit Eventmodell und Delivery-Ergebnis.
- Core-Audit-Service verteilt Events an aktive Sinks.
- Fehler eines Audit-Plugins blockieren nicht den Core-Audit-Eintrag.
- Tests pruefen Isolation und Fehlerbehandlung.

### CG-BL-033: Background-Jobs fuer technische Plugins aktivieren

**Ziel:** Bereits definierte BackgroundJob-Contributions werden lauffaehig.

**Akzeptanzkriterien:**
- Runtime startet registrierte Jobs aktiver Plugins.
- Jobs erhalten Host-Kontext, CancellationToken und Permission-Kontext.
- Deaktivieren eines Plugins stoppt dessen Jobs kontrolliert.
- Admin-UI zeigt Jobstatus und letzte Ausfuehrung.

### CG-BL-034: Technische Plugin-Konfiguration zentralisieren

**Ziel:** Systemnahe Plugins bekommen eine sichere Konfigurationsablage.

**Akzeptanzkriterien:**
- Sensitive Werte koennen als Secret markiert und maskiert angezeigt werden.
- Konfiguration wird versioniert oder mindestens geaendert-auditierbar gespeichert.
- Plugins lesen Konfiguration nur ueber Host-Facade.
- Admin-Panels nutzen dieselben Schemafelder wie andere Plugin-Kommandos.

## Epic 5: Sicherheit, Governance und Betrieb

### CG-BL-040: Plugin-Permissions administrierbar machen

**Ziel:** Vereinsadmins koennen Plugin-Berechtigungen Rollen zuweisen.

**Akzeptanzkriterien:**
- Plugin-Permissions werden nach Installation in der Permission-Verwaltung sichtbar.
- Permissions enthalten Modulname, Beschreibung und Risiko-Kategorie.
- Rollen koennen Plugin-Permissions erhalten oder verlieren.
- Entfernte Plugins hinterlassen keine kaputten Rollenansichten.

### CG-BL-041: Aktivierungsprozess mit Risiko-Check

**Ziel:** Aktivierung macht Migrations-, Permission- und Technikrisiken sichtbar.

**Akzeptanzkriterien:**
- Vor Aktivierung werden angeforderte Permissions, Migrationen und technische Extension-Points angezeigt.
- Admin muss risikoreiche technische Plugins bewusst bestaetigen.
- Aktivierungsfehler speichern einen klaren Status und sind in der UI sichtbar.

### CG-BL-042: Plugin-Daten-Lifecycle definieren

**Ziel:** Deaktivieren, Deinstallieren und Datenloeschung sind klar geregelt.

**Akzeptanzkriterien:**
- Deaktivieren laesst Daten erhalten, entfernt aber UI/Jobs/Routen.
- Deinstallieren bietet Wahl zwischen Daten behalten und Daten loeschen.
- Plugin-Migration-State und Datenpraefixe werden dokumentiert.
- Loeschvorgaenge sind auditierbar.

### CG-BL-043: Plugin-Health und Diagnose

**Ziel:** Admins erkennen defekte Plugins schnell.

**Akzeptanzkriterien:**
- Runtime zeigt Ladefehler, letzte Exceptions, Jobstatus und Migrationsstatus.
- Plugin kann optional Health-Check bereitstellen.
- Diagnoseinformationen vermeiden Secrets und personenbezogene Daten.

## Epic 6: Developer Experience und Dokumentation

### CG-BL-050: Plugin-Authoring-Guide nach Plugin-Kategorien strukturieren

**Ziel:** Plugin-Autoren finden passende Beispiele fuer ihre Erweiterungsart.

**Akzeptanzkriterien:**
- Guide enthaelt getrennte Kapitel fuer Mitglieder-Profil, generelle Erweiterung und technische Plugins.
- Jedes Kapitel zeigt Manifest, Permissions, Contributions und typische Tests.
- `CarInfo`, Inventar und Notification dienen als Referenzpfade.

### CG-BL-051: Plugin-Projekt-Template bereitstellen

**Ziel:** Neue Plugins koennen schnell und regelkonform gestartet werden.

**Akzeptanzkriterien:**
- Template erzeugt `plugin.json`, `.csproj`, `IPluginModule` und Beispielprovider.
- Kategorie kann beim Erzeugen ausgewaehlt werden.
- Build erzeugt ein signierbares ZIP-Paket.
- README beschreibt lokale Entwicklung und Installation.

### CG-BL-052: Architekturtests fuer neue Extension-Points erweitern

**Ziel:** Neue Plugin-Faehigkeiten bleiben contract-first und isoliert.

**Akzeptanzkriterien:**
- Tests blocken direkte Referenzen auf Core-Namensraeume fuer neue Provider-Typen.
- Tests pruefen Manifest-Kategorien und Extension-Point-Validierung.
- Smoke-Tests decken Navigation, generische Seiten und technische Provider ab.

## Priorisierungsvorschlag

1. `CG-BL-001`, `CG-BL-002`, `CG-BL-003`: Taxonomie und Sichtbarkeit schaffen.
2. `CG-BL-010`, `CG-BL-011`, `CG-BL-015`: Mitglieder-Profil-Plugins produktionsreif machen.
3. `CG-BL-020`, `CG-BL-021`, `CG-BL-022`: Generelle Erweiterungen sichtbar und nutzbar machen.
4. `CG-BL-030`, `CG-BL-032`, `CG-BL-033`: Technische Plugins aus den vorhandenen Facades heraus konkretisieren.
5. `CG-BL-040`, `CG-BL-041`, `CG-BL-042`: Governance und Betrieb absichern.
