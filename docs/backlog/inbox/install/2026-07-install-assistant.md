# ClubGear Install-Assistent Backlog

Stand: 2026-07-09

Ziel dieser Datei: Die saubere Version eines ersten Setup-Assistenten fuer ClubGear ist in kleine, logisch abhaengige Slices geschnitten. Jeder Slice ist als kopierbarer `/crispy start ...` Befehl formuliert und soll eigenstaendig mit Analyse, Design, Implementation, Tests und kurzer Doku abgeschlossen werden.

## Zielbild

Beim ersten Start ist ClubGear ohne manuelle Datenbank- oder Config-Eingriffe ueber `/install` einrichtbar. Der Assistent legt die technischen Grundlagen an, fragt Betreiber- und Admin-Daten ab, schreibt die Systemkonfiguration, erzeugt den ersten Admin-Benutzer und schaltet sich danach automatisch ab.

Nach abgeschlossener Installation gilt:
- `/install` ist nicht mehr nutzbar und fuehrt auf Login, Home oder eine 404/Redirect-Entscheidung.
- Die Anwendung startet ohne reale Seed-Vereinsdaten.
- Es gibt genau einen klaren Bootstrap-Pfad fuer den ersten Admin.
- Setup-Daten werden validiert, auditierbar gespeichert und koennen nicht durch parallele Requests doppelt angelegt werden.
- Docker/Podman-Starts funktionieren ohne lokale .NET-Installation weiterhin.

## Guardrail-Protokoll fuer alle Slices

Alle Slices sind `generic-core`, weil sie Startup, Routing, Identity, SystemConfig und Seed-Verhalten betreffen. Vor Datei-Aenderungen:
- ADR `ADRs/0001-plugin-development-guardrails.md` lesen und anwenden.
- Bestehende Seed-, Identity-, SystemConfig- und Admin-Patterns pruefen.
- Keine plugin-spezifische Logik einbauen.
- Tests in `tests/ClubGear.ArchitectureTests` ergaenzen oder anpassen.
- Keine realen Vereins-, Personen-, Mail- oder Adressdaten als Defaults verwenden.

## Dependency Map

1. Installationsstatus und technische Basis: Slices 01-02
2. `/install` Flow und UI: Slices 03-05
3. Seed- und Bootstrap-Verhalten bereinigen: Slices 06-07
4. Sicherheit, Nebenlaeufigkeit und Abschaltung: Slices 08-09
5. Betriebsreife, Doku und Tests: Slices 10-12

## Slice 01 - Installationsstatus und Service-Foundation

Abhaengigkeiten: keine

```text
/crispy start Analysiere und implementiere eine Installationsstatus-Foundation fuer ClubGear. Ziel: Der Core kann eindeutig erkennen, ob die Anwendung noch eingerichtet werden muss oder bereits installiert ist. Nutze vorhandene SystemConfigEntry-Patterns und fuehre mindestens System.InstallCompleted, System.InstallCompletedAtUtc und optional System.InstallVersion ein. Erstelle einen kleinen Service wie IInstallationStateService fuer Lesen und atomare Statusaenderung. Schreibe Architekturtests fuer frische Datenbank, gesetzten Status und idempotentes Setzen des Installationsabschlusses.
```

Akzeptanzkriterien:
- Installationsstatus ist ueber einen Service abfragbar.
- Der Status nutzt bestehende Persistenzmuster und braucht keine neue Sonderdatenbank.
- Tests decken `not installed`, `installed` und idempotente Updates ab.

## Slice 02 - Install-Routing und Zugriffsgate

Abhaengigkeiten: Slice 01

```text
/crispy start Implementiere das Routing- und Zugriffsgate fuer /install. Ziel: /install ist nur erreichbar, solange ClubGear noch nicht installiert ist. Nach Abschluss wird /install kontrolliert blockiert oder weitergeleitet. Fuehre einen InstallController oder eine passende Middleware/Filter-Loesung ein, die bestehende Authentication- und MaintenanceMode-Pfade nicht kaputt macht. Schreibe Tests fuer GET /install vor Installation, GET /install nach Installation, Zugriff auf normale Seiten vor Installation und Login-/Static-Asset-Verhalten.
```

Akzeptanzkriterien:
- `/install` ist vor Abschluss erreichbar.
- `/install` ist nach Abschluss abgeschaltet.
- Login, statische Assets und Fehlerseiten bleiben kontrolliert erreichbar.
- Das Gate erzeugt keine Redirect-Schleifen.

## Slice 03 - Install-ViewModel und Validierung

Abhaengigkeiten: Slice 01

```text
/crispy start Erstelle die ViewModels und Validierungsregeln fuer den ClubGear Install-Assistenten. Ziel: Der Wizard erfasst Betreiber-Daten, ersten Admin-Benutzer, Basis-Mail-Konfiguration und optionale Runtime-Einstellungen strukturiert und sicher. Definiere Modelle fuer ClubName, ClubAddress, ClubZip, ClubCity, ClubEmail, ClubWebsite, AdminFullName, AdminEmail, AdminPassword, ConfirmPassword, EmailSenderName, EmailSenderAddress, SmtpHost, SmtpPort, UseStartTls und Plugin-Startmodus. Nutze DataAnnotations oder bestehende Validierungsmuster und schreibe Tests fuer Pflichtfelder, Passwortbestaetigung, E-Mail-Format und neutrale Defaults.
```

