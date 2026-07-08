# ADR: Serviceheft Plugin

## Purpose

Architekturentscheidungen fuer das Serviceheft-Plugin als Erweiterung von CarInfo.

---

## 2026-06-18 - Plugin-only Umsetzung ohne neue Core-Aenderungen

### Decision

Das Serviceheft wird als eigenstaendiges Plugin `clubgear.plugin.servicebook` umgesetzt. Es
registriert `IMemberDetailCardProvider`, `IMemberEditTabProvider` und `IMemberActionProvider`.
Controller, Core-Services, Contracts und gemeinsame Views erhalten fuer diese Funktion keine
Serviceheft-spezifischen Aenderungen.

### Rationale

Die vorhandenen Member-Slots und Member-Actions reichen fuer Anzeige und Mutationen aus. Eine
eigene Core-Route `/User/Checkheft` oder ein Checkheft-Controller wuerde die Plugin-Grenze
durchbrechen und waere nach ADR 0001 GR-002 freigabepflichtig.

---

## 2026-06-18 - CarInfo-Integration ueber maschinenlesbare Dateninsel

### Decision

CarInfo ab Version 1.0.5 rendert im eigenen Edit-Tab ein HTML-encodiertes
`data-carinfo-vehicles`-Attribut. Es enthaelt `id`, `make`, `color`, `licensePlate` und
`extraValues` der Fahrzeuge des aktuellen Mitglieds. Das Serviceheft liest diese Daten
clientseitig und verwendet die stabile CarInfo-ID als Fahrzeugreferenz.

### Rationale

`IPluginDataStore` erlaubt absichtlich nur Tabellen mit dem Prefix des aufrufenden Plugins.
Direkte SQL-Zugriffe des Servicehefts auf CarInfo-Tabellen wuerden diese Isolation verletzen.
Ein generischer Cross-Plugin-Read-Contract existiert nicht. Die Dateninsel bleibt vollstaendig
in Plugin-Code, veraendert keinen Core-Vertrag und stellt nur Daten bereit, die CarInfo im selben
Mitgliedskontext ohnehin sichtbar rendert.

---

## 2026-06-18 - Serviceheft speichert Fahrzeug-Snapshots im eigenen Schema

### Decision

Jeder Serviceeintrag speichert neben `CarId` auch Fabrikat, Farbe und Kennzeichen als Snapshot.
Alle Serviceeintraege und Teile liegen in den plugin-eigenen Tabellen `service_records` und
`service_parts`. Jede Mutation wird durch `MemberId` auf das aktuelle Mitglied begrenzt.

### Rationale

Der Snapshot erhaelt die Wartungshistorie, wenn ein Fahrzeug spaeter in CarInfo geloescht oder
sein Kennzeichen geaendert wird. Die CarInfo-ID bleibt die primaere Zuordnung fuer aktive
Fahrzeuge; der Snapshot ist die historische Darstellung und keine zweite Fahrzeugquelle.

---

## 2026-06-18 - Plugin-eigenes UI nutzt bestehende Member-Action-API

### Decision

Der Checkheft-Tab rendert Fahrzeugkarten, Kostenuebersicht, Historie und ein Bootstrap-Modal als
HTML/JavaScript aus dem Plugin. Schreiboperationen laufen ueber die bestehenden Endpunkte
`/api/member/plugin-actions` und `/api/self-service/plugin-actions`. Der Action-Provider
veroeffentlicht `servicebook.record.add`, `servicebook.record.update` und
`servicebook.record.delete`, alle mit
`clubgear.plugin.servicebook.member.write`.

Die generisch vom Host gerenderten Action-Buttons werden durch Plugin-CSS fuer dieses Modul
ausgeblendet, weil die Aktionen kontextgebundene Fahrzeug- und Teilewerte benoetigen und deshalb
im Serviceheft-Modal ausgeloest werden.

### Rationale

Die angehaengte Referenzoberflaeche benoetigt verschachtelte Teilelisten und eine
fahrzeugbezogene Detailansicht. Das generische Einzelfeld-Modal kann diese Struktur nicht
abbilden. Die bestehende Action-API liefert dennoch Berechtigungspruefung, Mitgliedsbindung und
Plugin-Isolation, ohne neue Controller oder Core-Sonderlogik einzufuehren.

---

## 2026-06-18 - Self-Service rendert Plugin-Edit-Bereiche als eigenstaendige Karten

### Decision

`_PluginSlots.cshtml` erhaelt den generischen Modus `edit-cards`. Im Self-Service wird dieser
Modus verwendet und jeder `MemberEditTabSlot` als eigene Bootstrap-Karte mit dem Slot-Titel
gerendert. Der bisherige gemeinsame Kasten mit der Ueberschrift `Plugin-Erweiterungen` und die
globalen Action-Buttons entfallen im Self-Service. Die Mitgliederverwaltung verwendet weiterhin
den bestehenden `edit`-Modus mit Tabs.

### Rationale

