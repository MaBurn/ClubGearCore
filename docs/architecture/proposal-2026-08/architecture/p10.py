from dio import Page


def page_calendar_in_nextcloud():
    p = Page("10 · Calendar data lives in Nextcloud", 1700, 1120)
    p.text(40, 24, 1300, 30, "CORE-07 in Nextcloud mode — the VEVENT is the record", 20, "#0077BA", bold=True)
    p.text(40, 54, 1400, 40,
           "Instruction: everything entered into a calendar is stored in Nextcloud. ClubGear therefore has no events "
           "table and no registrations table for those calendars — only a cache it can throw away and rebuild.", 12)

    # write path
    p.band(40, 110, 1620, 300, "Write path — creating an event or registering for one")
    ui = p.node(70, 170, 200, 70, "ClubGear UI\nor self-service", "core", fontsize=11)
    svc = p.node(300, 170, 210, 70, "CalendarService\npermission + policy check", "core_new", fontsize=11)
    port = p.node(540, 170, 190, 70, "ICalendarStore\nupsert(item, ifMatch)", "contract", fontsize=11, bold=True)
    plug = p.node(760, 170, 210, 70, "Nextcloud plugin\nCalDAV client", "plugin", fontsize=11)
    get = p.node(1000, 150, 190, 50, "GET event\nread current ETag", "plugin", fontsize=11)
    chk = p.node(1000, 212, 190, 50, "Check capacity\nagainst ATTENDEE list", "plugin", fontsize=11)
    put = p.node(1220, 180, 190, 56, "PUT  If-Match: ETag", "plugin", fontsize=11, bold=True)
    nc = p.node(1450, 170, 180, 70, "Nextcloud\nCalDAV collection", "ext", fontsize=11, bold=True)
    retry = p.node(1220, 262, 190, 56, "412 → re-read,\nre-check, retry (bounded)", "warn", fontsize=11)
    cache = p.node(760, 300, 210, 60, "Update read cache\n+ emit notification event", "core_new", fontsize=11)

    E = p.edge
    E(ui, svc); E(svc, port); E(port, plug); E(plug, get); E(get, chk); E(chk, put); E(put, nc)
    E(nc, retry, "412 conflict", "#c98a1e", dashed=1, exitp=(0.5, 1), entryp=(1, 0.5))
    E(retry, get, "", "#c98a1e", dashed=1, exitp=(0, 0.5), entryp=(0.5, 1))
    E(nc, cache, "201 / 204", "#187344", dashed=1, exitp=(0.5, 1), entryp=(1, 0.5))

    p.text(70, 300, 660, 90,
           "Why the loop: with Nextcloud as the record there is no database transaction to hold a capacity\n"
           "limit. The limit is enforced optimistically — read ETag, verify free places, conditional write.\n"
           "Correct, and serialised per event; the cost is latency and a retry under concurrent sign-ups.", 11.5)

    # read path
    p.band(40, 440, 800, 290, "Read path — a calendar query")
    q = p.node(70, 500, 200, 60, "Query\nrange · unit · status", "core", fontsize=11)
    idx = p.node(300, 500, 220, 60, "Read cache (index)\nderived, discardable", "core_new", fontsize=11, bold=True)
    ct = p.node(560, 490, 240, 80, "ctag / sync-token changed?\nsync-collection REPORT\nfetch only what moved", "plugin", fontsize=11)
    E(q, idx); E(idx, ct, "on miss or stale", "#0077BA", dashed=1)
    p.text(70, 590, 740, 120,
           "The cache exists because CalDAV cannot answer »all events of unit X in September where I am\n"
           "registered« without fetching and parsing the collection. It holds no authority: »Rebuild cache«\n"
           "drops and refetches it, and a disagreement is always resolved in favour of Nextcloud.\n\n"
           "If Nextcloud is unreachable the calendar is readable from cache, flagged stale, and writes fail.", 11.5)

    # not in clubgear
    p.band(880, 440, 780, 290, "What this removes from the ClubGear schema")
    p.node(910, 490, 340, 50, "No events table", "danger", fontsize=11, bold=True)
    p.node(910, 550, 340, 50, "No registrations table", "danger", fontsize=11, bold=True)
    p.node(910, 610, 340, 50, "No waitlist table", "danger", fontsize=11, bold=True)
    p.text(1280, 490, 360, 180,
           "…for calendars in Nextcloud mode.\n\n"
           "Local mode keeps the tables and stays available for\n"
           "installations without Nextcloud. The two modes share\n"
           "the same service and the same UI; only the store\n"
           "implementation differs.", 11.5)

    # consequences
    p.band(40, 760, 1620, 320, "Consequences to accept with this decision")
    cons = [
        ("Capacity is optimistic, not transactional", "#b05252",
         "Enforced by ETag + If-Match with a bounded retry. Two members\n"
         "hitting the last free place still resolve correctly, but one gets a\n"
         "retry and, in the worst case, a »place just taken« message."),
        ("Foreign clients can damage extensions", "#b05252",
         "Capacity, unit and waitlist ride on X- properties. A phone or desktop\n"
         "CalDAV client editing the event may drop unknown properties or\n"
         "attendee lines. Recommendation: share managed calendars read-only\n"
         "and keep writes in ClubGear or the Nextcloud web UI."),
        ("Availability and backup move to Nextcloud", "#c98a1e",
         "There is deliberately no local master copy. Nextcloud downtime means\n"
         "a read-only, stale calendar. Deleting the collection in Nextcloud\n"
         "deletes the data — restoring it is a Nextcloud restore."),
        ("Personal data leaves the ClubGear database", "#c98a1e",
         "Attendee names and addresses live in the Nextcloud instance. A member\n"
         "deletion request must purge ATTENDEE entries across all collections,\n"
         "which is why the member ↔ attendee link stays in ClubGear."),
    ]
    x = 70
    for title, color, body in cons:
        p.text(x, 800, 370, 40, title, 12.5, color, bold=True)
        p.text(x, 840, 370, 160, body, 11)
        x += 400
    return p