Akzeptanzkriterien:
- ViewModel deckt alle Setup-Felder ab.
- Keine realen Defaultdaten sind hart codiert.
- Validierung ist serverseitig getestet.

## Slice 04 - Erste Install-Seite als kompakter Wizard

Abhaengigkeiten: Slices 02-03

```text
/crispy start Implementiere die erste nutzbare /install UI fuer ClubGear als kompakten Wizard. Ziel: Eine klare Setup-Seite oder zwei logisch getrennte Schritte erfassen Betreiber- und Admin-Daten, zeigen Validierungsfehler sauber an und passen visuell zum bestehenden Bootstrap-Layout. Baue Views unter Views/Install, nutze vorhandene Layout-Konventionen ohne Marketing-Landingpage, und stelle sicher, dass Text und Formularfelder auf Mobile/Desktop nicht ueberlaufen. Schreibe strukturierende View-Tests, die zentrale Felder, Form-Action, CSRF-Token und Fehlerdarstellung pruefen.
```

Akzeptanzkriterien:
- `/install` zeigt ein vollstaendiges Formular.
- Layout passt zum bestehenden ClubGear-Stil.
- CSRF-Schutz ist aktiv.
- View-Tests pruefen zentrale Formularelemente.

## Slice 05 - Install-Command: Config und Admin anlegen

Abhaengigkeiten: Slices 01, 03, 04

```text
/crispy start Implementiere den Install-Command fuer ClubGear. Ziel: Ein gueltiger POST auf /install schreibt Club-Konfiguration und Mail-Basics in SystemConfig, erzeugt den ersten Admin-User ueber ASP.NET Core Identity, vergibt Admin- und MemberManager-Rollen nach bestehenden RoleNames, setzt InstallCompleted und leitet danach auf Login oder Admin weiter. Nutze eine transaktionale Service-Schicht, arbeite mit UserManager/RoleManager statt direkter Identity-Tabellenmanipulation und schreibe Tests fuer erfolgreichen Install, Validierungsfehler, doppelte Admin-Mail und Rollenzuweisung.
```

Akzeptanzkriterien:
- Erster Admin wird ueber Identity erzeugt.
- Rollen werden nach bestehenden RoleNames vergeben.
- SystemConfig enthaelt die eingegebenen Betreiber- und Maildaten.
- Installabschluss wird erst nach erfolgreichem Setup gesetzt.

## Slice 06 - Seed-Daten bereinigen und Demo-Mitglied entkoppeln

Abhaengigkeiten: Slice 01

```text
/crispy start Bereinige das ClubGear Seed-Verhalten fuer installationsfaehige Public-Releases. Ziel: Beim ersten Start werden keine realen Vereinsdaten und kein personenbezogen wirkendes Demo-Mitglied mehr automatisch erzeugt. Ersetze reale Defaults im SystemConfigSeedTask durch neutrale Platzhalter oder leere Werte und entscheide, ob MemberSeedTask entfernt, deaktiviert oder an einen expliziten Demo-/Development-Modus gebunden wird. Passe Tests an und pruefe, dass bestehende produktive Installationen durch fehlende Defaults nicht kaputt gehen.
```

Akzeptanzkriterien:
- Keine realen Vereins-, Adress- oder Kontaktmail-Daten im Seed.
- Demo-Mitglied wird nicht ungefragt in produktionsnahen Starts erzeugt.
- Bestehende SystemConfig-Eintraege werden nicht ueberschrieben.
- Tests decken frische DB und vorhandene DB ab.

## Slice 07 - First-User-Admin-Logik mit Installer harmonisieren

Abhaengigkeiten: Slices 01, 05

```text
/crispy start Harmonisiere die bestehende First-User-Admin-Logik mit dem neuen Install-Assistenten. Ziel: Es gibt keinen zweiten, widerspruechlichen Bootstrap-Pfad fuer Admin-Rechte. Pruefe AccountFeatureService, Registrierung und EnsureMasterAdminForFirstUserAsync. Entscheide, ob normale Registrierung vor Installation blockiert wird und ob nach Installation der erste User nur noch ueber /install Admin wird. Implementiere die gewaehlte Regel und schreibe Tests fuer Registrierung vor Installation, Registrierung nach Installation und Admin-Rollenvergabe.
```

Akzeptanzkriterien:
- Admin-Bootstrap ist eindeutig und dokumentiert.
- Normale Registrierung kann den Installer nicht umgehen.
- Bestehende Self-Service-Registrierung bleibt nach Installation funktionsfaehig.

## Slice 08 - Nebenlaeufigkeit und sichere Abschaltung

Abhaengigkeiten: Slices 01, 05

