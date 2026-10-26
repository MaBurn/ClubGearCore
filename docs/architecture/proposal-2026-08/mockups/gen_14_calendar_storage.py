from _kit import *

o, top = chrome(active="Admin", eyebrow_text="Core · CORE-07  ·  calendar storage",
                title="Calendar storage",
                subtitle="Everything entered into a calendar is stored in Nextcloud. ClubGear keeps a rebuildable cache — no second master copy.",
                unread=3, actions=[("Rebuild cache", "outline"), ("Connection", "primary")])
out = [o]

# calendars
p, cy = panel(28, top, W - 56, 290, "Calendars", "Each calendar maps to one CalDAV collection")
out.append(p)
cols = [("Calendar", 220, "l"), ("Organisation unit", 180, "l"), ("CalDAV collection", 290, "l"),
        ("Storage", 150, "l"), ("Cache read", 150, "l"), ("Items", 80, "r"), ("", 54, "c")]
rows = [
    [{"t": "Vereinstermine", "weight": 600}, "Verein (root)", "/calendars/clubgear-svc/verein", {"t": "Nextcloud", "badge": "plugin"}, "09:41 · ctag 8f3a", "412", "⋯"],
    [{"t": "Regionalgruppe Süd", "weight": 600}, "RG Süd", "/calendars/clubgear-svc/rg-sued", {"t": "Nextcloud", "badge": "plugin"}, "09:41 · ctag 21c9", "128", "⋯"],
    [{"t": "Technik-Referat", "weight": 600}, "Technik-Referat", "/calendars/clubgear-svc/technik", {"t": "Nextcloud", "badge": "plugin"}, "09:41 · ctag 4b02", "54", "⋯"],
    [{"t": "Vereinsheim (offline)", "weight": 600}, "Verein (root)", {"t": "—", "muted": True}, {"t": "Local", "badge": "info"}, {"t": "n/a", "muted": True}, "9", "⋯"],
]
t, _ = table(29, cy + 6, W - 58, cols, rows)
out.append(t)
out.append(text(46, cy + 210, "Local mode remains available for installations that run without Nextcloud — it is not a second copy of a Nextcloud calendar.",
                size=11.5, fill=C["muted"]))

# field mapping
my = top + 306
p, cy = panel(28, my, 800, 450, "Where each field lives", "The VEVENT is the record — including who is registered")
out.append(p)
cols2 = [("ClubGear concept", 226, "l"), ("Stored in Nextcloud as", 400, "l"), ("", 172, "l")]
rows2 = [
    [{"t": "Title, description, place", "weight": 600}, "SUMMARY · DESCRIPTION · LOCATION", {"t": "standard", "badge": "ok"}],
    [{"t": "Start, end, time zone", "weight": 600}, "DTSTART / DTEND with TZID", {"t": "standard", "badge": "ok"}],
    [{"t": "Recurrence and exceptions", "weight": 600}, "RRULE · EXDATE · RECURRENCE-ID", {"t": "standard", "badge": "ok"}],
    [{"t": "Cancelled", "weight": 600}, "STATUS:CANCELLED", {"t": "standard", "badge": "ok"}],
    [{"t": "Organiser", "weight": 600}, "ORGANIZER", {"t": "standard", "badge": "ok"}],
    [{"t": "Registration", "weight": 600}, "ATTENDEE with PARTSTAT + RSVP", {"t": "standard", "badge": "ok"}],
    [{"t": "Waitlist position", "weight": 600}, "ATTENDEE;ROLE=OPT-PARTICIPANT + X-CLUBGEAR-*", {"t": "extension", "badge": "warn"}],
    [{"t": "Capacity", "weight": 600}, "X-CLUBGEAR-CAPACITY", {"t": "extension", "badge": "warn"}],
    [{"t": "Organisation unit, visibility", "weight": 600}, "X-CLUBGEAR-UNIT · CLASS · CATEGORIES", {"t": "extension", "badge": "warn"}],
]
t, _ = table(29, cy + 6, 798, cols2, rows2, row_h=36, head_h=34)
out.append(t)

# what clubgear keeps
p, cy = panel(844, my, W - 844 - 28, 450, "What ClubGear still holds", "None of it is authoritative calendar data")
out.append(p)
keep = [("Read cache / index", "ok", "Derived from CalDAV, discardable, rebuilt on demand.\nMakes range, unit and status filters usable without a\nround trip per query. Never written to directly."),
        ("Audit entries", "ok", "Who changed what in ClubGear. This is audit, not the\nappointment."),
        ("Notification records", "ok", "Delivery state of invitations and change notices."),
        ("Member ↔ attendee link", "warn", "The mapping from a ClubGear member id to the ATTENDEE\nentry, so a deletion request can be honoured.")]
ky = cy + 14
for title, kind, body in keep:
    out.append(rect(862, ky, W - 844 - 64, 78, C["surface"], C["panel_border"], rx=8))
    b, bw = badge(878, ky + 12, title, kind); out.append(b)
    for i, l in enumerate(body.split("\n")):
        out.append(text(878, ky + 48 + i * 15, l, size=10.8, fill=C["muted"]))
    ky += 88

# consequences
py = my + 466
p, cy = panel(28, py, W - 56, 210, "What follows from this — and what it costs",
              "These are consequences of the decision, not objections to it")
out.append(p)
cons = [
    ("Capacity is enforced optimistically", "danger",
     ["A registration is a CalDAV PUT with If-Match on the event ETag.",
      "On 412 the plugin re-reads, re-checks capacity and retries (bounded).",
      "Serialised per event — correct, but slower than a DB transaction."]),
    ("External edits can damage extensions", "danger",
     ["A phone or desktop client editing the event may drop unknown",
      "X- properties and attendee lines. Recommendation: share the managed",
      "calendars read-only and keep writes in ClubGear or the Nextcloud web UI."]),
    ("No Nextcloud, no calendar", "warn",
     ["By design there is no local fallback copy. If Nextcloud is unavailable,",
      "the calendar is read-only from cache and writes fail with a clear error.",
      "Backup and restore of the calendar become Nextcloud's responsibility."]),
    ("Personal data leaves the ClubGear database", "warn",
     ["Attendee names and addresses live in the Nextcloud instance.",
      "A member deletion request must purge ATTENDEE entries across all",
      "collections — hence the member ↔ attendee link kept on the right."]),
]
cw = (W - 92) / 4
for i, (title, kind, lines) in enumerate(cons):
    x = 46 + i * cw
    col = C["danger"] if kind == "danger" else C["warn"]
    for j, l in enumerate(wrap(title, 34)):
        out.append(text(x, cy + 22 + j * 16, l, size=12, fill=col, weight=700))
    body = " ".join(lines)
    for j, l in enumerate(wrap(body, 47)):
        out.append(text(x, cy + 58 + j * 16, l, size=10.6, fill=C["body"]))

write("14-calendar-storage.svg", out, W, 1180)