Fahrzeuge und Serviceheft sind fachlich eigenstaendige Profilbereiche. Eine generische
Kartenabbildung gibt jedem Plugin seinen eigenen sichtbaren Bereich, ohne CarInfo- oder
Serviceheft-Namen im Core zu hinterlegen. Die Aktionen bleiben ueber die Buttons innerhalb des
jeweiligen Plugin-Inhalts erreichbar; das gemeinsame Action-Modal wird weiterhin einmal pro Seite
bereitgestellt.

---

## 2026-06-30 - Mitgliederverwaltung wechselt zu edit-cards-Modus (ServiceBook = CarInfo-Erweiterung)

### Decision

`Views/Members/Edit.cshtml` Zeile 18 wird von `ViewData["PluginSlotMode"] = "edit"` auf
`ViewData["PluginSlotMode"] = "edit-cards"` geaendert. Damit werden CarInfo und ServiceBook in
der Mitgliederverwaltung unter einem gemeinsamen Bootstrap-Card mit zwei inneren Tabs
("Fahrzeuge" und "Serviceheft") dargestellt. Beide Provider setzen bereits `GroupKey = "fahrzeuge"`
und `GroupTitle = "Fahrzeuge"`. Die frueherer Entscheidung vom 2026-06-18, die Mitgliederverwaltung
im `edit`-Modus zu belassen, wird mit dieser Aenderung aufgehoben.

### Rationale

ServiceBook ist inhaltlich eine Erweiterung von CarInfo: Serviceeintraege beziehen sich immer auf
ein Fahrzeug aus CarInfo. Die gemeinsame Karte macht diese Zugehoerigkeit in der Oberflaeche
sichtbar. Die technische Voraussetzung (GroupKey-Infrastruktur in `_PluginSlots.cshtml`) war
bereits vorhanden. Kein Core-Code erhaelt plugin-spezifische Kenntnisse; die Koordination
erfolgt ausschliesslich ueber den stabilen `GroupKey`-String "fahrzeuge".

Die Detailansicht (`Details.cshtml`) bleibt unveraendert im `details`-Modus ohne Gruppierung,
da keine `GroupKey`-Infrastruktur fuer `MemberDetailCardSlot` besteht und der Mehrwert gegenueber
dem Aufwand (neue Infrastruktur, neue Rendering-Branch) nicht gerechtfertigt ist.

---

## 2026-06-30 - Plugin-Abhaengigkeiten ueber DependenciesJson in PluginStatusRecord

### Decision

Fuer die Aktivierungspruefstufe wird ein generischer Abhaengigkeitsmechanismus eingefuehrt:

1. `PluginDependency` ist ein neuer `sealed record` in `Contracts/Plugin/` mit `ModuleId` (string)
   und `MinVersion` (Version). `TryParse` versteht das Format `"moduleId@>=x.y.z"`.
2. `PluginManifest` erhaelt eine nicht-positionale init-only Property
   `IReadOnlyList<PluginDependency> Dependencies` mit Default `Array.Empty<PluginDependency>()`.
3. `PluginManifestParser` liest das optionale `"dependencies"`-Array aus `plugin.json` und
   validiert jedes Element via `PluginDependency.TryParse`.
4. `PluginStatusRecord` erhaelt eine neue Spalte `DependenciesJson TEXT NOT NULL DEFAULT '[]'`,
   die via idempotenter SQLite-Migration (`202606300101_AddPluginDependencies`) angelegt wird.
5. `PluginInstallerService.PersistSuccessAsync` serialisiert `manifest.Dependencies` in diese
   Spalte beim Installieren/Upgraden.
6. `PluginLifecycleService.ActivateAsync` liest `existing.DependenciesJson` vor dem Assembly-Load
   und prueft fuer jede Abhaengigkeit: (a) Ist das Plugin im Runtime-Registry?
   (b) Ist die Laufzeitversion >= MinVersion? Bei Fehlschlag wird sofort `"dependency-not-met"`
   zurueckgegeben, ohne die DB zu schreiben.
7. `ServiceBook/plugin.json` deklariert `"dependencies": ["clubgear.plugin.carinfo@>=1.0.5"]`.

### Rationale

Die Aktivierungssperre muss vor dem Assembly-Load greifen, weil `module.Manifest.Dependencies`
erst nach `LoadAsync` zugaenglich ist. Eine DB-Spalte fuer `DependenciesJson` ist daher
erforderlich. Als autoritative Pruefquelle wird `_runtimeRegistry.GetByModuleId` verwendet (nicht
`_statusStore.GetByKey`), weil ein Eintrag mit `IsActive=true` in der DB nicht garantiert, dass
das Plugin tatsaechlich im Laufzeitspeicher vorhanden ist (z.B. nach einem Ladefehlschlag beim
Start). Die Minimalversion 1.0.5 entspricht der einzigen vorhandenen Quellevidenz im Codebase
(JS-Warnung in `ServiceBookProviders.cgcs`). `LoadActivatedAsync` wird nicht veraendert; die
Abhaengigkeitssperre gilt ausschliesslich fuer explizite Admin-Aktivierungsaufrufe.
Binary-Kompatibilitaet bleibt durch die nicht-positionale init-only Property erhalten;
bestehende Plugin-Assemblies (CarInfo, Inventar) benoetigen keine Neukompilierung.
