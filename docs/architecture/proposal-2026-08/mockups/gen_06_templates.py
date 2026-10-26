from _kit import *

o, top = chrome(active="Admin", eyebrow_text="Core · CORE-05",
                title="Template  ·  member.welcome",
                subtitle="Channel-neutral, versioned, addressed by semantic event key — callers never store template ids.",
                unread=3, actions=[("Preview", "outline"), ("Publish version 8", "primary")])
out = [o]

# left: event map
lx, lw = 28, 300
p, cy = panel(lx, top, lw, 640, "Event keys", "Core and plugin contributions")
out.append(p)
groups = [
    ("Core", [("member.welcome", True, None), ("member.email.changed", False, None),
              ("security.password.reset", False, None), ("event.registration.confirmed", False, None)]),
    ("Finance plugin", [("finance.demand.created", False, "plugin"), ("finance.sepa.prenotification", False, "plugin")]),
    ("Newsletter plugin", [("news.campaign.finished", False, "plugin")]),
    ("Service book plugin", [("servicebook.due.reminder", False, "plugin")]),
]
gy = cy + 10
for gname, keys in groups:
    out.append(eyebrow(lx + 18, gy + 14, gname))
    gy += 24
    for k, sel, tag in keys:
        if sel:
            out.append(rect(lx + 10, gy - 2, lw - 20, 32, "rgba(0,119,186,0.08)", rx=6))
            out.append(rect(lx + 10, gy - 2, 3, 32, C["brand"], rx=2))
        out.append(text(lx + 22, gy + 19, k, size=12.5, fill=C["brand"] if sel else C["body"],
                        weight=600 if sel else 400))
        gy += 32
    gy += 10
out.append(line(lx, top + 640 - 52, lx + lw, top + 640 - 52))
out.append(text(lx + 18, top + 640 - 22, "de-DE · en-GB · fallback de-DE", size=12, fill=C["muted"]))

# center: editor
ex, ew = 344, 570
p, cy = panel(ex, top, ew, 640, "Version 8 (draft)", "de-DE · channel: e-mail · layout: club-default")
out.append(p)
tb, ty = tabs(ex + 18, cy + 4, ew - 36, ["E-mail", "In-app", "Plain text", "Placeholders"], 0)
out.append(tb)
f, y1 = field(ex + 18, ty + 12, ew - 36, "Subject", "Willkommen im Verein, {{member.firstName}}!"); out.append(f)
out.append(text(ex + 18, y1 + 34, "Body", size=12, fill=C["muted"], weight=600))
out.append(rect(ex + 18, y1 + 42, ew - 36, 250, C["white"], C["input_border"], rx=6))
body = [
    "Hallo {{member.firstName}} {{member.lastName}},",
    "",
    "willkommen bei {{club.name}}. Deine Mitgliedsnummer ist",
    "{{member.number}}, dein Beitragssatz {{membership.feeLabel}}.",
    "",
    "Deine Ortsgruppe: {{unit.name}}",
    "Ansprechpartner: {{unit.contactName}}",
    "",
    "{{> layout.footer }}",
]
by = y1 + 68
for l in body:
    out.append(text(ex + 32, by, l, size=12.5, fill=C["body"] if l else C["muted"]))
    by += 20
out.append(text(ex + 18, y1 + 316, "Declared placeholders", size=12, fill=C["muted"], weight=600))
chx, chy = ex + 18, y1 + 326
for lab, ok in [("member.firstName", True), ("member.lastName", True), ("member.number", True),
                ("club.name", True), ("unit.name", True), ("membership.feeLabel", True),
                ("unit.contactName", True), ("member.iban", False)]:
    if chx + int(len(lab) * 6.8) + 26 > ex + ew - 18:
        chx, chy = ex + 18, chy + 32
    c, cw = chip(chx, chy, lab, ok)
    out.append(c)
    chx += cw + 8
out.append(alert(ex + 18, chy + 42, ew - 36,
                 "member.iban is not in the schema for this event — publication is blocked.", "bad", icon="✕"))

# right: versions + preview
rx, rw = 930, W - 930 - 28
p, cy = panel(rx, top, rw, 300, "Version history", "Every save is immutable")
out.append(p)
vers = [("v8", "draft", "you · today 10:42", "warn"), ("v7", "current", "A. Falk · 12.06.2026", "ok"),
        ("v6", "archived", "A. Falk · 03.02.2026", "grey"), ("v5", "archived", "import · 11.11.2025", "grey")]
vy = cy + 12
for v, st, who, kind in vers:
    out.append(rect(rx + 16, vy, rw - 32, 50, C["surface"], C["panel_border"], rx=8))
    out.append(text(rx + 30, vy + 22, v, size=13, fill=C["ink"], weight=700))
    b, bw = badge(rx + 60, vy + 10, st, kind); out.append(b)
    out.append(text(rx + 30, vy + 40, who, size=11, fill=C["muted"]))
    if st == "archived":
        out.append(text(rx + rw - 30, vy + 30, "↻ roll back", size=11.5, fill=C["brand"], anchor="end"))
    if st == "current":
        out.append(text(rx + rw - 30, vy + 30, "compare", size=11.5, fill=C["brand"], anchor="end"))
    vy += 58

p, cy = panel(rx, top + 316, rw, 324, "Preview", "Rendered against controlled sample data")
out.append(p)
out.append(rect(rx + 16, cy + 10, rw - 32, 214, C["white"], C["panel_border"], rx=8))
out.append(rect(rx + 16, cy + 10, rw - 32, 34, C["surface2"], rx=8))
out.append(text(rx + 30, cy + 32, "Willkommen im Verein, Clara!", size=12.5, fill=C["ink"], weight=700))
pl = ["Hallo Clara Weiss,", "", "willkommen bei Musterverein e.V. Deine", "Mitgliedsnummer ist 4471, dein Beitragssatz",
      "Familienmitglied (ermäßigt).", "", "Deine Ortsgruppe: Ortsgruppe München", "Ansprechpartner: Bernd Kruse"]
py2 = cy + 62
for l in pl:
    out.append(text(rx + 30, py2, l, size=11.5, fill=C["body"]))
    py2 += 18
out.append(alert(rx + 16, cy + 236, rw - 32, "Same version + same input = same output.", "info", h=38))
out.append(text(rx + 16, cy + 292, "Missing values are shown as ⟨unset⟩, never as empty text.", size=11.5, fill=C["muted"]))

write("06-template-editor.svg", out, W, 900)
