from _kit import *

o, top = chrome(active="Members", eyebrow_text="Core · CORE-08",
                title="Clara Weiss  ·  Mitglied 4471",
                subtitle="Household relationships are explicit, dated and validated. Fee consequences stay in the Finance plugin.",
                unread=3, actions=[("Audit", "outline"), ("Edit member", "primary")])
out = [o]

tb, ty = tabs(28, top - 6, W - 56, ["Overview", "Household", "Membership", "Communication", "Service book (plugin)", "Vehicles (plugin)"], 1)
out.append(tb)

# household tree
p, cy = panel(28, ty + 18, 620, 420, "Household", "Primary member: Dirk Weiss · household hh_9931")
out.append(p)
fam = [
    (0, "Dirk Weiss", "Primary member", "Vollmitglied", "since 01.01.2019", "ok", False),
    (1, "Clara Weiss", "Spouse", "Familienmitglied", "since 01.01.2019", "ok", True),
    (1, "Jonas Weiss", "Child", "Jugendmitglied", "since 12.04.2021", "ok", False),
    (1, "Mia Weiss", "Child", "Jugendmitglied", "01.02.2016 – 31.12.2025", "grey", False),
]
fy = cy + 14
for depth, name, rel, mtype, span, kind, sel in fam:
    x = 46 + depth * 26
    if sel:
        out.append(rect(38, fy - 4, 600, 60, "rgba(0,119,186,0.07)", rx=8))
        out.append(rect(38, fy - 4, 3, 60, C["brand"], rx=2))
    if depth:
        out.append(line(46 + 8, fy - 12, 46 + 8, fy + 26, C["input_border"]))
        out.append(line(46 + 8, fy + 26, x + 2, fy + 26, C["input_border"]))
    out.append(avatar(x + 8, fy + 8, "".join(w[0] for w in name.split())[:2], r=15))
    out.append(text(x + 48, fy + 22, name, size=13.5, fill=C["ink"], weight=700))
    b, bw = badge(x + 48 + int(len(name) * 8) + 10, fy + 10, rel, "info" if kind == "ok" else "grey"); out.append(b)
    out.append(text(x + 48, fy + 42, mtype + " · " + span, size=11.5, fill=C["muted"]))
    if kind == "grey":
        b2, _ = badge(560, fy + 16, "ended", "grey"); out.append(b2)
    fy += 68
out.append(line(28, cy + 320, 648, cy + 320))
out.append(text(46, cy + 348, "Ending a relationship keeps a dated, auditable history — nothing is deleted.", size=12, fill=C["muted"]))

# validation
p, cy = panel(664, ty + 18, W - 664 - 28, 420, "Rules enforced on save")
out.append(p)
rules = [("No self-link", "ok"), ("No duplicate active link", "ok"), ("No cycle in the household", "ok"),
         ("Effective range must be valid", "ok"), ("At most one active primary household", "ok"),
         ("Membership type change is validated against family rules", "warn")]
ry = cy + 22
for r, k in rules:
    out.append(text(682, ry, "✓" if k == "ok" else "!", size=13, fill=C["success"] if k == "ok" else C["warn"], weight=700))
    out.append(text(702, ry, r, size=12.5, fill=C["body"]))
    ry += 30
out.append(alert(682, ry + 6, W - 664 - 64,
                 "Jonas turns 18 on 04.11.2026 — Jugendmitglied rule expires.", "warn", h=46, icon="⚠"))
out.append(text(682, ry + 74, "Privacy", size=13, fill=C["ink"], weight=700))
for i, l in enumerate(["Seeing a relationship does not grant access to",
                       "another adult's profile. Access to a dependent",
                       "requires an explicit policy plus identity link."]):
    out.append(text(682, ry + 98 + i * 19, l, size=12, fill=C["muted"]))

# consumer contract
p, cy = panel(28, ty + 454, W - 56, 190, "What other modules see", "A minimal read model — never the member entity")
out.append(p)
cols = [("Consumer", 240, "l"), ("Reads", 420, "l"), ("Must not", 400, "l"), ("Owner", 130, "l")]
rows = [
    [{"t": "Finance plugin", "weight": 600}, "household id, role, active range, membership type", "compute in core, query core tables", {"t": "plugin", "badge": "plugin"}],
    [{"t": "Newsletter plugin", "weight": 600}, "one recipient per household flag", "read child contact data", {"t": "plugin", "badge": "plugin"}],
    [{"t": "Self-service", "weight": 600}, "only relationships the user may see", "reveal other adults' data", {"t": "core", "badge": "info"}],
]
t, _ = table(29, cy + 6, W - 58, cols, rows)
out.append(t)

write("09-family-relationships.svg", out, W, 900)
