from _kit import *

o, top = chrome(active="Plugins", eyebrow_text="Plugin · NEWS-01  ·  new plugin",
                title="Campaign  ·  Rundbrief 09/2026",
                subtitle="Consent-aware campaigns on top of core templates and channels. Listmonk is an optional adapter, not a premise.",
                unread=3, actions=[("Dry run", "outline"), ("Request approval", "primary")])
out = [o]
b, _ = badge(310, 82, "draft · created by M. Brenne", "grey"); out.append(b)

# composer
p, cy = panel(28, top, 620, 372, "Composition")
out.append(p)
f, y1 = field(46, cy + 4, 290, "Template event key", "news.rundbrief (v4, de-DE)", kind="select"); out.append(f)
f, _ = field(352, cy + 4, 278, "Locale policy", "member locale, fallback de-DE", kind="select"); out.append(f)
f, y2 = field(46, y1 + 14, 290, "Sender", "vorstand@musterverein.de"); out.append(f)
f, _ = field(352, y1 + 14, 278, "Reply-to", "redaktion@musterverein.de"); out.append(f)
f, y3 = field(46, y2 + 14, 290, "Scheduled", "05.09.2026 08:00"); out.append(f)
f, _ = field(352, y2 + 14, 278, "Batch size / rate", "200 per 5 min", kind="select"); out.append(f)
out.append(text(46, y3 + 36, "Attachments", size=12, fill=C["muted"], weight=600))
out.append(rect(46, y3 + 46, 290, 38, C["surface"], C["input_border"], rx=6))
out.append(text(60, y3 + 70, "📎  Rundbrief-09-2026.pdf · 1.8 MB", size=12, fill=C["body"]))
b, _ = badge(352, y3 + 56, "stored via CORE-02", "info"); out.append(b)

# recipients
p, cy = panel(664, top, W - 664 - 28, 372, "Recipient snapshot", "Materialised and frozen before sending")
out.append(p)
out.append(text(682, cy + 34, "1 284", size=30, fill=C["ink"], weight=700))
out.append(text(770, cy + 34, "recipients selected", size=13, fill=C["muted"]))
seg = [("Membership status = active", "+1 402"), ("Organisation unit = all", "—"),
       ("Explicit exclusions", "−12"), ("Opted out", "−74", "warn"),
       ("Suppressed (bounced/invalid)", "−28", "bad"), ("No e-mail on file", "−4", "grey")]
sy = cy + 62
for s in seg:
    out.append(text(682, sy, s[0], size=12.5, fill=C["body"]))
    col = {"warn": C["warn"], "bad": C["danger"], "grey": C["muted"]}.get(s[2] if len(s) > 2 else "", C["muted"])
    out.append(text(W - 46, sy, s[1], size=12.5, fill=col, weight=600, anchor="end"))
    sy += 26
out.append(line(682, sy + 4, W - 46, sy + 4))
out.append(text(682, sy + 28, "Households collapsed to one recipient", size=12.5, fill=C["muted"]))
out.append(text(W - 46, sy + 28, "−36", size=12.5, fill=C["muted"], weight=600, anchor="end"))
b, _ = btn(682, sy + 48, "View recipient list", "outline", w=170); out.append(b)
b, _ = btn(864, sy + 48, "Preview samples", "outline", w=150); out.append(b)

# send progress
sy2 = top + 388
p, cy = panel(28, sy2, 760, 320, "Last run  ·  Rundbrief 08/2026", "Resumable from persisted progress")
out.append(p)
out.append(progress(46, cy + 22, 724, 78, h=10))
out.append(text(46, cy + 54, "1 002 of 1 284 delivered · batch 6 of 7 · cancellable", size=12.5, fill=C["muted"]))
cols = [("State", 190, "l"), ("Count", 100, "r"), ("Meaning", 448, "l")]
rows = [
    [{"t": "Sent", "badge": "ok"}, "1 002", "accepted by the transport"],
    [{"t": "Queued", "badge": "info"}, "268", "waiting for the next batch window"],
    [{"t": "Bounced", "badge": "bad"}, "9", "IMAP correlation, address suppressed"],
    [{"t": "Suppressed", "badge": "warn"}, "5", "opted out after the snapshot was taken"],
]
t, _ = table(29, cy + 58, 758, cols, rows, row_h=34)
out.append(t)

# consent
p, cy = panel(804, sy2, W - 804 - 28, 320, "Consent and safety")
out.append(p)
items = ["Opt-in, legal basis, timestamp and source are recorded per address",
         "Suppression blocks sending unless a documented mandatory policy applies",
         "Retrying a batch cannot deliver twice to one recipient",
         "Header injection and recipient disclosure are prevented by construction",
         "Failed Listmonk sync never changes local consent silently",
         "Approval and send are separate permissions from drafting"]
bl, _ = bullet_list(822, cy + 24, items, budget=44)
out.append(bl)

write("12-newsletter.svg", out, W, 920)
