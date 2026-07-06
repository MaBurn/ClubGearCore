# Member Legacy Compatibility (v1)

## Ziel

Diese erste Doku beschreibt den aktuellen Migrationspfad, mit dem bestehende ClubManager-Daten auf das aktuelle ClubGear-Modell angehoben werden koennen, ohne dass ein manueller SQL-Import notwendig ist.

Der Fokus liegt auf Basisfunktionen rund um Mitgliederdaten und einem sicheren Start mit bestehender SQLite-Datenbank.

## Kontext

ClubGear verwendet aktuell keinen klassischen EF-Migrationsordner, sondern fuehrt beim Start einen kompatibilitaetsorientierten Schema- und Datenabgleich aus.

Die zentrale Logik liegt in:
- Services/Core/ApplicationSeeder.cs

## Aktuelle Strategie

Beim App-Start werden in dieser Reihenfolge ausgefuehrt:

1. EnsureCreated auf der Ziel-Datenbank.
2. SQLite-kompatibler Schemaabgleich fuer Members und MemberAddresses.
3. Legacy-Mapping Alt -> Neu fuer zentrale Member-Felder.
4. Seed-Tasks fuer Demo-/Systemdaten (nur soweit noetig).

## Was bereits kompatibel gemacht wurde

### Schema-Erweiterung Members

Fehlende Basisspalten werden automatisch per ALTER TABLE ergaenzt, unter anderem:

- IsVerified
- MemberNumber
- PhoneNumber
- IsActive
- JoinedAt
- Joined
- Leaved
- LastUpdated
- RIP
- MembershipDiscount
- FamilyMembership
- MainMemberId
- NotifyViaEmail
- NotifyViaMatrix
- DataprivacyAccepted
- NewsletterConsent
- ProfileImagePath
- PendingEmail
- EmailVerificationToken
- EmailVerificationTokenExpiry
- RentalPayoutOptions
- InitPassword
- KeycloakUsername
- ApplicationUserId

### Legacy-Mapping Members

Folgende Alt-Felder werden auf neue Basisfelder gemappt:

- IsNotVerifyed -> IsVerified (invertierte Logik)
- MembershipNumber -> MemberNumber

Zusaetzlich werden Fallbacks gesetzt, damit Pflichtdaten konsistent sind:

- MemberNumber wird bei Bedarf aus Id erzeugt (Format M-0001)
- FirstName wird auf CompanyName oder ClubName oder Unbekannt gesetzt
- LastName wird auf - gesetzt, falls leer
- JoinedAt wird aus Joined oder LastUpdated gefuellt

### Telefonnummern

Wenn eine alte Tabelle PhoneNumbers existiert, wird die erste gueltige Nummer je Mitglied nach Members.PhoneNumber uebernommen, sofern dort noch kein Wert steht.

### Adressen

Wenn eine alte Tabelle Addresses existiert, werden Eintraege in MemberAddresses uebernommen (inkl. IsDefault), ohne bestehende Zieldatensaetze zu duplizieren.

## Basis-Mapping Uebersicht

| Alt (ClubManager) | Neu (ClubGear) | Status |
|---|---|---|
| IsNotVerifyed | IsVerified | implementiert |
| MembershipNumber | MemberNumber | implementiert |
| PhoneNumbers.Number | PhoneNumber | implementiert (erste Nummer) |
| Addresses.* | MemberAddresses.* | implementiert |
| DateOfBirth | DateOfBirth | direkt kompatibel |
| Gender | Gender | direkt kompatibel |
| IsCompany | IsCompany | direkt kompatibel |
| CompanyName | CompanyName | direkt kompatibel |
| IsClub | IsClub | direkt kompatibel |
| ClubName | ClubName | direkt kompatibel |
| Joined | Joined | direkt kompatibel |
| Leaved | Leaved | direkt kompatibel |
| RIP | RIP | direkt kompatibel |
| MembershipDiscount | MembershipDiscount | direkt kompatibel |
| FamilyMembership | FamilyMembership | direkt kompatibel |
| MainMemberId | MainMemberId | direkt kompatibel |
| DataprivacyAccepted | DataprivacyAccepted | direkt kompatibel |
| NewsletterConsent | NewsletterConsent | direkt kompatibel |
| ProfileImagePath | ProfileImagePath | direkt kompatibel |
| PendingEmail | PendingEmail | direkt kompatibel |
| EmailVerificationToken | EmailVerificationToken | direkt kompatibel |
| EmailVerificationTokenExpiry | EmailVerificationTokenExpiry | direkt kompatibel |
| RentalPayoutOptions | RentalPayoutOptions | direkt kompatibel |

## Noch nicht im Basis-Upgrade enthalten

Diese Bereiche sind im aktuellen v1-Pfad noch nicht automatisch migriert:

- BankAccounts
- Payments
- Vehicles
- GPSTrackers
- MembershipType und MembershipTypeID
- CommunicationChannel und CommunicationPreferences
- LocalUnits
- FamilyMembers als eigene Beziehungstabelle

## Betriebs-Hinweise

1. Backup der Datenbank vor dem ersten Start mit neuer Version erstellen.
2. Anwendung starten.
3. Seeder fuehrt kompatibilitaetsrelevante SQL-Schritte automatisch aus.
4. Danach Member-Ansichten pruefen (Liste, Bearbeitung, Verifizierung).

## Risiko- und Qualitäts-Hinweise

- Der Ansatz ist bewusst defensiv und nullable-freundlich ausgelegt.
- Legacy-Daten mit stark abweichenden Spaltennamen ausserhalb der dokumentierten Faelle werden nicht automatisch erkannt.
- Fuer komplexe Altmodule sollte ein zweiter, expliziter Upgrade-Block pro Modul folgen.

## Naechste Ausbaustufe (v2)

Empfohlene Reihenfolge:

1. Vehicles
2. BankAccounts
3. Payments
4. MembershipType
5. GPSTrackers
6. Kommunikationspraeferenzen

Pro Modul:
- Tabellen-Existenz pruefen
- Spalten robust aufloesen
- Daten idempotent uebernehmen
- Funktionalitaet in UI und Service testen
