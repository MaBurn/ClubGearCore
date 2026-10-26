from _kit import *

o, top = chrome(active="Calendar", eyebrow_text="Core · CORE-07  (+ CalDAV connector plugin)",
                title="Calendar and events",
                subtitle="Events, recurring meetings and registrations live in core. Nextcloud/CalDAV synchronisation is a plugin.",
                unread=3, actions=[("Export .ics", "outline"), ("New event", "primary")])
out = [o]

# filter bar
p, cy = panel(28, top, W - 56, 76, None)
out.append(p)
cxp = 46
for lab, on in [("All units", False), ("Regionalgruppe Süd", True), ("Technik-Referat", False), ("Redaktion", False)]:
    c, cw = chip(cxp, top + 26, lab, on); out.append(c); cxp += cw + 8
cxp += 24
for lab, on in [("Meetings", True), ("Events", True), ("Registered by me", False)]:
    c, cw = chip(cxp, top + 26, lab, on); out.append(c); cxp += cw + 8
out.append(text(W - 200, top + 44, "‹  September 2026  ›", size=14, fill=C["brand"], weight=700, anchor="middle"))

# month grid
gx, gy, gw = 28, top + 92, 860
p, cy = panel(gx, gy, gw, 470, None)
out.append(p)
days = ["Mo", "Tu", "We", "Th", "Fr", "Sa", "Su"]
cw = (gw - 32) / 7
for i, d in enumerate(days):
    out.append(text(gx + 16 + i * cw + cw / 2, gy + 26, d.upper(), size=10.5, fill=C["muted"], weight=700, anchor="middle", ls="0.8"))
evs = {
    2: [("Stammtisch", "info")], 5: [("Werkstatt-AG", "info")],
    9: [("Stammtisch", "info"), ("Vorstand", "warn")],
    12: [("Ausfahrt Süd", "ok")], 16: [("Stammtisch", "info")],
    19: [("Redaktion", "plugin")], 23: [("Stammtisch", "info")],
    26: [("JHV 2026", "warn")], 30: [("Stammtisch", "info")],
}
rh = 76
start_off = 1
for w in range(5):
    for dcol in range(7):
        n = w * 7 + dcol + 1 - start_off
        x = gx + 16 + dcol * cw
        y = gy + 38 + w * rh
        out.append(rect(x, y, cw - 4, rh - 6, C["white"] if 1 <= n <= 30 else C["surface"], C["hairline"], rx=6))
        if 1 <= n <= 30:
            today = n == 30
            if today:
                out.append(rect(x, y, cw - 4, rh - 6, "rgba(0,119,186,0.06)", C["brand"], rx=6))
            out.append(text(x + 8, y + 18, str(n), size=11.5, fill=C["brand"] if today else C["muted"], weight=700 if today else 400))
            for k, (title, kind) in enumerate(evs.get(n, [])):
                pal = {"info": ("#dbeaf4", C["brand_dark"]), "ok": (C["success_bg"], C["success"]),
                       "warn": (C["warn_bg"], C["warn"]), "plugin": ("#efe6f7", "#5b3d86")}[kind]
                out.append(rect(x + 6, y + 26 + k * 22, cw - 16, 19, pal[0], rx=4))
                out.append(text(x + 12, y + 39 + k * 22, title[:14], size=10.5, fill=pal[1], weight=600))

# right: event detail
dx, dw = 904, W - 904 - 28
p, cy = panel(dx, gy, dw, 470, "Jahreshauptversammlung 2026", "26.09.2026 · 19:00–22:00 (Europe/Berlin) · Vereinsheim")
out.append(p)
b, _ = badge(dx + 18, cy + 6, "Regionalgruppe Süd", "info"); out.append(b)
b, _ = badge(dx + 158, cy + 6, "hybrid", "plugin"); out.append(b)

out.append(text(dx + 18, cy + 56, "Registration", size=13, fill=C["ink"], weight=700))
out.append(text(dx + 18, cy + 78, "Capacity 80 · 62 confirmed · 4 waitlist", size=12.5, fill=C["muted"]))
out.append(progress(dx + 18, cy + 90, dw - 36, 78))
b, _ = btn(dx + 18, cy + 112, "Register", "primary", w=110); out.append(b)
b, _ = btn(dx + 136, cy + 112, "Guest +1", "outline", w=94); out.append(b)

out.append(line(dx, cy + 166, dx + dw, cy + 166))
out.append(eyebrow(dx + 18, cy + 190, "Recurrence"))
out.append(text(dx + 18, cy + 212, "Series: 1st Wednesday monthly", size=12.5))
out.append(text(dx + 18, cy + 232, "This instance was moved from 02.09.", size=12.5, fill=C["muted"]))
out.append(text(dx + 18, cy + 252, "01.10. cancelled (exception)", size=12.5, fill=C["muted"]))

out.append(line(dx, cy + 272, dx + dw, cy + 272))
out.append(eyebrow(dx + 18, cy + 296, "Stored in"))
out.append(text(dx + 18, cy + 318, "Nextcloud »Verein Süd«  ·  VEVENT is the record", size=12.5))
b, _ = badge(dx + 18, cy + 328, "storage plugin · cache read 09:40", "plugin"); out.append(b)
out.append(text(dx + 18, cy + 372, "UID nc-8831-a4 · ETag 8f3a · 62 ATTENDEE entries", size=11.5, fill=C["muted"]))
out.append(text(dx + 18, cy + 392, "Registrations are written back as ATTENDEE/PARTSTAT.", size=11.5, fill=C["muted"]))

n, _ = note(28, gy + 486, W - 56,
            ["Core owns the event model, permissions and policy; in Nextcloud mode the data itself — including the attendee list — lives in the CalDAV collection.",
             "ClubGear keeps a rebuildable cache, the member ↔ attendee link, audit and notification records. Local mode stays available for installations without Nextcloud."])
out.append(n)

write("08-calendar.svg", out, W, 900)
