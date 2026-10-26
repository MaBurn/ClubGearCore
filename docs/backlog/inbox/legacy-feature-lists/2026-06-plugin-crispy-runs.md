# Crispy Commands fuer ClubGear Plugin-Backlog

Stand: 2026-06-09

Ziel dieser Datei: Das Backlog aus `.backlog/backlog.md` ist in kleine, logisch abhaengige `/crispy-Start`-Befehle geschnitten. Jeder Befehl sollte QRSPI vollstaendig durchlaufen: Fragen, Recherche, Struktur, Plan, Implementation. Die Reihenfolge ist bewusst gesetzt; spaetere Befehle sollten erst gestartet werden, wenn die direkten Abhaengigkeiten abgeschlossen sind.

## Guardrail-Protokoll fuer alle Commands

Jeder `/crispy-Start`-Befehl muss vor Datei-Aenderungen die ADR `ADRs/0001-plugin-development-guardrails.md` anwenden.

Pflicht fuer jeden Command:
- Task klassifizieren: `plugin-only`, `generic-core`, `mixed` oder `unclear`.
- Plugin-Kategorie bestimmen: `member-profile`, `general-extension`, `technical`, `cross-category` oder `unknown`.
- Vorab entscheiden, ob Core-Code betroffen ist.
- Wenn Core-Code betroffen ist: vor der Implementierung explizit User-Freigabe nach dem ADR-Approval-Template einholen.
- Keine plugin-spezifische Logik in den Core einbauen.
- Nach Umsetzung die relevanten Guardrails und Tests im Ergebnis nennen.

Kurzregel: Plugin-spezifische Arbeit bleibt im Plugin. Generische Core-Aenderungen sind nur mit vorheriger User-Freigabe erlaubt.

## Dependency Map

1. Plugin-Fundament und Governance-Basis: Commands 01-04
2. Mitglieder-Profil-Plugins: Commands 05-10
3. Generelle Erweiterungen: Commands 11-14
4. Technische Plugins: Commands 15-19
5. Betrieb, Lifecycle und Diagnose: Command 20
6. DX, Doku und Release-Gates: Commands 21-22

## Command 01 - Plugin-Kategorien im Manifest

Abhaengigkeiten: keine
Backlog-Bezug: CG-BL-001

```text
/crispy-Start Analysiere und implementiere Plugin-Kategorien im ClubGear Plugin-Manifest. Ziel: Plugins deklarieren eindeutig eine Kategorie member-profile, general-extension oder technical. Erweitere Contracts/Plugin, Manifest-Parser, plugin.json-Kompatibilitaet, Admin-Anzeige und Tests. Achte auf additive Rueckwaertskompatibilitaet fuer bestehende Plugins ohne Kategorie; diese sollen eine sinnvolle Default-Kategorie erhalten oder klar als legacy/uncategorized behandelt werden.
```

Guardrail-Hinweis: Dieser Command ist `generic-core`. Vor Core-Aenderungen ist User-Freigabe nach ADR 0001 erforderlich.

## Command 02 - Extension-Points katalogisieren und validieren

Abhaengigkeiten: Command 01
Backlog-Bezug: CG-BL-002

```text
/crispy-Start Erstelle einen validierten Extension-Point-Katalog fuer ClubGear Plugins. Ziel: Extension-Points werden als stabile Contract-Werte dokumentiert und im Manifest validiert. Ordne alle vorhandenen und geplanten Extension-Points den Kategorien member-profile, general-extension und technical zu. Implementiere Parser-Validation, Tests und Dokumentation im Plugin Authoring Guide.
```

Guardrail-Hinweis: Dieser Command ist `generic-core`. Vor Core-Aenderungen ist User-Freigabe nach ADR 0001 erforderlich.

## Command 03 - Plugin-Detailseite und Aktivierungsrisiko

Abhaengigkeiten: Commands 01-02
Backlog-Bezug: CG-BL-003, CG-BL-041

```text
/crispy-Start Erweitere die Plugin-Admin-Oberflaeche um Plugin-Detailinformationen und einen Aktivierungs-Risiko-Check. Ziel: Admins sehen Kategorie, Permissions, Extension-Points, Quelle, Version, Signaturstatus, Migrationsstatus und technische Risiken vor Aktivierung. Implementiere UI, Backend-Query-Modelle, Aktivierungsbestaetigung fuer technische Plugins und Tests fuer Anzeige sowie Fehlerfaelle.
```

