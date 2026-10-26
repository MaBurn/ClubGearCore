# Finance-Plugin: CRISPY-Läufe aus nextSteps.md

Die 8 Punkte aus `nextSteps.md` sind hier zu **7 thematisch zusammenhängenden CRISPY-Läufen** gruppiert. Jeder Block ist als ein eigener `/crispy:start`-Aufruf gedacht (eigene Q→R→A→S→P→I-Pipeline, eigener Design-Gate). Reihenfolge = empfohlene Abarbeitungsreihenfolge, da spätere Läufe auf Ergebnissen früherer aufbauen (siehe Abhängigkeit je Block).

---

## 1. Laufende Bugfix-Iteration abschließen
*(entspricht nextSteps.md Punkt 0 — Voraussetzung für alles Weitere, da sonst auf inkonsistentem Stand aufgebaut wird)*

```
/crispy:start Die unfertige Iteration aus ADRs/consolidated/finance-plugin.adr.md fertigstellen: (1) PluginNavEntryService soll Berechtigungen wirklich über IPermissionService/DB-Rollen prüfen statt nur über einen Claim- und Wildcard-Check, damit der Kassenwart-Navigationseintrag korrekt erscheint. (2) FinanceSelfServiceSectionProvider.BuildSelfServiceHtml im Self-Service-Bereich bereinigen (kein <hr/>, keine doppelte Überschrift, kompakter Statusblock, kontextabhängiges Button-Label) - aktuell nur im Host vorbereitet, im Plugin selbst noch nicht umgesetzt. (3) IBAN-Unmasking für Kassenwart einbauen: FinanceEditTabProvider, FinanceDetailCardProvider und FinanceKassenwartPageProvider sollen über hostContext.Permissions.HasPermissionAsync prüfen und bei Berechtigung die volle IBAN statt der maskierten Version zeigen - die Host-Infrastruktur (IPluginPermissionFacade) existiert bereits, wird aber nirgends aufgerufen. Danach Version auf 1.6.0, alle drei betroffenen Plugins (Finance, CarInfo, ServiceBook) neu paketieren, da sie dieselbe Contracts-DLL teilen.
```

---

## 2. Zahlungen tatsächlich erfassen können
*(entspricht Punkt 1 — kleinster Schritt mit größtem sofortigen Nutzen, keine Schemaänderung nötig)*

```
/crispy:start Im Finance-Plugin existiert bereits eine "payments"-Tabelle (Betrag, Datum, Kategorie, Verwendungszweck, Gegenkonto-IBAN, Status), aber es gibt keinen Schreibpfad dafür - aktuell wird nur gelesen (GetRecentPaymentsAsync). Ich möchte, dass der Kassenwart eine Zahlung eines Mitglieds tatsächlich erfassen kann (Betrag, Datum, Kategorie, Verwendungszweck, optional Gegenkonto-IBAN), sichtbar sowohl in der Mitgliedsakte als auch im Kassenwart-Bereich, inkl. Audit-Trail wie bei den Bankverbindungen.
```

---

## 3. Vereinskonten & mitgliedsunabhängige Buchungen
*(entspricht Punkt 2 — die einzige strukturelle Weichenstellung; alle folgenden Läufe (4-7) setzen dieses Datenmodell voraus)*

```
/crispy:start Aktuell ist im Finance-Plugin jede Tabelle zwingend an eine MemberId gebunden - es gibt kein Konzept für Buchungen ohne Mitgliedsbezug (Miete, Versicherung, Vereinsfest-Kosten, Spenden von Nicht-Mitgliedern) und keine eigenen Vereinskonten (z.B. "Hauptkonto", "Kasse", "Sparkonto") getrennt von Mitglieds-Bankverbindungen. Ich möchte das Datenmodell so erweitern, dass Buchungen optional ohne Mitgliedsbezug erfasst werden können und der Verein mehrere eigene Konten führen kann, denen Buchungen zugeordnet werden. Das ist eine bewusste strukturelle Entscheidung - bitte Optionen (MemberId nullable machen vs. neue allgemeinere Buchungstabelle) im Design gegeneinander abwägen.
```

