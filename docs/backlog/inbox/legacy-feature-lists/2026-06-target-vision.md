# ClubGear Zielbild

Stand: 2026-06-09

## Was ich bauen will

Ich will ClubGear zu einer erweiterbaren Vereinsverwaltung ausbauen. Der Kern soll stabile Vereinsfunktionen bereitstellen, waehrend Zusatzfunktionen ueber klar geregelte Plugins ergaenzt werden koennen.

Das Ziel ist nicht, dass Plugins beliebig in den Core eingreifen. Stattdessen sollen Plugins ueber definierte Vertraege arbeiten: mit eigenem Manifest, eigenen Berechtigungen, isolierter Datenhaltung, validierten Extension-Points und einer Admin-Oberflaeche, die Zweck, Risiken und Status eines Plugins sichtbar macht.

## Grundidee

ClubGear bleibt der Host. Der Host verwaltet Mitglieder, Rollen, Permissions, Audit, Notifications, Plugin-Lifecycle, Migrationen und UI-Integration.

Plugins liefern fachliche oder technische Erweiterungen. Sie duerfen nur ueber freigegebene Contracts und Host-Facades mit ClubGear sprechen. Dadurch bleibt der Core wartbar, pruefbar und sicher, auch wenn spaeter viele Vereinsfunktionen als Plugin dazukommen.

## Die drei Plugin-Arten

### 1. Mitglieder-Profil-Plugins

Diese Plugins erweitern einzelne Mitgliederprofile um zusaetzliche Daten, Karten, Tabs, Statusanzeigen oder Aktionen.

Beispiele:
- Fahrzeuge eines Mitglieds
- GPS-Tracker eines Mitglieds
- Ausweise, Qualifikationen, Lizenzen oder vereinsinterne Nachweise

Wichtig ist, dass solche Daten eindeutig an ein Mitglied gebunden sind, aber nicht direkt in Core-Tabellen geschrieben werden. Plugins sollen dafuer eine standardisierte Member-Extension-Data-Fondation bekommen.

Ziel:
- Plugin-Daten am Mitglied speichern
- Daten im Mitgliederprofil anzeigen und bearbeiten
- Self-Service getrennt fuer Lesen und Schreiben steuern
- Plugin-Felder spaeter in Suche, Segmenten, Import und Export nutzen

### 2. Generelle Erweiterungen

Diese Plugins bringen eigene Vereinsfunktionen mit, die nicht nur an einem einzelnen Mitglied haengen.

Beispiele:
- Inventarverwaltung
- Vereinszeitung
- Materialausleihe
- Content- oder Dokumentbereiche

Solche Plugins sollen eigene Navigationseintraege, Listen, Detailseiten, Aktionen und Dashboard-Widgets beitragen koennen. Der Host rendert diese Oberflaechen moeglichst generisch, damit Plugins keine direkten Core-Views brauchen.

Ziel:
- Plugins erscheinen bei Bedarf in der Hauptnavigation
- Listen- und Detailansichten werden plugin-generisch gerendert
- Aktionen laufen ueber schema-basierte Commands
- Permissions steuern, wer welche Plugin-Funktionen sieht

### 3. Technische Plugins

Diese Plugins erweitern systemnahe Funktionen von ClubGear.

Beispiele:
- Notification-Kanaele wie Matrix, Push oder Webhook
- Audit-Log-Sinks fuer externe Systeme
- Background-Jobs
- Identity Provider oder SSO-Anbindungen

Technische Plugins sind riskanter als reine Fachplugins, weil sie naeher am Betriebssystem, an externen Diensten oder am Login-Flow liegen. Deshalb brauchen sie besonders klare Konfiguration, Risikoanzeige, Health-Checks, Auditierung und Fehlerisolation.

Ziel:
- technische Provider-Vertraege definieren
- Secrets sicher speichern und maskieren
- Jobs kontrolliert starten und stoppen
- Fehler sichtbar machen, ohne den Core zu blockieren

## Was bereits vorhanden ist

ClubGear hat schon eine gute technische Basis:

- ASP.NET-Core-8-MVC-Anwendung mit SQLite und Identity
- Plugin-Lifecycle fuer Installation, Aktivierung, Deaktivierung und Laden
- Plugin-Manifeste, Signatur- und Hash-Pruefung
- Contract-Kompatibilitaetspruefung
- Plugin-Migrationen und plugin-isolierte Persistence
- Runtime-Isolation gegen direkte Core-Abhaengigkeiten
- Member-Slots, Admin-Panels, Runtime-Routen und Background-Job-Contributions
- Facades fuer Permissions, Audit und Notifications
- Beispielplugin `CarInfo`
- Architektur- und Regressionstests

Das Fundament ist also da. Jetzt muss es fachlich klarer, adminfreundlicher und produktionsnaeher werden.

## Was noch fehlt

Die groesste Luecke ist die klare Produktstruktur rund um Plugins.

Aktuell gibt es viele technische Bausteine, aber noch nicht genug sichtbare Ordnung:

