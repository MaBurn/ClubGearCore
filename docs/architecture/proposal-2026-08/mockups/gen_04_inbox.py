from _kit import *

o, top = chrome(active="Dashboard", eyebrow_text="Core · CORE-04",
                title="Notifications",
                subtitle="One inbox for core and plugin events. Email ships with core; every other medium is a provider plugin.",
                unread=3, actions=[("Preferences", "outline"), ("Mark all read", "primary")])
out = [o]

# list
lx, lw = 28, 470
p, cy = panel(lx, top, lw, 640, "Inbox")
out.append(p)
cxp = lx + 18
for lab, on in [("All", False), ("Unread (3)", True), ("Archived", False)]:
    c, cw = chip(cxp, cy + 6, lab, on); out.append(c); cxp += cw + 8
out.append(line(lx, cy + 46, lx + lw, cy + 46))

items = [
    (True, "Rental request awaiting your approval", "Marketplace · rent.request.created", "12 min ago", "warn", "Action"),
    (True, "SEPA batch 2026-08 exported", "Finance · finance.sepa.exported", "1 h ago", "info", "Info"),
    (True, "New member registration to review", "Core · member.registration.pending", "3 h ago", "warn", "Action"),
    (False, "Inventory item INV-2304 marked defect", "Inventory · inv.item.defect", "Yesterday", "grey", "Info"),
    (False, "Password changed", "Core · security.password.changed", "Yesterday", "grey", "Security"),
    (False, "Newsletter 08/2026 finished sending", "Newsletter · news.campaign.finished", "2 days ago", "grey", "Info"),
    (False, "Calendar: Jahreshauptversammlung moved", "Calendar · event.rescheduled", "3 days ago", "grey", "Info"),
]
iy = cy + 58
for i, (unread, title, src, when, kind, tag) in enumerate(items):
    sel = i == 0
    if sel:
        out.append(rect(lx + 10, iy - 6, lw - 20, 72, "rgba(0,119,186,0.07)", rx=8))
        out.append(rect(lx + 10, iy - 6, 3, 72, C["brand"], rx=2))
    if unread:
        out.append(f'<circle cx="{lx + 30}" cy="{iy + 14}" r="4.5" fill="{C["accent"]}"/>')
    out.append(text(lx + 44, iy + 18, title, size=13.5, fill=C["ink"] if unread else C["body"],
                    weight=700 if unread else 400))
    out.append(text(lx + 44, iy + 40, src, size=11.5, fill=C["muted"]))
    out.append(text(lx + lw - 20, iy + 18, when, size=11.5, fill=C["muted"], anchor="end"))
    b, bw = badge(lx + lw - 20 - 62, iy + 30, tag, kind); out.append(b)
    out.append(line(lx + 20, iy + 60, lx + lw - 20, iy + 60, C["hairline"]))
    iy += 74

# detail
dx, dw = 516, W - 516 - 28
p, cy = panel(dx, top, dw, 452, "Rental request awaiting your approval",
              "Marketplace · rent.request.created · 30.08.2026 09:12 · severity: action required")
out.append(p)
out.append(text(dx + 18, cy + 30, "Clara Weiss requested »Anhänger 750 kg« from 05.09. to 08.09.2026.", size=13.5))
out.append(text(dx + 18, cy + 54, "Deposit €150 · pickup Werkstatt Süd · liability notice accepted.", size=13.5, fill=C["muted"]))
b, _ = btn(dx + 18, cy + 76, "Open rental order", "primary", w=160); out.append(b)
b, _ = btn(dx + 188, cy + 76, "Mark as read", "outline", w=118); out.append(b)
b, _ = btn(dx + 316, cy + 76, "Delete", "danger", w=86); out.append(b)

out.append(line(dx, cy + 132, dx + dw, cy + 132))
out.append(eyebrow(dx + 18, cy + 158, "Delivery trail — idempotency key rent-4471-approve-1"))
trail = [
    ("In-app inbox", "Delivered 09:12:04", "ok", "core"),
    ("E-mail (SMTP)", "Delivered 09:12:07 · smtp id 8f31c", "ok", "core"),
    ("Matrix", "Provider disabled — policy: fall back to e-mail", "warn", "plugin"),
    ("Telegram", "Not selected for this event category", "grey", "plugin"),
]
ty = cy + 172
for name, status, kind, owner in trail:
    out.append(rect(dx + 18, ty, dw - 36, 40, C["surface"], C["panel_border"], rx=8))
    out.append(text(dx + 34, ty + 25, name, size=13, fill=C["ink"], weight=600))
    b, bw = badge(dx + 152, ty + 10, "core" if owner == "core" else "plugin", "info" if owner == "core" else "plugin")
    out.append(b)
    out.append(text(dx + 224, ty + 25, status, size=12.5, fill=C["muted"]))
    b, bw = badge(dx + dw - 36 - 74, ty + 10, {"ok": "sent", "warn": "skipped", "grey": "off"}[kind], kind)
    out.append(b)
    ty += 46

# admin panel
ay = top + 468
p, cy = panel(dx, ay, dw, 224, "Failed deliveries (administrators)", "Content redacted · retry is idempotent")
out.append(p)
cols = [("Event", 210, "l"), ("Channel", 130, "l"), ("Recipient", 140, "l"), ("Attempts", 80, "r"), ("State", 110, "l")]
rows = [
    [{"t": "news.campaign.finished", "weight": 600}, "Matrix", "u***@club.de", "3", {"t": "Retrying", "badge": "warn"}],
    [{"t": "finance.demand.created", "weight": 600}, "E-mail", "b***@gmx.de", "5", {"t": "Bounced", "badge": "bad"}],
    [{"t": "member.welcome", "weight": 600}, "Telegram", "chat ***4471", "1", {"t": "No destination", "badge": "grey"}],
]
t, _ = table(dx + 1, cy + 6, dw - 2, cols, rows)
out.append(t)

write("04-notification-inbox.svg", out, W, 940)