Guardrail-Hinweis: Dieser Command ist `generic-core`. Vor Core-Aenderungen ist User-Freigabe nach ADR 0001 erforderlich.

## Command 04 - Plugin-Permissions administrierbar machen

Abhaengigkeiten: Commands 01-03
Backlog-Bezug: CG-BL-040

```text
/crispy-Start Mache Plugin-Permissions in ClubGear administrierbar. Ziel: Nach Installation werden Plugin-Permissions mit Modulname, Beschreibung und Risikokategorie in der Rollen-/Permission-Verwaltung sichtbar. Rollen koennen diese Permissions erhalten oder verlieren. Entfernte oder deaktivierte Plugins duerfen keine kaputten Rollenansichten erzeugen. Implementiere Datenfluss, UI und Regressionstests.
```

Guardrail-Hinweis: Dieser Command ist `generic-core`. Vor Core-Aenderungen ist User-Freigabe nach ADR 0001 erforderlich.

## Command 05 - Member Extension Data Foundation

Abhaengigkeiten: Commands 01-04
Backlog-Bezug: CG-BL-010

```text
/crispy-Start Entwirf und implementiere ein standardisiertes Datenmodell fuer plugin-erweiterte Mitgliedsdaten. Ziel: Member-Profile-Plugins koennen strukturierte Zusatzdaten mit MemberId speichern, ohne Core-Tabellen direkt zu kennen. Nutze plugin-isolierte Persistence, Permission-Pruefung und Self-Service-Kontext. Erweitere Contracts und Host-Facades additiv, dokumentiere Migrationen und schreibe Tests fuer Isolation, CRUD und Berechtigungen.
```

Guardrail-Hinweis: Dieser Command ist `generic-core` oder `mixed`. Vor Core-Aenderungen ist User-Freigabe nach ADR 0001 erforderlich.

## Command 06 - CarInfo als Member-Profil-Referenzplugin stabilisieren

Abhaengigkeiten: Command 05
Backlog-Bezug: CG-BL-011

```text
/crispy-Start Stabilisiere das CarInfo Plugin als Referenz fuer Mitglieder-Profil-Plugins. Ziel: Fahrzeuge koennen im Mitgliederprofil hinzugefuegt, bearbeitet und geloescht werden; Detailansicht zeigt Fahrzeuge als Plugin-Detailkarte; Bearbeiten nutzt ein Plugin-Tab oder schema-getriebene Action-UI. Halte den Core plugin-generisch und passe CarInfo an die neue Member Extension Data Foundation an. Tests muessen Admin- und Member-Flows abdecken.
```

Guardrail-Hinweis: Dieser Command ist primaer `plugin-only`, solange nur CarInfo angepasst wird. Jede Core-Aenderung braucht vorher User-Freigabe nach ADR 0001.

## Command 07 - Self-Service-Freigaben fuer Profilplugins

Abhaengigkeiten: Commands 05-06
Backlog-Bezug: CG-BL-015

```text
/crispy-Start Implementiere Self-Service-Freigaben fuer Mitglieder-Profil-Plugins. Ziel: Admins steuern pro Plugin und Slot, ob Mitglieder Daten sehen oder bearbeiten duerfen. Schreibrechte sind getrennt von Leserechten. Die Self-Service-UI blendet nicht erlaubte Karten, Tabs und Aktionen aus. Nutze CarInfo als Referenzfall und schreibe Tests fuer Lesen erlaubt, Schreiben verboten und vollstaendig verborgen und vollständig erlaubt (Lesen &).
```

Guardrail-Hinweis: Dieser Command ist `generic-core` oder `mixed`. Vor Core-Aenderungen ist User-Freigabe nach ADR 0001 erforderlich.

## Command 08 - GPS-Tracker Profilplugin konzipieren und als zweites Beispiel anlegen

Abhaengigkeiten: Commands 05-07
Backlog-Bezug: CG-BL-012

```text
/crispy-Start Konzipiere und scaffold-e ein GPS-Tracker Plugin als zweites Mitglieder-Profil-Beispiel. Ziel: Tracker-ID, Anbieter, Aktivstatus, Ausgabe- und Rueckgabedatum werden pro Mitglied verwaltet. Das Plugin nutzt dieselben Member-Extension-Kontrakte wie CarInfo, liefert Detailkarte, Status-Badge und Admin-Aktionen zum Zuweisen und Entziehen. Fokus: saubere Architektur, Tests und Dokumentation, nicht maximale Feature-Tiefe.
```