- Plugin-Kategorien fehlen im Manifest
- Extension-Points sind noch nicht vollstaendig katalogisiert und validiert
- Admins sehen noch nicht klar genug, was ein Plugin tut und welche Risiken es hat
- Mitglieder-Profil-Plugins brauchen eine einheitliche Datenbasis
- Generelle Plugins brauchen Navigation, Listen, Details und Dashboard-Integration
- Technische Plugins brauchen explizite Provider-Vertraege
- Plugin-Permissions, Aktivierung, Health, Deinstallation und Datenloeschung muessen besser steuerbar werden

## Reihenfolge

### Phase 1: Plugin-Fundament klaeren

Zuerst muss jedes Plugin eindeutig beschreiben, was es ist und wo es eingreifen will.

Umsetzung:
- Plugin-Kategorien im Manifest einfuehren
- Extension-Points katalogisieren und validieren
- Plugin-Detailseite fuer Admins ausbauen
- Plugin-Permissions administrierbar machen

Ergebnis:
Admins und Entwickler verstehen, welche Art von Plugin installiert wird, welche Rechte es braucht und welche Teile von ClubGear es erweitert.

### Phase 2: Mitglieder-Profil-Plugins produktionsreif machen

Danach sollen Erweiterungen am Mitglied sauber funktionieren.

Umsetzung:
- standardisierte Member-Extension-Data-Fondation bauen
- `CarInfo` als Referenzplugin stabilisieren
- Self-Service-Freigaben fuer Profilplugins einfuehren
- GPS-Tracker als zweites Beispielplugin anlegen
- Suche, Segmente, Import und Export fuer Plugin-Daten vorbereiten

Ergebnis:
Vereine koennen Mitgliederdaten flexibel erweitern, ohne den Core fuer jede Sonderlogik anzupassen.

### Phase 3: Generelle Erweiterungen ermoeglichen

Dann sollen Plugins vollwertige eigene Vereinsbereiche liefern koennen.

Umsetzung:
- Navigation-Contributions einfuehren
- generische Listen- und Detailseiten bauen
- Inventar-Plugin als Referenz umsetzen
- Vereinszeitung und Dashboard-Widgets als weitere Beispiele nutzen

Ergebnis:
ClubGear kann neue Fachbereiche aufnehmen, ohne fuer jede Erweiterung neue Core-Seiten zu schreiben.

### Phase 4: Technische Plugins konkretisieren

Anschliessend werden systemnahe Erweiterungen sauber angebunden.

Umsetzung:
- sichere technische Plugin-Konfiguration zentralisieren
- Notification-Channel-Provider bauen
- Audit-Log-Sink-Plugins ermoeglichen
- Background-Jobs fuer technische Plugins aktivieren
- Identity-Provider-Schnittstelle fachlich und technisch klaeren

Ergebnis:
ClubGear kann externe Systeme und technische Dienste anbinden, ohne Stabilitaet und Login-Sicherheit zu riskieren.

### Phase 5: Governance, Betrieb und Developer Experience abrunden

Zum Schluss braucht das Plugin-System gute Betriebs- und Entwicklungswerkzeuge.

Umsetzung:
- Aktivierungsprozess mit Risiko-Check
- Daten-Lifecycle fuer Deaktivieren, Deinstallieren und Loeschen
- Plugin-Health und Diagnose
- Plugin-Authoring-Guide nach Kategorien
- Plugin-Projekt-Template
- Architekturtests fuer neue Extension-Points

Ergebnis:
Plugins sind nicht nur technisch moeglich, sondern im Vereinsalltag installierbar, nachvollziehbar, wartbar und testbar.

## Zielzustand

Am Ende soll ClubGear ein stabiler Vereinsverwaltungs-Core mit einem kontrollierten Plugin-Oekosystem sein.

Ein Vereinsadmin soll sehen:
- Welche Plugins installiert sind
- Zu welcher Kategorie sie gehoeren
- Welche Permissions sie brauchen
- Welche UI-Bereiche, Jobs oder Provider sie beitragen
- Ob Migrationen, Signaturen, Health-Checks und Jobs in Ordnung sind
- Welche Daten beim Deaktivieren oder Deinstallieren erhalten bleiben oder geloescht werden

Ein Plugin-Autor soll wissen:
- Welche Plugin-Art er baut
- Welche Extension-Points erlaubt sind
- Welche Contracts und Facades er verwenden darf
- Wie Persistence, Permissions, Tests und Packaging funktionieren
- Welche Referenzplugins als Vorlage dienen

Ein Vereinsmitglied soll nur die Funktionen sehen, die fuer es freigegeben sind. Besonders bei Profilplugins muss getrennt steuerbar sein, ob ein Mitglied Daten nur lesen oder auch selbst bearbeiten darf.

## Kurz gesagt

Ich will ClubGear so weiterentwickeln, dass Vereine ihre Verwaltung modular erweitern koennen:

- Mitgliederprofile durch fachliche Zusatzdaten
- Vereinsbereiche durch eigenstaendige Funktionsplugins
- Systemfunktionen durch technische Provider

Der Core bleibt dabei stabil, sicher und uebersichtlich. Plugins werden nicht als Sonderfaelle eingebaut, sondern als klar typisierte, validierte und administrierbare Erweiterungen.