```text
/crispy start Haerte den ClubGear Install-Assistenten gegen parallele oder wiederholte Installationsversuche. Ziel: Zwei gleichzeitige POSTs auf /install koennen nicht zwei Admins oder widerspruechliche Config erzeugen. Nutze Datenbanktransaktion, eindeutige SystemConfig-Schluessel, erneute Statuspruefung im Command und sinnvolle Fehlerbehandlung. Schreibe Tests oder Service-Level-Smoke-Tests fuer doppelten Submit, bereits abgeschlossene Installation und teilweise fehlgeschlagenen Install.
```

Akzeptanzkriterien:
- Doppelte POSTs erzeugen keinen doppelten Bootstrap-Admin.
- Bereits installierte Systeme blockieren Install-POSTs.
- Fehler lassen das System nicht faelschlich als installiert zurueck.

## Slice 09 - Install-Middleware fuer globale Pre-Install-Fuehrung

Abhaengigkeiten: Slices 01-02

```text
/crispy start Implementiere eine globale Pre-Install-Fuehrung fuer ClubGear. Ziel: Wenn die Anwendung noch nicht installiert ist, fuehren geschuetzte normale Seiten kontrolliert zu /install, waehrend /install, statische Assets, Health/Fehlerseiten und notwendige Auth-Endpunkte keine Schleifen erzeugen. Ordne die Middleware sauber in Program.cs relativ zu Authentication, MaintenanceMode und Authorization ein. Schreibe Tests fuer typische Pfade: /, /Members, /Account/Login, /install, /css/site.css und nach InstallCompleted.
```

Akzeptanzkriterien:
- Uninstallierte Instanz fuehrt Benutzer zum Setup.
- Statische Assets funktionieren im Setup.
- Kein Konflikt mit MaintenanceMode.
- Installierte Instanz verhaelt sich wie bisher.

## Slice 10 - SMTP-Test und optionale Mail-Konfiguration

Abhaengigkeiten: Slices 03-05

```text
/crispy start Erweitere den Install-Assistenten um eine optionale SMTP-Konfiguration mit sicherem Testpfad. Ziel: Betreiber koennen beim Setup Mailwerte setzen, aber Installation ist auch ohne externen SMTP moeglich. Nutze bestehende EmailSettings- und NotificationChannel-Patterns, maskiere Passwoerter, speichere leere Secrets als leer und biete optional einen Test, der Fehler verstaendlich meldet ohne die Installation zu blockieren. Schreibe Tests fuer PickupDirectory-Default, SMTP-Werte, Passwort-Maskierung und fehlgeschlagenen Test.
```

Akzeptanzkriterien:
- Mailsetup ist optional.
- Secrets werden nicht sichtbar ausgegeben.
- Fehlgeschlagener SMTP-Test verhindert nicht zwingend die Installation.

## Slice 11 - Doku, Docker/Podman und Release-Hinweise

Abhaengigkeiten: Slices 02-09

```text
/crispy start Dokumentiere den ClubGear Install-Assistenten fuer Betrieb und Release. Ziel: README, docs/quickstart.md und docs/releases.md erklaeren den ersten Start ueber Docker/Podman, den Aufruf von /install, die einzugebenden Daten, das Abschalten nach Installation und den Reset fuer lokale Tests. Ergaenze Hinweise, dass keine .NET-Installation lokal noetig ist. Aktualisiere Screenshots nur mit neutralen Demo-Daten und pruefe, dass keine personenbezogenen Daten in der Doku landen.
```

Akzeptanzkriterien:
- Quickstart beschreibt First-Run-Setup.
- Docker/Podman-Pfade bleiben ohne lokale .NET-Installation nutzbar.
- Doku enthaelt keine realen Personen-/Vereinsdaten.

## Slice 12 - Release-Gate und End-to-End-Verifikation

Abhaengigkeiten: Slices 01-11

```text
/crispy start Erstelle ein Release-Gate fuer den ClubGear Install-Assistenten und verifiziere den kompletten First-Run-Flow. Ziel: Eine frische Datenbank wird gestartet, /install aufgerufen, Admin und Club-Konfiguration werden angelegt, /install ist danach abgeschaltet, Login funktioniert und normale Adminseiten sind erreichbar. Fuehre automatisierte Architekturtests aus, ergaenze fehlende Regressionstests und pruefe containerisierten Start mit Docker oder Podman. Dokumentiere die exakten Verifikationsbefehle und Ergebnisse.
```

Akzeptanzkriterien:
- Frischer First-Run funktioniert Ende-zu-Ende.
- `/install` ist nach Abschluss abgeschaltet.
- Admin-Login funktioniert.
- Tests und containerisierte Verifikation sind dokumentiert.

## Offene Designentscheidungen

- Soll `/install` nach Abschluss 404, Redirect auf Login oder Redirect auf Home liefern?
- Soll normale Registrierung vor Installation komplett blockiert werden?
- Soll das Demo-Mitglied nur im Development-Profil erhalten bleiben oder ganz entfernt werden?
- Soll der Wizard ein Schritt bleiben oder ab Slice 04 in Betreiber/Admin/Mail-Schritte getrennt werden?
- Soll `InstallCompleted` nur in SystemConfig liegen oder zusaetzlich als eigener Installationsdatensatz auditierbar werden?