Guardrail-Hinweis: Dieser Command ist primaer `plugin-only`. Jede Core-Aenderung braucht vorher User-Freigabe nach ADR 0001.

## Command 09 - Plugin-Felder in Mitgliedersuche und Segmenten

Abhaengigkeiten: Commands 05-08
Backlog-Bezug: CG-BL-013

```text
/crispy-Start Mache plugin-erweiterte Mitgliedsdaten in Suche und Segmenten nutzbar. Ziel: Member-Profile-Plugins deklarieren filterbare Felder; die Mitgliederliste rendert diese Filter dynamisch; die Auswertung bleibt plugin-isoliert und sicher gegen freies SQL. Implementiere mindestens einen Referenzfilter fuer CarInfo oder GPS-Tracker und ergaenze Tests fuer Filteranzeige, Berechtigungen und Ergebnislogik.
```

Guardrail-Hinweis: Dieser Command ist `generic-core` oder `mixed`. Vor Core-Aenderungen ist User-Freigabe nach ADR 0001 erforderlich.

## Command 10 - Import/Export fuer Plugin-Mitgliedsdaten

Abhaengigkeiten: Commands 05-09
Backlog-Bezug: CG-BL-014

```text
/crispy-Start Implementiere Import und Export fuer plugin-erweiterte Mitgliedsdaten. Ziel: Exporte koennen Plugin-Daten optional einschliessen; Importe koennen Plugin-Spalten validieren und an das passende Plugin delegieren; Fehler erscheinen pro Zeile und Feld. Achte auf Permissions, grosse Dateien, stabile Spaltennamen und Rueckwaertskompatibilitaet bestehender Member-Importe.
```

Guardrail-Hinweis: Dieser Command ist `generic-core` oder `mixed`. Vor Core-Aenderungen ist User-Freigabe nach ADR 0001 erforderlich.

## Command 11 - Navigation-Contributions fuer generelle Plugins

Abhaengigkeiten: Commands 01-04
Backlog-Bezug: CG-BL-020

```text
/crispy-Start Implementiere Navigation-Contributions fuer generelle ClubGear Plugins. Ziel: Plugins wie Inventar oder Vereinszeitung koennen Menueeintraege mit Label, Icon, Route, Permission und Sortierung beisteuern. Der Host rendert sichtbare Eintraege nur bei vorhandener Permission und zeigt Contributions in der Plugin-Admin-Ansicht. Ergaenze Contract, Runtime-Collector, Layout-Integration und Tests.
```

Guardrail-Hinweis: Dieser Command ist `generic-core`. Vor Core-Aenderungen ist User-Freigabe nach ADR 0001 erforderlich.

## Command 12 - Generische Plugin-Seiten fuer Listen und Details

Abhaengigkeiten: Command 11
Backlog-Bezug: CG-BL-021

```text
/crispy-Start Baue generische Plugin-Seiten fuer Listen und Details. Ziel: General-Extension-Plugins koennen Listenansichten mit Spalten, Filtern, Aktionen und Detailansichten bereitstellen, ohne Core-spezifische Views zu schreiben. Nutze schema-basierte Commands fuer Create, Edit und Delete. Implementiere sichere Rendering-Regeln, Permission-Pruefung, Fehlerdarstellung und Tests fuer plugin-agnostisches Verhalten.
```

Guardrail-Hinweis: Dieser Command ist `generic-core`. Vor Core-Aenderungen ist User-Freigabe nach ADR 0001 erforderlich.

## Command 13 - Inventar-Plugin als General-Extension-Referenz

Abhaengigkeiten: Commands 11-12
Backlog-Bezug: CG-BL-022

```text
/crispy-Start Erstelle ein Inventar-Plugin als Referenz fuer generelle Erweiterungen. Ziel: Inventarobjekte haben Nummer, Name, Kategorie, Status, Lagerort und optional verantwortliches Mitglied. Das Plugin liefert Navigation, Listenansicht, Detailansicht und Admin-Panel ueber generische Host-Oberflaechen. Implementiere Migrationen, Plugin-Persistence, CRUD-Kommandos, Tests und Dokumentation.
```

Guardrail-Hinweis: Dieser Command ist primaer `plugin-only`, wenn Commands 11-12 abgeschlossen sind. Jede Core-Aenderung braucht vorher User-Freigabe nach ADR 0001.

