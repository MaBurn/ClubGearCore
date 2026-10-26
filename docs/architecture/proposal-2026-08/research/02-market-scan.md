# Market scan — candidate features not in the current backlog

Method: web research on German association-management software and on the legal obligations that
apply to a registered association (e.V.) in 2025–2026. The point is not to copy competitors but to
find capabilities that a ClubGear installation will be asked for and that would be expensive to
retrofit into the wrong architecture.

Each item is rated by how much it would change the architecture if adopted late.

---

## A. Legally driven — these arrive whether or not they are planned

### A1. Digital and hybrid general meeting, with resolutions and voting
**Retrofit cost: high.** Since the 2023 amendment, §32(2) BGB permits hybrid meetings by board
decision and purely virtual meetings by member resolution, without a statutory amendment. A 2024
decision of AG Spandau held that a mere voting tool is not sufficient as the channel of
communication — real participation rights (speech, motions, voting) must be exercisable.

Fits ClubGear as a plugin over CORE-07 (event + registration), CORE-06 (motion lifecycle) and
CORE-04 (invitations, notice periods). What must be decided **now** is whether the calendar event
model carries attendance, proxy and quorum concepts, because adding them later touches the
registration table and its concurrency rules.

Sources: [Bundestag](https://www.bundestag.de/dokumente/textarchiv/2023/kw06-de-digitale-mitgliederversammlung-931492) ·
[vereinsrecht.de](https://www.vereinsrecht.de/aenderung-des-vereinsrechts-beschlossen-hybride-und-virtuelle-mitgliederversammlungen-auch-ohne-satzungsregelung-moeglich.html) ·
[DSEE Rechtstipp](https://www.deutsche-stiftung-engagement-und-ehrenamt.de/dseerechtstipp/virtuelle-mitgliederversammlung-was-jetzt-gilt/) ·
[Haufe](https://www.haufe.de/recht/weitere-rechtsgebiete/wirtschaftsrecht/gesetzesaenderung-virtuelle-mitgliederversammlung-im-verein_210_590540.html)

### A2. E-invoicing (E-Rechnung, EN 16931 / XRechnung / ZUGFeRD)
**Retrofit cost: medium.** Since 1 January 2025 every association that buys from a business must be
able to **receive** and archive structured e-invoices. The obligation to **issue** them phases in
from 1 January 2027 (prior-year turnover above €800,000) and 1 January 2028 for all domestic B2B
invoices. A PDF is not an e-invoice. Membership fees, donations and B2C invoices are exempt.

For ClubGear this is a Finance-plugin concern (sponsoring, advertising, rentals), not a core
concern — provided the file storage service (CORE-02) can hold an immutable, audit-retained
document and Finance can attach a structured payload to a booking. Both are already in scope.

Sources: [easybill](https://www.easybill.de/ratgeber/e-rechnung-im-verein/) ·
[ClubDesk](https://www.clubdesk.de/de/vereinswissen/e-rechnung-im-verein-was-euer-verein-beachten-muss) ·
[Hamburger Software](https://www.hamburger-software.de/blog/artikel/e-rechnungspflicht-fuer-vereine-gesetzliche-grundlagen-ausnahmen-praxistipps) ·
[vereinsplaner.de](https://vereinsplaner.de/c/e-rechnung-fuer-deutsche-vereine)

### A3. Digital accessibility (BFSG)
**Retrofit cost: very high if late — it is a property of the rendering layer.** The
Barrierefreiheitsstärkungsgesetz applies from 28 June 2025. It references EN 301 549, which in turn
requires WCAG 2.1 level AA, plus a published accessibility statement. Associations are affected
where they offer paid services, online shops or booking systems; offerings covered purely by the
membership fee are generally out of scope.

The architectural consequence for this proposal is direct: because CORE-03 makes the **host** render
all plugin surfaces, accessibility can be solved once in the host renderer instead of in every
plugin. That is an argument for the CORE-03 design, and a reason not to allow plugins to return raw
HTML.

Sources: [Aktion Mensch](https://www.aktion-mensch.de/inklusion/barrierefreiheit/barrierefreie-website/gesetzliche-pflichten) ·
[IHK Stuttgart](https://www.ihk.de/stuttgart/fuer-unternehmen/recht-und-steuern/it-recht/barrierefreie-webseiten-6200594) ·
[DOSB](https://www.dosb.de/aktuelles/news/detail/digitale-barrierefreiheit-wird-pflicht-auch-fuer-sportvereine) ·
[deutsches-ehrenamt.de](https://deutsches-ehrenamt.de/vereinsrecht-fuehrung/verein-barrierefrei/)

### A4. Donation receipts (Zuwendungsbestätigung)
**Retrofit cost: low.** A donation above €300 requires an association-issued confirmation on the
official pattern. This is a Finance-plugin document generated from a core template event — it needs
nothing new architecturally once CORE-05 and CORE-02 exist.

Sources: [vostel.de](https://vostel.de/blog/spendenbescheinigung/) ·
[vereinsplaner.de](https://vereinsplaner.de/c/spendenbescheinigung-verein-deutschland)

---

## B. Expected by users — common in the 2026 market

| # | Feature | Where it belongs | Retrofit cost |
|---|---|---|---|
| B1 | **Online membership application** with digital SEPA mandate, feeding the approval workflow | Core self-service + CORE-06 + Finance | medium — needs a public, unauthenticated form surface, which no current ClubGear extension point provides |
| B2 | **Digital membership card** (wallet pass, QR check-in) | Plugin over member + CORE-02 | low |
| B3 | **Course, training and resource booking** (Kurse, Platz-/Raumbuchung) | Plugin over CORE-07 | medium — the backlog explicitly defers room/resource booking; keep the event model open for it |
| B4 | **Anniversary and honours automation** (Ehrungen, Jubiläen) | Small plugin over CORE-04/05 | low — same shape as the legacy birthday greetings |
| B5 | **Expense reimbursement / Ehrenamtspauschale** | Finance plugin | low |
| B6 | **Federation reporting** (Bestandserhebung / LSB statistics export) | Core report or small plugin | low, if membership types and units are modelled properly — which CORE-01 and CORE-08 do |
| B7 | **Accounting export** (DATEV, EÜR) | Finance plugin | low |
| B8 | **Mobile app / push notifications** | Channel-provider plugin over CORE-04 | low — this is precisely the case the CORE-04 provider contract is designed for |

Market context: easyVerein, Clubway, Klublink, Kurabu and comparable products converge on
membership administration, SEPA collection, accounting, communication, online application, digital
membership card and event/course booking as the expected baseline.

Sources: [trusted.de](https://trusted.de/vereinsverwaltung) ·
[klublink.de](https://klublink.de/beste-vereinssoftware-2026.php) ·
[vereinsbühne](https://vereinsbuehne.de/vereinssoftware-vergleich/) ·
[Kurabu digital membership card](https://www.kurabu.com/digitaler-mitgliedsausweis) ·
[OMR Reviews](https://omr.com/de/reviews/contenthub/software-fuer-mitgliederverwaltung)

---

## C. Platform hygiene worth naming explicitly

| # | Item | Rationale |
|---|---|---|
| C1 | **GDPR self-service: data export, deletion request, consent versioning** | The backlog mentions retention and privacy per feature, but no single subject-access workflow. Cheap to add while CORE-01/04/08 are being designed; expensive once eight modules hold personal data. |
| C2 | **Second factor / passkeys for privileged accounts** | Treasurer and administrator accounts will hold bank data (FIN-01). Belongs to the existing account/IAM area, not to a new module. |
| C3 | **Per-tenant or per-unit data export for handover** | Boards change. A unit administrator leaving should be able to hand over a complete, auditable export. Fits CORE-01 + CORE-12. |

---

## Recommendation on scope

Do **not** add any of these to the current backlog now. Two of them, however, should influence the
P0 design decisions while those decisions are still cheap:

1. **A3 (accessibility)** confirms that the host must own rendering and that plugin-supplied raw
   HTML stays forbidden — a CORE-03 constraint, not a later project.
2. **A1 (digital general meeting)** suggests that the CORE-07 event model should not hard-code
   "registration = attendance". Keeping attendance, proxy and quorum as separable concepts costs
   almost nothing now.

Everything else in section B can be added later as plugins without touching the architecture, which
is the point of the proposed boundary.