---

## 4. Mitgliedsbeiträge & SEPA-Lastschrifteinzug
*(entspricht Punkt 3 + 6 zusammengefasst — Beitragsverwaltung und ihr Einzugsweg gehören inhaltlich zusammen; baut auf Lauf 2 und 3 auf)*

```
/crispy:start Mitgliedsbeiträge sollen automatisiert verwaltet werden: wiederkehrende Beitragsforderungen je Mitglied generieren (abhängig vom Mitgliedschaftstyp, jährlich/quartalsweise), offene und bezahlte Beiträge anzeigen, einfaches Mahnwesen bei Fälligkeitsüberschreitung. Zusätzlich soll der tatsächliche Einzug per SEPA-Lastschrift unterstützt werden: aktuell wird nur eine SepaDirectDebitId je Mitglied gespeichert, aber nie etwas damit erzeugt - ein SEPA-XML-Export (pain.008) für den Beitragseinzug soll auf Basis der offenen Forderungen und hinterlegten Mandate erzeugt werden können, direkt nutzbar für den Upload beim Kreditinstitut.
```

---

## 5. Kontenrahmen, Budget & Kassenbericht
*(entspricht Punkt 4 + 7 zusammengefasst — Kategorien sind die Voraussetzung für jeden Bericht, daher gemeinsam geplant; sinnvoll erst, wenn über Lauf 2-4 echte Buchungsdaten anfallen)*

```
/crispy:start Für das Finance-Plugin einen konfigurierbaren Kontenrahmen (Kategorie-Liste, z.B. Beiträge, Spenden, Miete, Veranstaltungen, Ausrüstung) einführen, den bestehende und neue Buchungen referenzieren. Darauf aufbauend ein einfaches Jahresbudget je Kategorie mit Soll/Ist-Vergleich für den Vorstand, sowie eine Kassenbericht/Jahresabschluss-Ansicht (Einnahmen/Ausgaben nach Kategorie, wählbarer Zeitraum) mit CSV/PDF-Export für Kassenprüfer und Mitgliederversammlung.
```

---

## 6. Kontoauszug-Import & Abgleich
*(entspricht Punkt 5 — eigenständiges technisches Thema (Bankschnittstelle/Parsing), reduziert danach den manuellen Erfassungsaufwand aus Lauf 2)*

```
/crispy:start Für das Finance-Plugin einen Import von echten Kontoauszügen des Vereinskontos ermöglichen (CAMT.053 oder MT940-Format), der die enthaltenen Buchungen einliest und automatisch Abgleichsvorschläge gegen bereits erfasste, offene Buchungen macht (Abgleich über IBAN, Betrag, Verwendungszweck). Der Kassenwart soll Vorschläge manuell bestätigen oder ablehnen können, um doppelte Erfassung zu vermeiden.
```

---

## 7. Rollen verfeinern
*(entspricht Punkt 8 — quer zu den fachlichen Themen, daher bewusst als eigener, kleiner Lauf am Ende; kann bei Bedarf auch vorgezogen werden, falls Kassenprüfer-Zugriff früher gebraucht wird)*

```
/crispy:start Aktuell gibt es im Finance-Plugin im Wesentlichen nur ein einziges Recht ("Kassenwart-Zugriff"). Die Rollen sollen verfeinert werden: Kassenwart (volle Schreibrechte), Vorstand (Leserechte auf Berichte/Kassenbericht), Kassenprüfer (befristeter Lesezugriff, z.B. nur im Zeitraum vor der Mitgliederversammlung aktivierbar).
```

---

## Hinweis zur Nutzung

Jeden Block einzeln per `/crispy:start <Text im Codeblock>` starten, den Durchlauf bis zum Ende (inkl. `08_changelog.md`) durchlaufen lassen, bevor der nächste Block gestartet wird - die CRISPY-Artefakte unter `.crispy/` werden pro Lauf überschrieben bzw. sind nur für einen Lauf gültig. Lauf 1 sollte in jedem Fall zuerst passieren, Lauf 3 vor 4/5/6, da diese strukturell darauf aufbauen.