## Command 14 - Vereinszeitung und Dashboard-Widgets

Abhaengigkeiten: Commands 11-13
Backlog-Bezug: CG-BL-023, CG-BL-024

```text
/crispy-Start Implementiere die Foundation fuer Dashboard-Widgets und nutze sie in einem Vereinszeitung-Plugin-Prototyp. Ziel: Plugins koennen kompakte Widgets mit Titel, Inhalt, Permission und Sortierung beitragen. Die Vereinszeitung verwaltet Ausgaben, Artikel und Publikationsstatus; Mitglieder sehen veroeffentlichte Ausgaben im Self-Service. Achte auf sichere Inhalte, Permission-Grenzen und Tests fuer Widget-Rendering.
```

Guardrail-Hinweis: Dieser Command ist `mixed`. Vor Core-Aenderungen ist User-Freigabe nach ADR 0001 erforderlich.

## Command 15 - Technische Plugin-Konfiguration

Abhaengigkeiten: Commands 01-04
Backlog-Bezug: CG-BL-034

```text
/crispy-Start Zentralisiere Konfiguration fuer technische Plugins. Ziel: Systemnahe Plugins erhalten eine sichere Konfigurationsablage mit Secret-Markierung, Maskierung in der UI, Auditierung von Aenderungen und Zugriff nur ueber Host-Facade. Nutze bestehende SystemConfig- und Admin-Panel-Muster, erweitere sie plugin-generisch und schreibe Tests fuer Secrets, Permissions und Audit.
```

Guardrail-Hinweis: Dieser Command ist `generic-core` fuer `technical` Plugins. Vor Core-Aenderungen ist User-Freigabe nach ADR 0001 erforderlich.

## Command 16 - Notification-Channel-Provider

Abhaengigkeiten: Command 15
Backlog-Bezug: CG-BL-030

```text
/crispy-Start Implementiere einen Notification-Channel-Provider-Vertrag fuer technische Plugins. Ziel: Plugins koennen neue Kanaele wie Matrix, Push oder Webhook bereitstellen. Der Core-Notification-Service ruft aktive Plugin-Kanaele ueber die Runtime auf; Admins koennen Kanal-Konfiguration schema-basiert testen. Versandfehler werden auditierbar protokolliert und duerfen andere Kanaele nicht blockieren.
```

Guardrail-Hinweis: Dieser Command ist `generic-core` oder `mixed` fuer `technical` Plugins. Vor Core-Aenderungen ist User-Freigabe nach ADR 0001 erforderlich.

## Command 17 - Audit-Log-Sink-Plugins

Abhaengigkeiten: Command 15
Backlog-Bezug: CG-BL-032

```text
/crispy-Start Implementiere Audit-Log-Sink-Plugins. Ziel: Audit-Events koennen zusaetzlich an externe Systeme gesendet werden, waehrend der Core-Audit-Eintrag immer erhalten bleibt. Definiere Contract, Eventmodell, Delivery-Ergebnis, Fehlerisolation und Retry-/Backpressure-Entscheidung. Ergaenze Tests fuer erfolgreiche Auslieferung, Plugin-Fehler und Permission-/Isolation-Grenzen.
```

Guardrail-Hinweis: Dieser Command ist `generic-core` oder `mixed` fuer `technical` Plugins. Vor Core-Aenderungen ist User-Freigabe nach ADR 0001 erforderlich.

## Command 18 - Background-Jobs fuer technische Plugins

Abhaengigkeiten: Commands 15-17
Backlog-Bezug: CG-BL-033

```text
/crispy-Start Aktiviere BackgroundJob-Contributions fuer technische Plugins. Ziel: Die bereits vorhandenen BackgroundJob-Contributions werden zur Laufzeit gestartet, kontrolliert gestoppt und in der Admin-UI mit Status und letzter Ausfuehrung angezeigt. Jobs erhalten Host-Kontext und CancellationToken. Deaktivieren eines Plugins stoppt dessen Jobs sauber. Schreibe Tests fuer Start, Stop, Fehler und Deaktivierung.
```

Guardrail-Hinweis: Dieser Command ist `generic-core` fuer `technical` Plugins. Vor Core-Aenderungen ist User-Freigabe nach ADR 0001 erforderlich.

## Command 19 - Identity-Provider-Plugin-Schnittstelle

Abhaengigkeiten: Commands 01-04, Command 15
Backlog-Bezug: CG-BL-031

