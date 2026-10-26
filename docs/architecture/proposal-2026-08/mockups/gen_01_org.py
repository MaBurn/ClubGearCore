from _kit import *

o, top = chrome(active="Organisation",
                eyebrow_text="Core · CORE-01",
                title="Organisation structure",
                subtitle="One authoritative hierarchy. Units are referenced by calendar, inventory, notifications and plugins.",
                unread=3,
                actions=[("Audit trail", "outline"), ("New unit", "primary")])
out = [o]

# ---- left: tree -------------------------------------------------------
px, py, pw, ph = 28, top, 372, 630
p, cy = panel(px, py, pw, ph, "Units", "Search, drag to reorder or move")
out.append(p)
out.append(rect(px + 18, cy + 8, pw - 36, 34, C["white"], C["input_border"], rx=6))
out.append(text(px + 30, cy + 30, "Search units…", size=13, fill=C["muted2"]))
out.append(text(px + pw - 34, cy + 31, "⌕", size=16, fill=C["muted"], anchor="middle"))

tree = [
    (0, "Verein (root)", "grey", "Association", False),
    (1, "Regionalgruppe Nord", None, "Chapter", False),
    (2, "Ortsgruppe Hamburg", None, "Local unit", False),
    (2, "Ortsgruppe Kiel", None, "Local unit", False),
    (1, "Regionalgruppe Süd", None, "Chapter", True),
    (2, "Ortsgruppe München", None, "Local unit", False),
    (1, "Technik-Referat", None, "Committee", False),
    (2, "AG Werkstatt", None, "Working group", False),
    (1, "Redaktion", None, "Committee", False),
    (1, "Ortsgruppe Bremen", "grey", "Archived", False),
]
ty = cy + 58
for depth, name, tag, kind, selected in tree:
    x = px + 20 + depth * 20
    if selected:
        out.append(rect(px + 10, ty - 4, pw - 20, 34, "rgba(0,119,186,0.08)", rx=6))
        out.append(rect(px + 10, ty - 4, 3, 34, C["brand"], rx=2))
    if depth < 2:
        out.append(text(x - 2, ty + 17, "▾" if depth < 2 else "", size=10, fill=C["muted"]))
    fill = C["muted"] if tag == "grey" else (C["brand"] if selected else C["body"])
    out.append(text(x + 14, ty + 17, name, size=13.5, fill=fill,
                    weight=600 if selected or depth == 0 else 400))
    out.append(text(px + pw - 20, ty + 17, kind, size=11, fill=C["muted2"], anchor="end"))
    ty += 34

out.append(line(px, py + ph - 56, px + pw, py + ph - 56))
out.append(text(px + 18, py + ph - 28, "10 units · 1 archived", size=12, fill=C["muted"]))

# ---- right: unit detail ----------------------------------------------
dx, dw = 424, W - 424 - 28
p, cy = panel(dx, top, dw, 258, "Regionalgruppe Süd", "Unit ID  ou_2f19c4 · created 12.03.2024 · active")
out.append(p)
b, bw = badge(dx + dw - 96, top + 22, "Chapter", "info"); out.append(b)

f, _ = field(dx + 18, cy + 4, 300, "Name", "Regionalgruppe Süd"); out.append(f)
f, _ = field(dx + 336, cy + 4, 150, "Short name", "RG-SÜD"); out.append(f)
f, _ = field(dx + 504, cy + 4, 200, "Parent unit", "Verein (root)", kind="select"); out.append(f)
f, _ = field(dx + 722, cy + 4, 90, "Sort", "20"); out.append(f)

f, _ = field(dx + 18, cy + 76, 300, "Unit type", "Chapter", kind="select"); out.append(f)
f, _ = field(dx + 336, cy + 76, 240, "Join policy", "Self-service, needs approval", kind="select"); out.append(f)
out.append(text(dx + 594, cy + 90, "Permission inheritance", size=12, fill=C["muted"], weight=600))
out.append(toggle(dx + 594, cy + 100, True))
out.append(text(dx + 636, cy + 114, "Descendants inherit", size=12.5, fill=C["body"]))
b, _ = btn(dx + dw - 106, top + 258 - 48, "Save", "primary", w=88); out.append(b)
b, _ = btn(dx + dw - 204, top + 258 - 48, "Archive", "outline", w=86); out.append(b)

# members table
my = top + 274
p, cy = panel(dx, my, dw, 250, "Members of this unit", "48 active assignments")
out.append(p)
b, _ = btn(dx + dw - 130, my + 18, "Add member", "outline", w=112); out.append(b)
cols = [("Member", 250, "l"), ("Role in unit", 190, "l"), ("Since", 120, "l"),
        ("Source", 130, "l"), ("Status", 120, "l"), ("", 80, "c")]
rows = [
    [{"t": "Anna Falk", "weight": 600}, "Unit administrator", "01.04.2024", {"t": "Assigned", "muted": True}, {"t": "Active", "badge": "ok"}, "⋯"],
    [{"t": "Bernd Kruse", "weight": 600}, "Contact", "14.06.2024", {"t": "Assigned", "muted": True}, {"t": "Active", "badge": "ok"}, "⋯"],
    [{"t": "Clara Weiss", "weight": 600}, "Member", "02.01.2025", {"t": "Self-service", "muted": True}, {"t": "Pending", "badge": "warn"}, "⋯"],
    [{"t": "Dirk Sommer", "weight": 600}, "Member", "19.09.2023", {"t": "Import", "muted": True}, {"t": "Active", "badge": "ok"}, "⋯"],
]
t, _ = table(dx + 1, cy + 8, dw - 2, cols, rows)
out.append(t)

# delegated admins
ay = my + 266
p, cy = panel(dx, ay, dw, 92, None)
out.append(p)
out.append(text(dx + 18, ay + 30, "Delegated administration", size=14, fill=C["brand"], weight=600))
out.append(text(dx + 18, ay + 54, "Anna Falk may manage this unit and its descendants — org.unit.manage, org.membership.manage", size=12.5, fill=C["muted"]))
out.append(text(dx + 18, ay + 74, "She cannot touch Regionalgruppe Nord, the root unit, or global roles.", size=12.5, fill=C["muted"]))
b, _ = btn(dx + dw - 130, ay + 30, "Manage scope", "outline", w=112); out.append(b)

write("01-org-structure.svg", out)
