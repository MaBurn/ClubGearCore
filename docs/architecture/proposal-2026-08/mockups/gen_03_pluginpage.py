from _kit import *

o, top = chrome(active="Plugins", eyebrow_text="Core · CORE-03  ·  host-rendered",
                title="Inventory · Items",
                subtitle="This page is described by the plugin as data. The host renders it, checks permissions and validates input.",
                unread=3, actions=[("Export CSV", "outline"), ("New item", "primary")])
out = [o]
b, bw = badge(300, 82, "clubgear.plugin.inventory 2.4.0", "plugin"); out.append(b)

# left filter rail
fx, fw = 28, 258
p, cy = panel(fx, top, fw, 470, "Filters", "Plugin-declared, host-rendered")
out.append(p)
f, y1 = field(fx + 18, cy + 4, fw - 36, "Search", "Bohrmaschine", placeholder=False); out.append(f)
f, y2 = field(fx + 18, y1 + 14, fw - 36, "Warehouse", "Werkstatt Süd", kind="select"); out.append(f)
f, y3 = field(fx + 18, y2 + 14, fw - 36, "Category", "Elektrowerkzeug", kind="select"); out.append(f)
y4 = y3
out.append(text(fx + 18, y4 + 32, "Status", size=12, fill=C["muted"], weight=600))
cxp, cyp = fx + 18, y4 + 42
for lab, on in [("Available", True), ("On loan", True), ("Defect", False), ("Archived", False)]:
    if cxp + int(len(lab) * 6.8) + 26 > fx + fw - 18:
        cxp, cyp = fx + 18, cyp + 34
    c, cw = chip(cxp, cyp, lab, on)
    out.append(c)
    cxp += cw + 8
b, _ = btn(fx + 18, cyp + 54, "Apply", "primary", w=104); out.append(b)
b, _ = btn(fx + 132, cyp + 54, "Reset", "outline", w=90); out.append(b)

# table
tx, tw = 302, W - 302 - 28
p, cy = panel(tx, top, tw, 470, "Items", "148 results · page 2 of 8")
out.append(p)
cols = [("Inventory no.", 130, "l"), ("Description", 250, "l"), ("Category", 150, "l"),
        ("Warehouse", 150, "l"), ("Qty", 60, "r"), ("Status", 120, "l"), ("Actions", 90, "c")]
rows = [
    ["INV-2288", {"t": "Bohrmaschine Bosch GSB", "weight": 600}, "Elektrowerkzeug", "Werkstatt Süd", "1", {"t": "Available", "badge": "ok"}, "✎  ⋯"],
    ["INV-2291", {"t": "Akkuschrauber Makita", "weight": 600}, "Elektrowerkzeug", "Werkstatt Süd", "3", {"t": "On loan", "badge": "warn"}, "✎  ⋯"],
    ["INV-2304", {"t": "Schlagbohrhammer Hilti", "weight": 600}, "Elektrowerkzeug", "Lager Nord", "1", {"t": "Defect", "badge": "bad"}, "✎  ⋯"],
    ["INV-2310", {"t": "Bohrerset HSS 19-tlg.", "weight": 600}, "Verbrauchsmaterial", "Werkstatt Süd", "12", {"t": "Available", "badge": "ok"}, "✎  ⋯"],
    ["INV-2318", {"t": "Bohrständer Wabeco", "weight": 600}, "Werkstattausstattung", "Werkstatt Süd", "1", {"t": "Available", "badge": "ok"}, "✎  ⋯"],
    ["INV-2325", {"t": "Kernbohrmaschine", "weight": 600}, "Elektrowerkzeug", "Lager Nord", "1", {"t": "On loan", "badge": "warn"}, "✎  ⋯"],
]
t, ty = table(tx + 1, cy + 6, tw - 2, cols, rows)
out.append(t)
# pagination
out.append(text(tx + 18, ty + 34, "Showing 21–26 of 148", size=12.5, fill=C["muted"]))
pxs = tx + tw - 250
for lab, act in [("‹", False), ("1", False), ("2", True), ("3", False), ("4", False), ("…", False), ("8", False), ("›", False)]:
    out.append(rect(pxs, ty + 16, 30, 30, C["brand"] if act else C["white"], C["brand"] if act else C["input_border"], rx=6))
    out.append(text(pxs + 15, ty + 36, lab, size=12.5, fill=C["white"] if act else C["ink"], weight=600 if act else 400, anchor="middle"))
    pxs += 34

# contract strip
sy = top + 486
p, cy = panel(28, sy, W - 56, 150, "What the host owns and what the plugin owns")
out.append(p)
host = ["Authentication + CSRF", "Permission checks", "Model validation", "Rendering + encoding", "Success / error messages", "Pagination + sorting"]
plug = ["Page + column definition", "Filter + form schema", "Row data", "Command handlers", "Own tables (schema-isolated)", "Own permissions"]
out.append(text(46, cy + 30, "Host (ClubGear Core)", size=13, fill=C["brand"], weight=700))
out.append(text(668, cy + 30, "Plugin (contract implementation)", size=13, fill="#5b3d86", weight=700))
for i, s in enumerate(host):
    out.append(text(46 + (i % 3) * 200, cy + 58 + (i // 3) * 24, "· " + s, size=12.5, fill=C["body"]))
for i, s in enumerate(plug):
    out.append(text(668 + (i % 3) * 200, cy + 58 + (i // 3) * 24, "· " + s, size=12.5, fill=C["body"]))
out.append(line(650, sy + 44, 650, sy + 138, C["panel_border"]))

n, _ = note(28, sy + 158, W - 56,
            ["No Core file compares a module id to pick behaviour. The same renderer serves Finance, Newsletter and any future plugin.",
             "Uploads and downloads on this page go through CORE-02; the JSON twin of this page is served from the same definition."])
out.append(n)

write("03-plugin-page.svg", out, W, 900)