```text
/crispy-Start Erarbeite und implementiere die erste tragfaehige Identity-Provider-Plugin-Schnittstelle fuer ClubGear. Klaere zuerst per QRSPI-Design, welche Teile wegen ASP.NET-Core-Auth beim App-Start statisch sein muessen. Ziel: Provider-Metadaten, Claims-Mapping, Rollen-Mapping, Aktivierung, Testfunktion und sicherer Fallback fuer den Login-Flow. Falls Runtime-Plugins fuer Auth technisch nicht sinnvoll sind, liefere eine ADR und eine konfigurationsbasierte Alternative.
```

Guardrail-Hinweis: Dieser Command ist `generic-core` oder ADR-only fuer `technical` Plugins. Vor Core-Aenderungen ist User-Freigabe nach ADR 0001 erforderlich.

## Command 20 - Plugin-Daten-Lifecycle, Health und Deinstallation

Abhaengigkeiten: Commands 03-04; mindestens ein abgeschlossenes Realbeispiel aus Commands 05-10, 11-14 und 15-19
Backlog-Bezug: CG-BL-042, CG-BL-043

```text
/crispy-Start Definiere und implementiere Plugin-Daten-Lifecycle, Health und Diagnose. Ziel: Deaktivieren behaelt Daten, Deinstallieren bietet Daten behalten oder loeschen, Loeschvorgaenge sind auditierbar, und Admins sehen Ladefehler, Migrationsstatus, Jobstatus, Health-Checks und letzte Exceptions. Achte auf personenbezogene Daten, Secrets, klare UI-Texte und Regressionstests fuer Deaktivieren, Deinstallieren und Diagnose.
```

Guardrail-Hinweis: Dieser Command ist `generic-core` oder `mixed`. Vor Core-Aenderungen ist User-Freigabe nach ADR 0001 erforderlich.

## Command 21 - Plugin Authoring Guide nach Kategorien

Abhaengigkeiten: Commands 01-20 nach Bedarf; kann nach Command 14 und 18 als Zwischenstand gestartet werden
Backlog-Bezug: CG-BL-050

```text
/crispy-Start Aktualisiere den Plugin Authoring Guide nach Plugin-Kategorien. Ziel: Getrennte Kapitel fuer Mitglieder-Profil, generelle Erweiterung und technische Plugins mit Manifest, Permissions, Contributions, Persistence, Tests und Packaging. Nutze CarInfo, GPS-Tracker, Inventar, Vereinszeitung, Notification und Audit als Referenzbeispiele, soweit bereits implementiert. Halte Beispiele copy-paste-nah und versionskompatibel.
```

Guardrail-Hinweis: Dieser Command ist primaer Dokumentation. Core-Aenderungen sind nicht Teil dieses Commands und brauchen sonst vorher User-Freigabe nach ADR 0001.

## Command 22 - Plugin-Projekt-Template und Architekturtests

Abhaengigkeiten: Commands 01-21
Backlog-Bezug: CG-BL-051, CG-BL-052

```text
/crispy-Start Erstelle ein Plugin-Projekt-Template und erweitere die Architekturtests fuer alle neuen Extension-Points. Ziel: Ein neues Plugin kann mit Kategorie, plugin.json, csproj, IPluginModule und Beispielprovider gestartet werden. Build erzeugt ein signierbares ZIP. Architekturtests blocken direkte Core-Referenzen fuer neue Provider-Typen und pruefen Manifest-Kategorien, Extension-Point-Validation, Navigation, generische Seiten und technische Provider-Smoke-Flows.
```

Guardrail-Hinweis: Dieser Command ist `generic-core` oder `mixed` fuer DX und Tests. Vor Core-Aenderungen ist User-Freigabe nach ADR 0001 erforderlich.

## Empfohlene Startreihenfolge

1. Starte mit Command 01.
2. Danach Commands 02-04 abschliessen, bevor Feature-Plugins erweitert werden.
3. Fuer das aktuelle CarInfo-nahe Produktziel: Commands 05-07 direkt nach dem Fundament.
4. General Extensions koennen ab Command 11 parallel zu spaeteren Member-Themen laufen, sobald Commands 01-04 fertig sind.
5. Technische Plugins sollten mit Command 15 starten, weil Notification, Audit, Jobs und Identity sichere Konfiguration brauchen.
6. Command 20 erst starten, wenn mindestens ein Member-, ein General- und ein Technical-Plugin als Realbeispiel existiert.
