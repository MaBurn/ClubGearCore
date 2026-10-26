# Finance-Plugin: Nächste Schritte

Vision: Das Finance-Plugin soll nicht nur Kontodaten von Mitgliedern verwalten, sondern **alle finanziellen Bewegungen des Vereins** abbilden — Beiträge, Spenden, Ausgaben, Vereinskonten, nicht nur mitgliedsbezogene Buchungen.

## Status: die ursprüngliche Liste (Schritte 0–8) ist mit Finance `2.0.0` vollständig abgearbeitet

Diese Datei listete ursprünglich neun offene Schritte. Stand `2.0.0` (2026-10-03,
`plugins/Finance/CHANGELOG.md`) sind alle erledigt:

| # | Schritt | Status |
|---|---|---|
| 0 | Laufende Iteration abschließen (Nav-Rechteprüfung, Self-Service-HTML, IBAN-Unmasking) | ✅ `1.6.0` |
| 1 | Payments-Schreibpfad | ✅ `1.9.0` |
| 2 | Vereinskonten & mitgliedsunabhängige Buchungen | ✅ `1.9.0` |
| 3 | Mitgliedsbeiträge automatisieren (Beitragslauf, Mahnwesen) | ✅ `1.9.0` |
| 4 | Einnahmen/Ausgaben-Kategorien & einfaches Budget | ✅ `2.0.0` |
| 5 | Kontoauszug-Import & Abgleich (CAMT.053/MT940) | ✅ `2.0.0` |
| 6 | SEPA-Lastschrift-Export (pain.008) | ✅ `1.9.0` |
| 7 | Berichte & Kassenprüfung | ✅ `2.0.0` |
| 8 | Rollen verfeinern (`treasurer.read`/`treasurer.write` statt einem Recht) | ✅ `2.0.0` (plugin-seitig; Core-Seite offen, siehe unten) |

Die einzige nicht rein plugin-interne Restarbeit aus Schritt 8 — den Permission-Key in **Core**
umzubenennen (`Services/Core/SeedTasks/RolePermissionSeedTask.cs`), zwei Core-Architekturtests
anzupassen und bestehende Rollen-Zuweisungen zu migrieren — ist bewusst nicht Teil dieser Iteration
und steht in [`../../plugins.md`](../../plugins.md) unter "Die `kassenwart` → `treasurer`-Rename ist
halb fertig".

## Vorschlag: Finance 2.1 Kandidaten (noch nicht priorisiert, nicht gescoped)

Aus der Arbeit an `2.0.0` ergeben sich diese sinnvollen nächsten Schritte — keiner davon ist
beauftragt, das ist eine Empfehlung zur Priorisierung durch den Vorstand/Owner:

1. **Core-seitige Rollen-Migration abschließen** (siehe oben). Ohne das bleibt der
   `kassenwart`-Permission-Key als toter Compile-Shim im Plugin liegen. P1, weil er die einzige
   noch offene Hälfte von FIN-01 ist.
2. **Budget-Reporting ausbauen**: Die Soll/Ist-Ansicht aus `2.0.0` ist pro Kategorie und Jahr; ein
   Mehrjahresvergleich (Vorjahresvergleich) wäre der nächste sinnvolle Schritt, baut direkt auf dem
   bestehenden Budget-Schema auf.
3. **Beleg-Anhänge an Buchungen**: Buchungen (`bookings`) haben noch keine Möglichkeit, einen
   gescannten Beleg/eine Rechnung anzuhängen. Für die Kassenprüfung ist das der naheliegende nächste
   Schritt nach dem CSV-Export aus Schritt 7 — nutzt die bestehende Core-Dateidienst-Fähigkeit
   (CORE-02 in `../../core-capabilities.md`), keine eigene Speicherung im Plugin.
4. **PDF neben CSV für den Kassenprüfungs-Export**: Heute liefert `finance.report.export` nur CSV.
   Ein PDF-Layout über die zentrale Core-Template-Engine (CORE-05) wäre für die
   Mitgliederversammlung direkter nutzbar als CSV.
5. **Statement-Matching-Heuristik verbessern**: Der Scoring-Matcher aus `2.0.0` ist bewusst einfach
   (IBAN/Betrag/Verwendungszweck). Sobald echte Kontoauszüge eingespielt wurden, lohnt sich eine
   Auswertung der Trefferquote, bevor hier investiert wird — nicht vorab spekulativ ausbauen.

Reihenfolge: 1 blockiert nichts der anderen vier, sollte aber zuerst gemacht werden, weil er der
einzige fällige Rest aus einer bereits zugesagten Iteration ist, nicht neuer Scope. 2–5 sind
unabhängig voneinander und können nach Bedarf einzeln angegangen werden.
